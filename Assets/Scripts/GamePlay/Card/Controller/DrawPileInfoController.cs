using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 发牌堆信息 —— 点击展示发牌堆中的全部牌（重新按点数、花色排序，不按栈顺序）
/// 挂在场景中的物体上（需要 Collider 接收点击），一般放在发牌堆附近
/// </summary>
public class DrawPileInfoController : MonoBehaviour
{
    private Camera _camera;
    private Collider _collider;

    private void Awake()
    {
        _camera = Camera.main;
        _collider = GetComponent<Collider>();
    }

    private void Update()
    {
        if (Mouse.current == null || _camera == null || _collider == null) return;
        if (!CardGameModule.Instance.CanCardInput) return;
        if (!Mouse.current.leftButton.wasPressedThisFrame) return;

        var ray = _camera.ScreenPointToRay(Mouse.current.position.ReadValue());
        if (!Physics.Raycast(ray, out var hit) || hit.collider != _collider) return;

        var cards = CardsManager.Instance.GetDrawPileCards();
        if (cards.Count > 0)
        {
            ShowCardsPanel.ShowCards(cards);
        }
    }
}
