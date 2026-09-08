using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 卡牌控制层 —— 处理卡牌的拖拽交互（挂在卡牌预制体上，与 CardView 同物体）
/// 拖起时由 CardRuleManager 判定合法牌串，松开时判定落点并移动或回弹
/// </summary>
public class CardController : MonoBehaviour
{
    /// <summary>当前正在拖拽的 Controller（全局互斥，同一时刻只拖一串）</summary>
    private static CardController _activeDrag;

    /// <summary>拖拽时临时抬高的层级偏移</summary>
    private const int DragSortingOffset = 10000;

    private CardView _view;
    private Camera _camera;

    private bool _dragging;

    /// <summary>被拖的牌（锚点牌及其同列下方所有牌）</summary>
    private List<CardData> _draggedCards;

    /// <summary>拖拽开始时各牌的世界坐标</summary>
    private Vector3[] _dragStartPositions;

    /// <summary>拖拽开始时各牌的渲染排序</summary>
    private int[] _dragStartSortingOrders;

    /// <summary>按下点与锚点牌中心的偏移</summary>
    private Vector3 _dragOffset;

    /// <summary>是否从牌包拖出</summary>
    private bool _dragFromPocket;

    /// <summary>拖拽的牌所在牌包索引（从牌包拖出时有效）</summary>
    private int _dragPocketIndex;

    private void Awake()
    {
        _view = GetComponent<CardView>();
        _camera = Camera.main;
    }

    private void Update()
    {
        if (Mouse.current == null || _view == null || _camera == null) return;
        if (CardViewManager.Instance.IsAnimating) return;

        bool pressed = Mouse.current.leftButton.wasPressedThisFrame;
        bool released = Mouse.current.leftButton.wasReleasedThisFrame;

        // 按下：无人拖拽，且本牌是鼠标命中的最上面的牌 → 开始拖拽
        if (pressed && _activeDrag == null && IsTopmostHit())
        {
            _activeDrag = this;
            StartDrag();
        }

        // 只有拖拽者自己处理拖拽与释放
        if (_activeDrag != this) return;

        if (released)
        {
            EndDrag();
            _activeDrag = null;
        }
        else if (_dragging)
        {
            UpdateDrag();
        }
    }

    /// <summary>鼠标是否点中本牌，且本牌是所有命中牌里渲染最靠前的</summary>
    private bool IsTopmostHit()
    {
        var ray = _camera.ScreenPointToRay(Mouse.current.position.ReadValue());
        var hits = Physics.RaycastAll(ray);

        CardView top = null;
        int maxOrder = int.MinValue;

        foreach (var hit in hits)
        {
            var view = hit.collider.GetComponentInParent<CardView>();
            if (view == null) continue;

            var sr = view.GetComponent<SpriteRenderer>();
            int order = sr != null ? sr.sortingOrder : 0;
            if (order > maxOrder)
            {
                maxOrder = order;
                top = view;
            }
        }

        return top == _view;
    }

    /// <summary>开始拖拽：牌包牌拖单张，列牌由 CardRuleManager 判定合法牌串</summary>
    private void StartDrag()
    {
        _dragFromPocket = false;
        _dragPocketIndex = -1;

        CardData blockedCard = null;

        int pocketIndex = CardRuleManager.Instance.FindPocketIndex(_view.data);
        if (pocketIndex >= 0)
        {
            _draggedCards = new List<CardData> { _view.data };
            _dragFromPocket = true;
            _dragPocketIndex = pocketIndex;
        }
        else
        {
            _draggedCards = CardRuleManager.Instance.GetDraggableCards(_view.data, out blockedCard);
        }

        if (_draggedCards == null || _draggedCards.Count == 0)
        {
            // 拖不起来：阻塞点牌短暂变灰提示
            if (blockedCard != null)
            {
                var blockedView = CardViewManager.Instance.GetView(blockedCard);
                if (blockedView != null) CardAnimationHelper.FlashBlocked(blockedView);
            }
            _dragging = false;
            return;
        }

        _dragStartPositions = new Vector3[_draggedCards.Count];
        _dragStartSortingOrders = new int[_draggedCards.Count];
        for (int i = 0; i < _draggedCards.Count; i++)
        {
            var view = GetDragView(i);
            if (view == null) continue;

            _dragStartPositions[i] = view.transform.position;

            // 记录原始排序，并临时抬到高层（避免拖拽时被其他牌遮挡）
            var sr = view.GetComponent<SpriteRenderer>();
            int order = sr != null ? sr.sortingOrder : 0;
            _dragStartSortingOrders[i] = order;
            view.SetSortingOrder(order + DragSortingOffset);
        }

        Vector3 mouseWorld = ScreenToWorldPlane(Mouse.current.position.ReadValue());
        _dragOffset = transform.position - mouseWorld;

        // 拖起编排：锚点牌发光+投影+影子偏移，其余牌只发光
        for (int i = 0; i < _draggedCards.Count; i++)
        {
            var view = GetDragView(i);
            if (view == null) continue;
            if (i == 0) CardAnimationHelper.LiftAnchor(view, _draggedCards.Count);
            else CardAnimationHelper.LiftCard(view);
        }

        _dragging = true;
    }

    /// <summary>拖拽中：按偏移更新所有被拖牌位置（保持彼此相对关系）</summary>
    private void UpdateDrag()
    {
        Vector3 mouseWorld = ScreenToWorldPlane(Mouse.current.position.ReadValue());
        Vector3 anchorTarget = mouseWorld + _dragOffset + CardAnimationHelper.LiftOffset;

        for (int i = 0; i < _draggedCards.Count; i++)
        {
            var view = GetDragView(i);
            if (view == null) continue;

            Vector3 rel = _dragStartPositions[i] - _dragStartPositions[0];
            view.SetPosition(anchorTarget + rel);
        }
    }

    /// <summary>结束拖拽：判定落点（牌包/列），能移动则移动，否则回弹</summary>
    private void EndDrag()
    {
        // 放下编排：隐藏发光投影 + 影子恢复（无论移动成功还是回弹）
        for (int i = 0; i < _draggedCards.Count; i++)
        {
            var view = GetDragView(i);
            if (view != null) CardAnimationHelper.DropCard(view);
        }

        Vector3 mouseWorld = ScreenToWorldPlane(Mouse.current.position.ReadValue());
        int targetColumn = CardViewManager.Instance.GetColumnIndexAt(mouseWorld);
        int targetPocket = CardViewManager.Instance.GetPocketIndexAt(mouseWorld);

        bool moved = false;

        if (_dragFromPocket)
        {
            // 从牌包拖出：只能落到列
            if (targetColumn >= 0 && CardRuleManager.Instance.CanMoveFromPocket(_dragPocketIndex, targetColumn))
            {
                CardsManager.Instance.MoveFromPocket(_dragPocketIndex, targetColumn);
                moved = true;
            }
        }
        else
        {
            // 从列拖出：优先牌包（仅单张），否则列
            if (targetPocket >= 0 && _draggedCards.Count == 1 &&
                CardRuleManager.Instance.CanMoveToPocket(_draggedCards[0], targetPocket))
            {
                CardsManager.Instance.MoveToPocket(_draggedCards[0], targetPocket);
                moved = true;
            }
            else if (targetColumn >= 0 && CardRuleManager.Instance.CanMove(_draggedCards, targetColumn))
            {
                CardsManager.Instance.MoveCards(_draggedCards, targetColumn);
                moved = true;
            }
        }

        if (!moved)
        {
            // 回弹：恢复原始排序与位置（影子由 DropCard 恢复）
            for (int i = 0; i < _draggedCards.Count; i++)
            {
                var view = GetDragView(i);
                if (view == null) continue;
                view.SetSortingOrder(_dragStartSortingOrders[i]);
                view.SetPosition(_dragStartPositions[i]);
            }
        }

        _dragging = false;
        _draggedCards = null;
        _dragStartPositions = null;
        _dragStartSortingOrders = null;
        _dragFromPocket = false;
        _dragPocketIndex = -1;
    }

    /// <summary>获取被拖第 i 张牌的 View（牌包牌用自身，列牌从管理器查）</summary>
    private CardView GetDragView(int i)
    {
        if (_dragFromPocket && i == 0) return _view;
        return CardViewManager.Instance.GetView(_draggedCards[i]);
    }

    /// <summary>屏幕坐标 → 卡牌所在平面（z=0）的世界坐标</summary>
    private Vector3 ScreenToWorldPlane(Vector2 screenPos)
    {
        var ray = _camera.ScreenPointToRay(screenPos);
        var plane = new Plane(Vector3.forward, Vector3.zero);
        if (plane.Raycast(ray, out float dist))
        {
            return ray.GetPoint(dist);
        }
        return Vector3.zero;
    }
}
