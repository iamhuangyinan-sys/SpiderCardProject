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

    private void Awake()
    {
        _view = GetComponent<CardView>();
        _camera = Camera.main;
    }

    private void Update()
    {
        if (Mouse.current == null || _view == null || _camera == null) return;

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

    /// <summary>开始拖拽：由 CardRuleManager 判定合法牌串，记录起始位置</summary>
    private void StartDrag()
    {
        _draggedCards = CardRuleManager.Instance.GetDraggableCards(_view.data);
        if (_draggedCards == null || _draggedCards.Count == 0)
        {
            _dragging = false;
            return;
        }

        _dragStartPositions = new Vector3[_draggedCards.Count];
        _dragStartSortingOrders = new int[_draggedCards.Count];
        for (int i = 0; i < _draggedCards.Count; i++)
        {
            var view = CardViewManager.Instance.GetView(_draggedCards[i]);
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

        _dragging = true;
    }

    /// <summary>拖拽中：按偏移更新所有被拖牌位置（保持彼此相对关系）</summary>
    private void UpdateDrag()
    {
        Vector3 mouseWorld = ScreenToWorldPlane(Mouse.current.position.ReadValue());
        Vector3 anchorTarget = mouseWorld + _dragOffset;

        for (int i = 0; i < _draggedCards.Count; i++)
        {
            var view = CardViewManager.Instance.GetView(_draggedCards[i]);
            if (view == null) continue;

            Vector3 rel = _dragStartPositions[i] - _dragStartPositions[0];
            view.SetPosition(anchorTarget + rel);
        }
    }

    /// <summary>结束拖拽：判定落点，能移动则移动，否则回弹</summary>
    private void EndDrag()
    {
        Vector3 mouseWorld = ScreenToWorldPlane(Mouse.current.position.ReadValue());
        int targetColumn = CardViewManager.Instance.GetColumnIndexAt(mouseWorld);

        bool moved = false;
        if (targetColumn >= 0 && CardRuleManager.Instance.CanMove(_draggedCards, targetColumn))
        {
            CardsManager.Instance.MoveCards(_draggedCards, targetColumn);
            moved = true;
        }

        if (!moved)
        {
            // 回弹：恢复原始排序与位置
            for (int i = 0; i < _draggedCards.Count; i++)
            {
                var view = CardViewManager.Instance.GetView(_draggedCards[i]);
                if (view == null) continue;
                view.SetSortingOrder(_dragStartSortingOrders[i]);
                view.SetPosition(_dragStartPositions[i]);
            }
        }

        _dragging = false;
        _draggedCards = null;
        _dragStartPositions = null;
        _dragStartSortingOrders = null;
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
