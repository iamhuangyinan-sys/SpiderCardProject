using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using Framework;
using Framework.Event;
using Framework.Pool;
using Framework.UI;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 关卡选择面板 —— 生成关卡按钮网格（x=层，y=层内关），选关进入游戏
///
/// 两种模式（同一个面板实例，因为 UIManager 按预制体路径缓存）：
///   选关模式：可点关，箭头指向最后通关的关
///   查看模式：关卡中打开地图，只读，箭头指向当前关卡，多一个关闭按钮
///
/// 连线颜色表示走过的路线：
///   灰 = 还没走过，白 = 已走过（由 LevelStore.chosenLevelIds 的相邻两项还原）
/// 选关时若这一步是配表里的连线，会先把那条线刷白、停留一小段再进入关卡。
///
/// 打开期间一律遮挡场景输入（实现 ISceneInputBlockPanel）：
/// 地图模式下牌桌还在显示，而牌桌走的是 Physics.Raycast 场景拾取，uGUI 遮罩挡不住。
/// </summary>
public partial class LevelPanel : NormalPanel, ISceneInputBlockPanel
{
    /// <summary>打开飞入 / 关闭飞出</summary>
    protected override bool FlyInOnOpen => true;
    protected override bool FlyOutOnClose => true;

    /// <summary>关卡按钮模组预制体路径</summary>
    private const string ModulePrefabPath = "Prefab/UI/GamePlay/Module/LevelBtnModule";

    /// <summary>按钮间距</summary>
    private const float Spacing = 250f;

    /// <summary>路径连线粗细</summary>
    private const float LineThickness = 10f;

    /// <summary>路径连线两头收缩比例</summary>
    private const float LineShrinkRatio = 0.3f;

    /// <summary>未走过的连线颜色（灰）</summary>
    [SerializeField] private Color _lineIdleColor = new Color(1f, 1f, 1f, 0.18f);

    /// <summary>已走过的连线颜色（白）</summary>
    [SerializeField] private Color _lineClearedColor = Color.white;

    /// <summary>连线刷白的渐变时长</summary>
    [SerializeField] private float _lineFadeDuration = 0.15f;

    /// <summary>选关后停留多久再进关卡（让玩家看清路线刷白）</summary>
    [SerializeField] private float _enterDelay = 0.2f;

    private readonly List<LevelBtnModule> _modules = new();

    /// <summary>关卡 id → 按钮局部坐标</summary>
    private readonly Dictionary<string, Vector2> _posMap = new();

    /// <summary>连线（按两端关卡 id 的有序对索引，同一对关卡只存一条）</summary>
    private readonly Dictionary<(string, string), Image> _lineImages = new();

    /// <summary>已生成的路径连线（销毁用）</summary>
    private readonly List<GameObject> _lines = new();

    /// <summary>是否正在「刷白 → 延迟进入」流程中（期间屏蔽点击）</summary>
    private bool _entering;

    /// <summary>延迟进入的协程句柄（面板关闭时要停掉）</summary>
    private Coroutine _enterRoutine;

    /// <summary>
    /// 点击后、真正进关前的「箭头预览位置」：
    /// 停留期间箭头先挪到目标关（此时存档还没写，数据还是旧值），玩家能看到自己从哪走到哪
    /// </summary>
    private string _previewPlayerId;

    /// <summary>查看模式（关卡中打开地图）：所有关卡按钮不可点，只显示当前位置</summary>
    private bool _viewOnly;

    /// <summary>当前是否处于查看模式（地图）</summary>
    public bool IsViewOnly => _viewOnly;

    protected override void OnOpen()
    {
        LevelStore.Instance.OnDataChanged += RefreshLevels;

        if (comps.btnClose != null)
        {
            comps.btnClose.onClick.AddListener(OnClickClose);
            comps.btnClose.gameObject.SetActive(false);   // 只有查看模式才显示
        }

        BuildLevels();
        BuildLines();
        RefreshLevels();
    }

    /// <summary>重新显示时按最新存档刷新（通关后回选关 → 走过的线自动变白）</summary>
    protected override void OnShow()
    {
        _previewPlayerId = null;   // 重新打开时以真实存档为准
        RefreshLevels();
    }

    protected override void OnHide()
    {
        
    }

    protected override void OnClose()
    {
        LevelStore.Instance.OnDataChanged -= RefreshLevels;

        if (comps.btnClose != null) comps.btnClose.onClick.RemoveListener(OnClickClose);

        // 延迟进入还没走完就关面板 → 停掉，避免面板已回收还去调 SelectLevel
        if (_enterRoutine != null)
        {
            MonoManager.Instance.StopCoroutine(_enterRoutine);
            _enterRoutine = null;
        }
        _entering = false;
        _previewPlayerId = null;

        foreach (var m in _modules)
        {
            if (m == null) continue;

            m.OnClickLevel = null;      // 模组会进对象池复用，回调必须清掉
            PoolManager.Instance.Despawn(m.gameObject);
        }
        _modules.Clear();

        ClearLines();
        _posMap.Clear();
    }

    /// <summary>生成所有关卡按钮</summary>
    private void BuildLevels()
    {
        var parent = comps.imgLevelBtns.transform;
        int minLayer = LevelManager.Instance.GetMinLayer();
        int maxLayer = LevelManager.Instance.GetMaxLayer();

        _posMap.Clear();

        for (int layer = minLayer; layer <= maxLayer; layer++)
        {
            if (!LevelStore.Instance.levelsByLayer.TryGetValue(layer, out var list)) continue;

            for (int i = 0; i < list.Count; i++)
            {
                var go = PoolManager.Instance.Spawn(ModulePrefabPath);
                if (go == null) continue;

                var module = go.GetComponent<LevelBtnModule>();
                if (module == null) continue;

                var pos = CalcPosition(layer, i, list.Count, minLayer, maxLayer);

                go.transform.SetParent(parent, false);
                go.transform.localPosition = pos;
                module.OnClickLevel = OnClickModule;   // 选关流程统一由面板处理
                module.Bind(list[i]);

                _modules.Add(module);
                _posMap[list[i].Id] = pos;
            }
        }
    }

    /// <summary>按配表 NextLevelId（分号分隔，可多值）在关卡之间画路径连线</summary>
    private void BuildLines()
    {
        var parent = comps.imgLevelBtns.transform;

        _lineImages.Clear();

        foreach (var cfg in LevelStore.Instance.allLevels.Values)
        {
            if (!_posMap.TryGetValue(cfg.Id, out var start)) continue;

            foreach (var nextId in LevelManager.Instance.GetNextIds(cfg.Id))
            {
                if (!_posMap.TryGetValue(nextId, out var end)) continue;

                var key = MakeEdgeKey(cfg.Id, nextId);
                if (_lineImages.ContainsKey(key)) continue;   // 同一对关卡只画一条

                var line = DrawLineHelper.Draw(parent, start, end, LineThickness, LineShrinkRatio, _lineIdleColor);
                if (line == null) continue;

                _lineImages[key] = line;
                _lines.Add(line.gameObject);
            }
        }
    }

    /// <summary>清掉所有路径连线</summary>
    private void ClearLines()
    {
        foreach (var go in _lines)
        {
            if (go != null) Destroy(go);
        }
        _lines.Clear();
        _lineImages.Clear();
    }

    /// <summary>两端关卡 id 的有序对（作字典 key，保证 A-B 与 B-A 是同一条线）</summary>
    private static (string, string) MakeEdgeKey(string a, string b) =>
        string.CompareOrdinal(a, b) <= 0 ? (a, b) : (b, a);

    /// <summary>网格坐标：x=层（左→右），y=层内关（按该层关卡数上下关于 y=0 对称）</summary>
    private static Vector2 CalcPosition(int layer, int index, int count, int minLayer, int maxLayer)
    {
        float x = (layer - (minLayer + maxLayer) / 2f) * Spacing;
        float y = ((count - 1) / 2f - index) * Spacing;
        return new Vector2(x, y);
    }

    /// <summary>刷新所有按钮状态 + 连线颜色 + 「玩家在这里」箭头</summary>
    private void RefreshLevels()
    {
        foreach (var m in _modules)
        {
            if (m == null) continue;

            m.RefreshState();
        }

        RefreshPlayerArrow();
        RefreshLineColors();

        // 查看模式 / 正在进关的停留期：RefreshState 会把 interactable 复位，这里盖回去
        if (_viewOnly || _entering) SetModulesInteractable(false);
    }

    /// <summary>
    /// 只刷新「玩家在这里」箭头。
    /// 单独拆出来是因为选关前的预览只该动箭头 —— 不能走 RefreshLineColors，
    /// 否则数据还是旧的，会把刚播出去的刷白渐变又按灰色盖回去
    /// </summary>
    private void RefreshPlayerArrow()
    {
        string playerId = _previewPlayerId ?? LevelManager.Instance.GetPlayerLevelId();

        foreach (var m in _modules)
        {
            if (m != null) m.SetPlayerHere(m.LevelId == playerId);
        }
    }

    /// <summary>按「走过的路线」刷新连线颜色：没走过 = 灰，走过 = 白</summary>
    private void RefreshLineColors()
    {
        if (_lineImages.Count == 0) return;

        foreach (var pair in _lineImages)
        {
            var img = pair.Value;
            if (img == null) continue;

            Color target = LevelManager.Instance.HasTraversedEdge(pair.Key.Item1, pair.Key.Item2)
                ? _lineClearedColor
                : _lineIdleColor;

            // 已经是目标色就不动：避免打断点击时正在播的刷白渐变
            if (img.color != target) img.color = target;
        }
    }

    // ==================== 查看模式（关卡中打开地图） ====================

    /// <summary>
    /// 切换模式：true = 查看（只读、显示关闭按钮），false = 选关（可点）。
    /// 面板实例是全局唯一的，切成选关模式时必须复位，否则选关界面会点不动
    /// </summary>
    public void SetViewOnly(bool value)
    {
        _viewOnly = value;

        if (comps.btnClose != null) comps.btnClose.gameObject.SetActive(value);

        RefreshLevels();   // 顺带按当前模式重刷可点状态
    }

    /// <summary>关闭地图（面板生命周期交给 CardGameEntry 统一管）</summary>
    private void OnClickClose()
    {
        EventManager.Instance.Dispatch(E_EventEnum.OnMapClose);
    }

    // ==================== 选关 ====================

    /// <summary>按钮点击 → 箭头挪过去 + 走过的线刷白 → 停留一小段 → 进入关卡</summary>
    private void OnClickModule(LevelConfig cfg)
    {
        if (cfg == null || _entering || _viewOnly) return;

        // 上一步是「最后完成的关卡」→「本次选的关卡」这条线，说明玩家正沿路线往前走
        string fromId = LevelStore.Instance.LastClearedLevelId;
        bool traversing = LevelManager.Instance.IsNextLevel(fromId, cfg.Id);

        _entering = true;

        if (traversing) HighlightLine(fromId, cfg.Id);

        // 箭头先挪到目标关（存档要到 SelectLevel 才写，所以这里用预览值）
        _previewPlayerId = cfg.Id;
        RefreshPlayerArrow();

        SetModulesInteractable(false);      // 停留期间不许再点（下次 OnShow 会按解锁状态重设）
        _enterRoutine = MonoManager.Instance.StartCoroutine(EnterLevelRoutine(cfg.Id));
    }

    /// <summary>
    /// 把一条线刷白（纯显示）。
    /// 存档要到 SelectLevel 里才写，这里先手动亮给玩家看，配合下面的停留时间
    /// </summary>
    private void HighlightLine(string fromId, string toId)
    {
        if (!_lineImages.TryGetValue(MakeEdgeKey(fromId, toId), out var img) || img == null) return;

        img.DOKill();
        img.DOColor(_lineClearedColor, _lineFadeDuration).SetUpdate(true);
    }

    /// <summary>停留一小段时间让玩家看清「箭头挪位 + 路线刷白」，再真正进入关卡</summary>
    private IEnumerator EnterLevelRoutine(string levelId)
    {
        if (_enterDelay > 0f)
        {
            yield return new WaitForSecondsRealtime(_enterDelay);
        }

        _enterRoutine = null;

        LevelManager.Instance.SelectLevel(levelId);

        // SelectLevel 内部会刷数据 → RefreshLevels，所以 _entering 要等它之后再清，
        // 免得面板飞出的这段时间里按钮又变回可点
        _entering = false;

        // 没真的进关（比如非打牌关直接 return）→ 恢复点击，避免面板卡死
        if (IsOpen) SetModulesInteractable(true);
    }

    /// <summary>屏蔽 / 恢复所有关卡按钮的点击</summary>
    private void SetModulesInteractable(bool value)
    {
        foreach (var m in _modules)
        {
            if (m != null && m.comps.btnLevel != null) m.comps.btnLevel.interactable = value;
        }
    }
}
