using System;
using System.Collections.Generic;
using Framework.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 纸牌展示模组 —— 牌面渲染 + 牌 id 记录 + 悬停提示
///
/// 用法：任何需要显示一张牌的地方塞这个模组即可
///   可点击的（商店格）：Bind(id) 后调 SetClickable(回调)
///   不可点击的（展示牌）：只调 Bind(id)，按钮组件会被关掉
///
/// 牌面结构和 3D 牌一致（规则也共用 CardFaceHelper，只是这边画到 uGUI Image 上）：
///   btn_Card          牌底（保持预制体里的图，代码不动它）
///   img_Img           图案
///   img_Suit / img_Rank             主花色 / 主点数
///   img_Suit1~3 / img_Rank1~4       多重花色 / 多重点数的图标槽位
///   img_Attribute1~5                牌属性（沉底等）的图标槽位
///   （图标排布由预制体里的 LayoutGroup 负责，代码只管填图和开关槽位）
/// </summary>
public partial class CardModule : BaseModule, IPointerEnterHandler, IPointerExitHandler
{
    /// <summary>沉底牌只染图案（黄色），花色点数不受影响</summary>
    private static readonly Color SunkColor = new Color(1f, 0.85f, 0.25f, 1f);

    /// <summary>属性图标行的槽位数（预制体 img_Attribute1~5；超出的属性不显示）</summary>
    private const int AttributeSlotCount = 5;

    /// <summary>有多重花色时图案的 y 偏移（给花色图标行让位）</summary>
    [SerializeField] private float imageOffsetYOnMultiSuit = -10f;

    /// <summary>有属性时图案的 y 偏移（给属性图标行让位；优先级高于多重花色）</summary>
    [SerializeField] private float imageOffsetYOnAttribute = -20f;

    /// <summary>悬停提示的首选方向（放不下会自动换方向）</summary>
    [SerializeField] private E_TipDirection tipDirection = E_TipDirection.Down;

    /// <summary>提示框额外让出的距离（像素，默认 0；个别牌想再远一点就在预制体上调）</summary>
    [SerializeField] private float tipExtraGap = 0f;

    /// <summary>当前绑定的牌 id（空 = 未绑定）</summary>
    public string CardId { get; private set; }

    /// <summary>悬停状态变化（true = 进入 / false = 离开）—— 宿主用来做整体缩放等表现</summary>
    public Action<bool> OnHoverChanged;

    /// <summary>点击回调（null = 不响应点击）</summary>
    private Action _onClick;

    /// <summary>宿主的悬停缩放（用来把“放大后的尺寸”算进提示框间距；没有就按 1 倍算）</summary>
    private HoverScaleBehaviour _hoverScale;

    /// <summary>当前是否处于悬停中（只在变化时广播，避免重复驱动缩放）</summary>
    private bool _hovering;

    /// <summary>图案节点的初始位置（只改 y，x / 其它保持预制体设置）</summary>
    private Vector2 _imgFaceBasePos;

    /// <summary>当前牌的属性（tip 与图标都用；Bind 时整体拷贝，外部改 List 不影响这里）</summary>
    private readonly List<E_CardAttributeEnum> _attributes = new();

    /// <summary>属性图标的临时列表（避免每次刷新都 new）</summary>
    private readonly List<Sprite> _attributeSprites = new();

    protected override void OnBind()
    {
        SetClickable(null);   // 默认不可点击，需要点击的宿主自己开启
        _hoverScale = GetComponentInParent<HoverScaleBehaviour>();

        if (comps.imgImg != null)
        {
            _imgFaceBasePos = comps.imgImg.rectTransform.anchoredPosition;
        }
    }

    protected override void OnUnBind()
    {
        NotifyHover(false);
        HideTip();
        Clear();
    }

    // ==================== 对外 ====================

    /// <summary>绑定一张牌：记录 id / 属性 + 刷新牌面</summary>
    /// <param name="cardId">牌 id（编码 id）</param>
    /// <param name="attributes">牌的属性（null / 空 = 没有属性；会拷贝一份，外部改动不影响这里）</param>
    public void Bind(string cardId, IReadOnlyList<E_CardAttributeEnum> attributes = null)
    {
        CardId = cardId;

        _attributes.Clear();
        if (attributes != null) _attributes.AddRange(attributes);

        RefreshFace();
    }

    /// <summary>清空显示与点击（item 回收时调）</summary>
    public void Clear()
    {
        CardId = null;
        _attributes.Clear();
        SetClickable(null);
        RefreshFace();
    }

    /// <summary>设置点击回调；传 null 表示不可点击（按钮组件会被直接关掉）</summary>
    public void SetClickable(Action onClick)
    {
        _onClick = onClick;

        if (comps.btnCard == null) return;

        comps.btnCard.onClick.RemoveListener(OnClickCard);
        comps.btnCard.enabled = onClick != null;

        if (onClick != null)
        {
            comps.btnCard.onClick.AddListener(OnClickCard);
        }
    }

    /// <summary>收起提示（只有自己的提示才会被关）</summary>
    public void HideTip()
    {
        HoverTipHelper.Hide(this);
    }

    // ==================== 悬停 ====================

    public void OnPointerEnter(PointerEventData eventData)
    {
        NotifyHover(true);
        ShowTip();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        NotifyHover(false);
        HideTip();
    }

    /// <summary>被失活（列表回收 / 面板关闭）时收起提示，避免提示残留在屏幕上</summary>
    private void OnDisable()
    {
        NotifyHover(false);   // 失活时补一次“离开”，宿主正好把缩放收掉
        HideTip();
    }

    /// <summary>广播悬停状态（只在变化时发）</summary>
    private void NotifyHover(bool hovering)
    {
        if (_hovering == hovering) return;

        _hovering = hovering;
        OnHoverChanged?.Invoke(hovering);
    }

    private void ShowTip()
    {
        // 描述由牌的属性推导（可能多条；没有可说的属性时 helper 会直接收起）
        var tips = CardTipHelper.Build(CardId, _attributes);

        // 对齐目标用「可见牌面」：模组根节点的 rect 不一定等于牌的实际大小
        var target = comps.btnCard != null && comps.btnCard.image != null
            ? comps.btnCard.image.rectTransform
            : (RectTransform)transform;

        // 宿主悬停时会整体放大 → 让提示框按「放大后的尺寸」定位，间距才不会被放大吃掉
        float targetScale = _hoverScale != null ? _hoverScale.HoverScale : 1f;

        HoverTipHelper.Show(this, tips, target, tipDirection, targetScale, tipExtraGap);
    }

    // ==================== 内部 ====================

    /// <summary>
    /// 按当前 CardId / 属性刷新牌面（无牌时全部清空）。
    /// 内容和 3D 牌同一套计算（CardFaceHelper），只是画到 uGUI Image 上；
    /// 底板 btn_Card 保持预制体里的牌底，代码不碰
    /// </summary>
    private void RefreshFace()
    {
        var face = string.IsNullOrEmpty(CardId) ? null : CardFaceHelper.Build(CardId);

        SetImage(comps.imgImg, face?.Face);
        SetImage(comps.imgSuit, face?.MainSuit);
        SetImage(comps.imgRank, face?.MainRank);

        SetSlots(face?.ExtraSuits, comps.imgSuit1, comps.imgSuit2, comps.imgSuit3);
        SetSlots(face?.ExtraRanks, comps.imgRank1, comps.imgRank2, comps.imgRank3, comps.imgRank4);

        // 属性图标行（槽位固定 5 个，多出来的属性不显示）
        SetSlots(BuildAttributeSprites(), comps.imgAttribute1, comps.imgAttribute2,
                 comps.imgAttribute3, comps.imgAttribute4, comps.imgAttribute5);

        // 图案下沉：有属性让出属性行，否则多重花色让出花色行
        SetFaceOffsetY(CalcFaceOffsetY(face));

        // 沉底：只染图案
        if (comps.imgImg != null)
        {
            comps.imgImg.color = HasAttribute(E_CardAttributeEnum.Sunk) ? SunkColor : Color.white;
        }
    }

    /// <summary>属性 → 图标（查表 / 加载在 CardResManager 里；没图跳过，不留空槽）</summary>
    private List<Sprite> BuildAttributeSprites()
    {
        _attributeSprites.Clear();

        for (int i = 0; i < _attributes.Count && i < AttributeSlotCount; i++)
        {
            var sprite = CardResManager.Instance.GetAttributeSprite((int)_attributes[i]);
            if (sprite != null) _attributeSprites.Add(sprite);
        }

        return _attributeSprites;
    }

    /// <summary>
    /// 图案下沉规则：有属性 → 让出属性行（优先级最高）；
    /// 否则有多重花色 → 让出花色行；都没有就不动
    /// </summary>
    private float CalcFaceOffsetY(CardFace face)
    {
        if (_attributes.Count > 0) return imageOffsetYOnAttribute;
        if (face != null && face.HasMultiSuit) return imageOffsetYOnMultiSuit;

        return 0f;
    }

    /// <summary>当前牌有没有某个属性</summary>
    private bool HasAttribute(E_CardAttributeEnum attribute)
    {
        return _attributes.Contains(attribute);
    }

    /// <summary>设图并按有无图开关节点（图标槽位在 LayoutGroup 里，必须用 SetActive 占/不占位）</summary>
    private static void SetImage(Image img, Sprite sprite)
    {
        if (img == null) return;

        img.sprite = sprite;
        img.gameObject.SetActive(sprite != null);
    }

    /// <summary>给图标槽位依次填图，多出来的槽位关掉</summary>
    private static void SetSlots(IReadOnlyList<Sprite> sprites, params Image[] slots)
    {
        int count = sprites?.Count ?? 0;

        for (int i = 0; i < slots.Length; i++)
        {
            var slot = slots[i];
            if (slot == null) continue;

            bool used = i < count && sprites[i] != null;

            slot.sprite = used ? sprites[i] : null;
            slot.gameObject.SetActive(used);
        }
    }

    /// <summary>只改图案节点的 y（基准位置在 OnBind 里从预制体上记下来）</summary>
    private void SetFaceOffsetY(float offset)
    {
        if (comps.imgImg == null) return;

        var rect = comps.imgImg.rectTransform;
        var pos = _imgFaceBasePos;
        pos.y += offset;

        if (rect.anchoredPosition != pos)
        {
            rect.anchoredPosition = pos;
        }
    }

    private void OnClickCard()
    {
        _onClick?.Invoke();
    }
}
