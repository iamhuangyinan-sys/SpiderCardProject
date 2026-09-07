using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 发牌堆控制层 —— 点击发牌堆，给每列顶部发一张牌
/// </summary>
public class DrawPileController : MonoBehaviour
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
        if (CardViewManager.Instance.IsAnimating) return;

        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            var ray = _camera.ScreenPointToRay(Mouse.current.position.ReadValue());
            if (Physics.Raycast(ray, out var hit) && hit.collider == _collider)
            {
                CardsManager.Instance.DealFromDrawPile();
            }
        }
    }
}
