using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 世界空间的一行图片布局（SpriteRenderer 版的 HorizontalLayoutGroup）。
///
/// 为什么不用 UI 的 HorizontalLayoutGroup：那个只作用于 RectTransform，而卡牌是 SpriteRenderer，
/// 塞一个 World Space Canvas 进来会打断合批、还要额外同步 Canvas 与 SpriteRenderer 的排序。
/// 这里只做最朴素的三件事：量宽度 → 求和 → 逐个摆 x。
///
/// 用法（挂在底板节点上，例如卡牌的 MutipleSuit / MutipleRank）：
///   底板自己的 SpriteRenderer   = 背景板（可空）
///   底板下面的若干子节点         = 图标槽位（SpriteRenderer，Sprite 留空、默认失活）
/// 槽位数量 = 最多显示几个图标；槽位的 y / 缩放 / sortingOrder 都在预制体里定，
/// 代码只改「显示哪个 sprite」和 x 位置（y 统一取第一个槽位的）
///
/// 注意：图标 pivot 用 center（SpriteRenderer 默认），否则居中的那半个宽度会对不上
/// </summary>
public class SpriteRowLayout : MonoBehaviour
{
    /// <summary>图标之间的间距（世界单位）</summary>
    [SerializeField] private float spacing = 0.06f;

    /// <summary>图标整体缩放系数，乘在槽位自己的 localScale 上（1 = 保持预制体里的原始大小）</summary>
    [SerializeField] private float iconScale = 1f;

    /// <summary>
    /// true = 左对齐：整行的左边缘 = 节点自己的位置（图标从节点位置向右排，数量变了左边也不动）
    /// false = 居中：整行以节点原点为中心左右铺开
    /// </summary>
    [SerializeField] private bool alignLeft = true;

    /// <summary>图标槽位（预制体里的子 SpriteRenderer，按层级顺序）</summary>
    private readonly List<SpriteRenderer> _slots = new();

    /// <summary>各槽位的初始缩放（iconScale 乘在这上面，保住预制体里各自的调法）</summary>
    private readonly List<Vector3> _baseScales = new();

    /// <summary>整行的 y（取第一个槽位的初始 y，其余槽位用自己的 x 之外的 y 一律忽略）</summary>
    private float _rowY;

    /// <summary>图标槽位（CardView 登记渲染排序时要用）</summary>
    public IReadOnlyList<SpriteRenderer> Slots => _slots;

    private void Awake()
    {
        foreach (var renderer in GetComponentsInChildren<SpriteRenderer>(true))
        {
            if (renderer == null || renderer.transform == transform) continue;   // 跳过底板自己

            _slots.Add(renderer);
            _baseScales.Add(renderer.transform.localScale);
        }

        if (_slots.Count > 0) _rowY = _slots[0].transform.localPosition.y;

        SetItems(null);
    }

    /// <summary>喂一串要显示的图（null / 空 = 全部隐藏）。多余、以及 sprite 为 null 的槽位自动关掉</summary>
    public void SetItems(IReadOnlyList<Sprite> sprites)
    {
        int count = Mathf.Min(sprites?.Count ?? 0, _slots.Count);

        // 1. 先定每个槽位显示什么（顺便量出实际宽度）
        for (int i = 0; i < count; i++)
        {
            var slot = _slots[i];

            if (sprites[i] == null)
            {
                slot.gameObject.SetActive(false);
                continue;
            }

            slot.sprite = sprites[i];
            slot.transform.localScale = _baseScales[i] * iconScale;
            slot.gameObject.SetActive(true);
        }

        // 关掉多余的槽位
        for (int i = count; i < _slots.Count; i++)
        {
            _slots[i].gameObject.SetActive(false);
        }

        // 2. 整行宽度（只算真的显示出来的，间距只在图标之间）
        float total = 0f;
        int visible = 0;
        for (int i = 0; i < count; i++)
        {
            if (!_slots[i].gameObject.activeSelf) continue;

            total += SlotWidth(_slots[i]);
            visible++;
        }
        total += spacing * Mathf.Max(0, visible - 1);

        // 3. 摆 x：居中时第一个图标的「左边」=-total/2；每个图标按自己的半宽放到中心点上
        float x = alignLeft ? 0f : -total * 0.5f;
        for (int i = 0; i < count; i++)
        {
            var slot = _slots[i];
            if (!slot.gameObject.activeSelf) continue;

            float width = SlotWidth(slot);
            slot.transform.localPosition = new Vector3(x + width * 0.5f, _rowY, 0f);
            x += width + spacing;
        }
    }

    /// <summary>槽位当前宽度：sprite 自然宽度 × 当前缩放（Sliced / Tiled 用 renderer.size 的场合这里不适用）</summary>
    private float SlotWidth(SpriteRenderer slot)
    {
        if (slot.sprite == null) return 0f;

        return slot.sprite.bounds.size.x * slot.transform.localScale.x;
    }
}
