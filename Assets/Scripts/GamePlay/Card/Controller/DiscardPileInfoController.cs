using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 弃牌堆信息 —— 点击展示弃牌堆中的全部牌（重新按点数、花色排序）
/// 挂在场景中的物体上（需要 Collider 接收点击），一般放在弃牌堆附近
/// </summary>
public class DiscardPileInfoController : MonoBehaviour
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

        var cards = CardsManager.Instance.GetDiscardPileCards();
        if (cards.Count > 0)
        {
            ShowCardsPanel.ShowCards(cards);
        }
    }
}
