using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>可重复运行的生成/序列化检查；不进入游戏，不读写玩家存档。</summary>
public static class LevelGenerationChecks
{
    [MenuItem("Tools/Validate Random Levels")]
    public static void Run()
    {
        if (Application.isPlaying) throw new InvalidOperationException("请退出 Play 模式后运行检查");
        var sums = new double[8];
        var counts = new int[8];
        int preBossShopCount = 0;
        // 原先字段由 LevelConfig 继承；脱离继承后仍需按相同 JSON 字段恢复。
        var savedNode = JsonUtility.FromJson<RunLevelData>("{\"Id\":\"01010101\",\"LevelType\":2,\"FinishReward\":250,\"ColumnNum\":11,\"PocketNum\":2,\"NextLevelId\":\"01020101\",\"NeedStraightNum\":4,\"LevelEvent\":11002,\"initialCards\":[2,5],\"difficulty\":123}");
        Require(savedNode.Id == "01010101" && savedNode.LevelType == 2 && savedNode.FinishReward == 250
            && savedNode.ColumnNum == 11 && savedNode.PocketNum == 2 && savedNode.NextLevelId == "01020101"
            && savedNode.NeedStraightNum == 4 && savedNode.LevelEvent == 11002
            && savedNode.initialCards.SequenceEqual(new[] { 2, 5 }) && savedNode.difficulty == 123, "已有存档字段恢复失败");
        for (int seed = 0; seed < 10000; seed++)
        {
            var map = LevelGenerationHelper.Generate(seed, 52 + seed % 2);
            Require(LevelGenerationHelper.Validate(map, out _), $"地图校验失败 seed={seed}");
            CheckConnections(map);
            var preBoss = map.levels.Where(n => LevelManager.GetLayer(n.Id) == LevelGenerationSettings.LayerCount - 1).ToList();
            var preBossShop = preBoss.SingleOrDefault(n => n.LevelType == (int)E_LevelTypeEnum.Shop);
            if (preBossShop != null)
            {
                preBossShopCount++;
                Require(map.levels.Any(parent => LevelManager.GetLayer(parent.Id) == LevelGenerationSettings.LayerCount - 2
                    && parent.NextLevelId.Split(';').Contains(preBossShop.Id)
                    && parent.NextLevelId.Split(';').Any(id => preBoss.Any(n => n.Id == id && n.LevelType == (int)E_LevelTypeEnum.Normal))),
                    "Boss 前商店没有战斗替代选项");
            }
            Require(map.levels[0].LevelEvent == LevelGenerationSettings.FirstEventId && map.levels.Last().LevelEvent == LevelGenerationSettings.BossEventId, "事件改变");
            Require(map.levels.Last().difficulty > map.levels.Where(n => n.LevelType == 0).Max(n => n.difficulty), "Boss 难度不足");
            foreach (var n in map.levels.Where(n => n.LevelType != 1))
            {
                Require(n.initialCards.Sum() <= 52 + seed % 2, "初始发牌超预算");
                Require(n.initialCards.SequenceEqual(n.initialCards.OrderBy(x => x)), "初始发牌未排序");
                int layer = LevelManager.GetLayer(n.Id);
                sums[layer] += n.difficulty;
                counts[layer]++;
            }
            if (seed >= 20 && seed != 2408) continue; // 覆盖需要重试 Boss 约束的回归种子。
            string json = JsonUtility.ToJson(map);
            Require(json == JsonUtility.ToJson(LevelGenerationHelper.Generate(seed, 52 + seed % 2)), "种子不确定");
            var save = new RunSaveData { map = map, selectedLevelId = map.levels[0].Id };
            save.chosenLevelIds.Add(save.selectedLevelId);
            save.map.levels[0].actualInitialCards = LevelGenerationHelper.FitDeal(save.map.levels[0].initialCards, save.map.levels[0].ColumnNum, 30);
            var restored = JsonUtility.FromJson<RunSaveData>(JsonUtility.ToJson(save));
            Require(JsonUtility.ToJson(save) == JsonUtility.ToJson(restored), "Unity 存档往返丢字段");
            Require(LevelGenerationHelper.Validate(restored.map, out _), "Unity 地图恢复失败");
        }
        Require(preBossShopCount >= 6000, $"Boss 前商店出现率过低: {preBossShopCount}/10000");
        for (int available = 0; available < 60; available++)
        {
            var deal = LevelGenerationHelper.FitDeal(null, 13, available);
            Require(deal.Sum() <= available && deal.All(x => x >= 0), "配额不足处理失败");
            Require(deal.SequenceEqual(deal.OrderBy(x => x)), "实际发牌未排序");
            Require(available < 13 || deal.All(x => x >= 1), "产生意外空列");
        }
        // 真正走 Manager 的地图导入、当前关卡及路线恢复，不保存到磁盘。
        LevelStore.Instance.Init();
        LevelManager.Instance.Init();
        try
        {
            var map = LevelGenerationHelper.Generate(42, 52);
            var firstId = map.levels[0].Id;
            var nextId = map.levels[0].NextLevelId.Split(';')[0];
            var save = new RunSaveData { map = map, selectedLevelId = nextId };
            save.chosenLevelIds.Add(firstId);
            save.chosenLevelIds.Add(nextId);
            LevelManager.Instance.ImportFrom(save);
            Require(LevelStore.Instance.IsUnlocked(nextId) && LevelStore.Instance.currentLayer == 2, "路线恢复失败");
            var exported = new RunSaveData();
            LevelManager.Instance.ExportTo(exported);
            Require(JsonUtility.ToJson(exported.map) == JsonUtility.ToJson(map), "Manager 导出改变地图");
            Require(!LevelGenerationHelper.Validate(null, out _), "缺少地图的存档不能续玩");
        }
        finally
        {
            LevelManager.Instance.Dispose();
            LevelStore.Instance.Dispose();
        }
        Debug.Log($"RandomLevels PASS: 10000 seeds, pre-boss shops={preBossShopCount}, Unity JSON, progression, deal budgets. "
            + string.Join(", ", Enumerable.Range(1, 7).Select(i => $"L{i}={sums[i] / counts[i]:F1}")));
    }

    private static void Require(bool success, string message)
    {
        if (!success) throw new InvalidOperationException(message);
    }

    private static void CheckConnections(RunMapData map)
    {
        var layers = map.levels.GroupBy(n => LevelManager.GetLayer(n.Id))
            .ToDictionary(g => g.Key, g => g.OrderBy(n => LevelManager.GetLayerIndex(n.Id)).ToList());
        foreach (var pair in layers)
        {
            if (!layers.TryGetValue(pair.Key + 1, out var next)) continue;
            var edges = pair.Value.SelectMany((node, index) => node.NextLevelId.Split(';')
                .Select(id => (from: index, to: next.FindIndex(n => n.Id == id)))).ToList();
            foreach (var edge in edges)
            {
                Require(edge.to >= 0, "连线目标不存在");
                float fromY = (pair.Value.Count - 1) / 2f - edge.from;
                float toY = (next.Count - 1) / 2f - edge.to;
                Require(Math.Abs(fromY - toY) <= 1, $"连线跨行过大 seed={map.seed}");
                Require(edges.All(other => (edge.from - other.from) * (edge.to - other.to) >= 0), "连线交叉");
            }
        }
    }
}
