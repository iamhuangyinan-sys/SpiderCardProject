using System.Collections.Generic;
using Framework.Pool;
using Framework.UI;
using UnityEngine;

/// <summary>
/// 关卡选择面板 —— 生成关卡按钮网格（x=层，y=层内关），选关进入游戏
/// </summary>
public partial class LevelPanel : NormalPanel
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

    private readonly List<LevelBtnModule> _modules = new();

    /// <summary>关卡 id → 按钮局部坐标</summary>
    private readonly Dictionary<string, Vector2> _posMap = new();

    /// <summary>已生成的路径连线</summary>
    private readonly List<GameObject> _lines = new();

    protected override void OnOpen()
    {
        LevelStore.Instance.OnDataChanged += RefreshLevels;
        BuildLevels();
        BuildLines();
        RefreshLevels();
    }

    protected override void OnShow()
    {
        RefreshLevels();
    }

    protected override void OnHide()
    {
        
    }

    protected override void OnClose()
    {
        LevelStore.Instance.OnDataChanged -= RefreshLevels;

        foreach (var m in _modules)
        {
            if (m != null) PoolManager.Instance.Despawn(m.gameObject);
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

        foreach (var cfg in LevelStore.Instance.allLevels.Values)
        {
            if (!_posMap.TryGetValue(cfg.Id, out var start)) continue;

            foreach (var nextId in LevelManager.Instance.GetNextIds(cfg.Id))
            {
                if (!_posMap.TryGetValue(nextId, out var end)) continue;

                var line = DrawLineHelper.Draw(parent, start, end, LineThickness, LineShrinkRatio);
                if (line != null) _lines.Add(line.gameObject);
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
    }

    /// <summary>网格坐标：x=层（左→右），y=层内关（按该层关卡数上下关于 y=0 对称）</summary>
    private static Vector2 CalcPosition(int layer, int index, int count, int minLayer, int maxLayer)
    {
        float x = (layer - (minLayer + maxLayer) / 2f) * Spacing;
        float y = ((count - 1) / 2f - index) * Spacing;
        return new Vector2(x, y);
    }

    /// <summary>刷新所有按钮状态</summary>
    private void RefreshLevels()
    {
        foreach (var m in _modules)
        {
            if (m != null) m.RefreshState();
        }
    }
}
