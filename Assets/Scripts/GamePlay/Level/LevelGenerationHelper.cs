using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>纯数据生成器：不读取 Store，相同输入生成相同地图。</summary>
public static class LevelGenerationHelper
{
    public static RunMapData Generate(int seed, int deckCount, int difficulty = 1)
    {
        if (deckCount < 13) throw new ArgumentException("初始牌组不足 13 张");
        // 路线汇合后可能没有同时满足全部前置关卡约束的 Boss 参数，有限重试地图结构。
        // 重试种子只由原种子和次数决定，输入相同仍会生成完全相同的地图。
        InvalidOperationException lastError = null;
        for (int attempt = 0; attempt < 16; attempt++)
        {
            try { return GenerateAttempt(seed, deckCount, attempt, difficulty); }
            catch (InvalidOperationException error) { lastError = error; }
        }
        throw new InvalidOperationException("地图生成重试后仍无合法组合，请检查生成参数", lastError);
    }

    private static RunMapData GenerateAttempt(int seed, int deckCount, int attempt, int difficulty)
    {
        var random = new Random(attempt == 0 ? seed : DeriveSeed(seed, attempt, 3));
        var map = new RunMapData { seed = seed, generatorVersion = LevelGenerationSettings.Version };
        int shopLayer = 2 + Pick(random, LevelGenerationSettings.GuaranteedShopLayerWeights);
        var layers = new List<List<RunLevelData>>();
        for (int layer = 1; layer <= LevelGenerationSettings.LayerCount; layer++)
        {
            // 保底商店前汇合，下一层可选商店或战斗；其它层仍有随机分叉。
            int count = layer == 1 || layer == LevelGenerationSettings.LayerCount || layer == shopLayer - 1
                ? 1 : random.Next(2, 4);
            var nodes = new List<RunLevelData>();
            for (int index = 1; index <= count; index++)
            {
                var node = new RunLevelData { Id = $"01{layer:00}{index:00}01", NextLevelId = "" };
                nodes.Add(node);
                map.levels.Add(node);
            }
            layers.Add(nodes);
        }
        for (int layer = 0; layer < layers.Count - 1; layer++)
            Connect(layers[layer], layers[layer + 1], random);

        layers[shopLayer - 1][random.Next(layers[shopLayer - 1].Count)].LevelType = (int)E_LevelTypeEnum.Shop;
        // 先尝试 Boss 前商店，避免普通层随机商店抢占连续商店及路线数量限制。
        int preBossLayer = layers.Count - 1;
        if (random.NextDouble() < LevelGenerationSettings.LateShopChance)
            TryPlaceShop(map, layers[preBossLayer - 1], random, requireAlternative: true);

        for (int layer = 2; layer < layers.Count; layer++)
        {
            if (layer == shopLayer || layer == preBossLayer) continue;
            if (random.NextDouble() < LevelGenerationSettings.ShopChance)
                TryPlaceShop(map, layers[layer - 1], random, requireAlternative: false);
        }

        float maxNormal = 0;
        float targetBonus = difficulty >= 3 ? LevelGenerationSettings.Difficulty3TargetBonus : 0;
        int extraDealCards = difficulty >= 3 ? LevelGenerationSettings.Difficulty3ExtraDealCards : 0;
        foreach (var node in map.levels)
        {
            int layer = Layer(node);
            if (node.LevelType == (int)E_LevelTypeEnum.Shop) continue;
            bool isFirst = layer == 1;
            bool isBoss = layer == layers.Count;
            node.LevelType = isBoss ? (int)E_LevelTypeEnum.Boss : (int)E_LevelTypeEnum.Normal;
            node.LevelEvent = isFirst ? LevelGenerationSettings.FirstEventId : isBoss ? LevelGenerationSettings.BossEventId : 0;
            var nodeRandom = new Random(DeriveSeed(seed, layer * 100 + Index(node), 1));
            float progress = (float)(layer - 2) / Math.Max(1, layers.Count - 3);
            float target = isFirst ? LevelGenerationSettings.FirstTarget + targetBonus : isBoss
                ? Math.Max(LevelGenerationSettings.BossTarget + targetBonus, maxNormal + LevelGenerationSettings.BossMinimumGap)
                : LevelGenerationSettings.NormalStartTarget
                    + progress * (LevelGenerationSettings.NormalEndTarget - LevelGenerationSettings.NormalStartTarget)
                    + targetBonus
                    + LevelGenerationSettings.TierOffsets[Pick(nodeRandom, LevelGenerationSettings.TierWeights)];
            FillCombat(map, node, nodeRandom, deckCount, target, isFirst, isBoss, maxNormal,
                progress, LevelGenerationSettings.FirstScoreLimit + targetBonus, extraDealCards);
            if (!isFirst && !isBoss) maxNormal = Math.Max(maxNormal, node.difficulty);
            var rewards = new Random(DeriveSeed(seed, layer * 100 + Index(node), 2));
            double reward = Math.Max(80, Math.Min(190, 100 + 1.5 * (node.difficulty - 90)));
            if (isBoss) reward += LevelGenerationSettings.BossBonus;
            reward *= 1 + (rewards.NextDouble() * 2 - 1) * LevelGenerationSettings.RewardJitter;
            node.FinishReward = (int)Math.Round(reward / 5) * 5;
            if (isFirst) node.FinishReward = Math.Max(LevelGenerationSettings.FirstMinimumReward, node.FinishReward);
            if (difficulty >= 4)
                node.FinishReward = (int)Math.Round(node.FinishReward * LevelGenerationSettings.Difficulty4RewardMultiplier / 5) * 5;
        }
        if (!Validate(map, out string error)) throw new InvalidOperationException(error);
        return map;
    }

    private static bool TryPlaceShop(RunMapData map, List<RunLevelData> layer, Random random, bool requireAlternative)
    {
        if (layer.Count < 2 || layer.Any(n => n.LevelType == (int)E_LevelTypeEnum.Shop)) return false;
        var candidates = layer.OrderBy(_ => random.Next()).ToList();
        foreach (var node in candidates)
        {
            // 至少存在一个前置节点能同时选择商店和普通战斗。
            if (requireAlternative && !map.levels.Any(parent => Next(parent).Contains(node.Id)
                && Next(parent).Any(id => id != node.Id && layer.Any(n => n.Id == id && n.LevelType == (int)E_LevelTypeEnum.Normal)))) continue;
            node.LevelType = (int)E_LevelTypeEnum.Shop;
            if (ValidShopPaths(map)) return true;
            node.LevelType = (int)E_LevelTypeEnum.Normal;
        }
        return false;
    }

    // 有序的阶梯连线：覆盖双方所有节点且不交叉，按居中后的实际行位置限制跨度。
    private static void Connect(List<RunLevelData> from, List<RunLevelData> to, Random random)
    {
        int a = 0, b = 0;
        while (true)
        {
            from[a].NextLevelId += (from[a].NextLevelId.Length == 0 ? "" : ";") + to[b].Id;
            if (a == from.Count - 1 && b == to.Count - 1) break;
            bool canAdvanceFrom = a + 1 < from.Count && CanConnect(a + 1, from.Count, b, to.Count);
            bool canAdvanceTo = b + 1 < to.Count && CanConnect(a, from.Count, b + 1, to.Count);
            if (canAdvanceFrom && canAdvanceTo)
            {
                if (random.Next(2) == 0) a++;
                else b++;
            }
            else if (canAdvanceFrom) a++;
            else if (canAdvanceTo) b++;
            else throw new InvalidOperationException("相邻层节点数量无法在一个节点间距内连接");
        }
    }

    /// <summary>与地图 UI 共用居中行坐标，单位为一个节点间距。</summary>
    public static float GetRowOffset(int index, int count) => (count - 1) / 2f - index;

    private static bool CanConnect(int fromIndex, int fromCount, int toIndex, int toCount) =>
        Math.Abs(GetRowOffset(fromIndex, fromCount) - GetRowOffset(toIndex, toCount)) <= 1f;

    private static void FillCombat(RunMapData map, RunLevelData node, Random random, int deckCount,
        float target, bool first, bool boss, float maxNormal, float progress, float firstScoreLimit, int extraDealCards)
    {
        var previous = PreviousCombats(map, node).ToList();
        var candidates = new List<RunLevelData>();
        var weights = new List<double>();
        bool early = progress <= 0.25f;
        bool late = progress > 0.5f;
        // 枚举目标/列/槽，避免随机重试碰不到合法组合；发牌轮廓做有限采样。
        for (int c = first ? LevelGenerationSettings.MaximumColumns : LevelGenerationSettings.MinimumColumns;
             c <= LevelGenerationSettings.MaximumColumns; c++)
        for (int p = first ? LevelGenerationSettings.MaximumPockets : LevelGenerationSettings.MinimumPockets;
             p <= LevelGenerationSettings.MaximumPockets; p++)
        for (int goal = first ? 1 : boss ? 4 : 2; goal <= (first ? 2 : boss ? LevelGenerationSettings.MaximumStraightGoal : 4); goal++)
        {
            if (previous.Any(n => Math.Abs(n.ColumnNum - c) > 1 || Math.Abs(n.PocketNum - p) > 1)) continue;
            int goalWeight = first ? (goal == 1 ? 30 : 70) : boss ? (goal == 4 ? 70 : 30)
                : (early ? LevelGenerationSettings.EarlyGoalWeights : late
                    ? LevelGenerationSettings.LateGoalWeights : LevelGenerationSettings.MiddleGoalWeights)[goal - 2];
            if (goalWeight == 0) continue;
            for (int sample = 0; sample < LevelGenerationSettings.CandidateCount / 10; sample++)
            {
                int shorts = first ? random.Next(5, 8) : random.Next(3, 7);
                var candidate = new RunLevelData { ColumnNum = c, PocketNum = p, NeedStraightNum = goal };
                for (int col = 0; col < c; col++)
                    candidate.initialCards.Add(col < shorts ? 2 : random.Next(first ? 3 : 4, first ? 5 : 6));
                if (extraDealCards > 0)
                {
                    int extra = random.Next(extraDealCards + 1);
                    for (int card = 0; card < extra; card++)
                        candidate.initialCards[random.Next(shorts, c)]++;
                }
                // 初始生成不依赖事件加牌，留给事件原有流程处理。
                candidate.initialCards = FitDeal(candidate.initialCards, c, deckCount);
                candidate.difficulty = Score(candidate, first, boss);
                if (first && candidate.difficulty > firstScoreLimit) continue;
                if (boss && candidate.difficulty < maxNormal + LevelGenerationSettings.BossMinimumGap) continue;
                double distance = (candidate.difficulty - target) / LevelGenerationSettings.DifficultyTolerance;
                int baseColumn = !first && !early && (boss || late)
                    ? LevelGenerationSettings.MinimumColumns : LevelGenerationSettings.MaximumColumns;
                int basePocket = !first && (boss || late)
                    ? LevelGenerationSettings.MinimumPockets : LevelGenerationSettings.MaximumPockets;
                double weight = goalWeight * (c == baseColumn ? 0.7 : 0.15) * (p == basePocket ? 0.7 : 0.15)
                    * Math.Exp(-0.5 * distance * distance);
                candidates.Add(candidate);
                weights.Add(weight);
            }
        }
        if (candidates.Count == 0) throw new InvalidOperationException($"关卡 {node.Id} 无合法组合，请检查生成参数");
        double ticket = random.NextDouble() * weights.Sum();
        int selected = weights.Count - 1;
        for (int i = 0; i < weights.Count; i++)
        {
            ticket -= weights[i];
            if (ticket <= 0) { selected = i; break; }
        }
        var chosen = candidates[selected];
        node.ColumnNum = chosen.ColumnNum;
        node.PocketNum = chosen.PocketNum;
        node.NeedStraightNum = chosen.NeedStraightNum;
        node.initialCards = chosen.initialCards;
        node.difficulty = chosen.difficulty;
        node.initialCards.Sort();
    }

    public static float Score(RunLevelData node, bool first, bool boss)
    {
        float hidden = (float)node.initialCards.Sum() / node.ColumnNum - 1;
        int shorts = node.initialCards.Count(n => n <= 2);
        return 100 + LevelGenerationSettings.ColumnPressure * (12 - node.ColumnNum)
            + LevelGenerationSettings.PocketPressure * (3 - node.PocketNum)
            + LevelGenerationSettings.HiddenPressure * (hidden - 2.5f)
            + LevelGenerationSettings.ShortColumnPressure * (5 - shorts)
            + LevelGenerationSettings.GoalPressure[node.NeedStraightNum]
            + (node.NeedStraightNum >= 4 && node.PocketNum == 2 ? 6 : 0)
            + (first ? LevelGenerationSettings.FirstEventModifier : boss ? LevelGenerationSettings.BossEventModifier : 0);
    }

    /// <summary>删牌后牌数不足时确定性削减深列；极端小牌组也不会发出未翻开的半列。</summary>
    public static List<int> FitDeal(IReadOnlyList<int> plan, int columns, int available)
    {
        var result = new List<int>();
        for (int i = 0; i < columns; i++)
            result.Add(plan != null && plan.Count == columns ? Math.Max(1, plan[i]) : i < 5 ? 2 : 5);
        int total = result.Sum();
        while (total > Math.Max(0, available))
        {
            int deepest = 0;
            for (int i = 1; i < result.Count; i++) if (result[i] > result[deepest]) deepest = i;
            result[deepest]--;
            total--;
        }
        result.Sort();
        return result;
    }

    private static IEnumerable<RunLevelData> PreviousCombats(RunMapData map, RunLevelData node)
    {
        foreach (var parent in map.levels.Where(n => Next(n).Contains(node.Id)))
        {
            if (parent.LevelType != (int)E_LevelTypeEnum.Shop) yield return parent;
            else foreach (var ancestor in PreviousCombats(map, parent)) yield return ancestor;
        }
    }

    public static bool Validate(RunMapData map, out string error)
    {
        error = "无效的本局地图";
        if (map?.levels == null || map.levels.Count == 0) return false;
        var ids = new HashSet<string>();
        foreach (var n in map.levels)
        {
            if (n == null || n.Id == null || n.Id.Length != 8 || !int.TryParse(n.Id, out _) || !ids.Add(n.Id)) return false;
        }
        var sorted = map.levels.OrderBy(Layer).ToList();
        int lastLayer = Layer(sorted[sorted.Count - 1]);
        if (sorted.Count(n => Layer(n) == 1) != 1 || sorted.Count(n => Layer(n) == lastLayer) != 1) return false;
        if (sorted[0].LevelType != (int)E_LevelTypeEnum.Normal || sorted[sorted.Count - 1].LevelType != (int)E_LevelTypeEnum.Boss) return false;
        var reached = new HashSet<string> { sorted[0].Id };
        foreach (var n in sorted)
        {
            if (!reached.Contains(n.Id)) return false;
            var next = Next(n);
            if ((Layer(n) == lastLayer) != (next.Length == 0)) return false;
            foreach (string id in next)
            {
                var child = sorted.Find(x => x.Id == id);
                if (child == null || Layer(child) != Layer(n) + 1) return false;
                reached.Add(id);
            }
            if (n.LevelType == (int)E_LevelTypeEnum.Shop) continue;
            if (n.ColumnNum < 1 || n.PocketNum < 0 || n.NeedStraightNum < 1 || n.FinishReward < 0
                || n.initialCards == null || n.initialCards.Count != n.ColumnNum || n.initialCards.Any(x => x < 1)) return false;
            if (PreviousCombats(map, n).Any(p => Math.Abs(p.ColumnNum - n.ColumnNum) > 1 || Math.Abs(p.PocketNum - n.PocketNum) > 1)) return false;
        }
        if (!ValidShopPaths(map)) return false;
        error = null;
        return true;
    }

    private static bool ValidShopPaths(RunMapData map)
    {
        return Visit(map.levels[0], 0, false);
        bool Visit(RunLevelData node, int shops, bool hadOpportunity)
        {
            bool shop = node.LevelType == (int)E_LevelTypeEnum.Shop;
            if (shop) shops++;
            if (shops > LevelGenerationSettings.MaxShopsPerPath) return false;
            var children = Next(node).Select(id => map.levels.Find(n => n.Id == id)).ToList();
            if (children.Count == 0) return hadOpportunity;
            bool opportunity = hadOpportunity || children.Any(n => n.LevelType == (int)E_LevelTypeEnum.Shop);
            return children.All(n => !(shop && n.LevelType == (int)E_LevelTypeEnum.Shop) && Visit(n, shops, opportunity));
        }
    }

    private static int Pick(Random random, int[] weights)
    {
        int ticket = random.Next(weights.Sum());
        for (int i = 0; i < weights.Length; i++) if ((ticket -= weights[i]) < 0) return i;
        return weights.Length - 1;
    }

    private static int DeriveSeed(int seed, int node, int stream)
    {
        unchecked { return (seed * 397) ^ (node * 7919) ^ (stream * 104729); }
    }
    private static int Layer(RunLevelData n) => int.Parse(n.Id.Substring(2, 2));
    private static int Index(RunLevelData n) => int.Parse(n.Id.Substring(4, 2));
    private static string[] Next(RunLevelData n) => (n.NextLevelId ?? "").Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
}
