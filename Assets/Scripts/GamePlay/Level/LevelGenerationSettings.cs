/// <summary>试玩版集中调参入口。稳定后可迁入 Excel；修改只影响新局。</summary>
public static class LevelGenerationSettings
{
    public const int Version = 8;
    public const int LayerCount = 7;
    public const int FirstEventId = 11001;
    public const int BossEventId = 11002;
    public const int CandidateCount = 160;
    public const float FirstTarget = 55;
    public const float NormalStartTarget = 90;
    public const float NormalEndTarget = 122;
    public const float Difficulty3TargetBonus = 8;
    public const int Difficulty3ExtraDealCards = 6;
    public const float FirstScoreLimit = 80;
    public const int MinimumColumns = 10;
    public const int MaximumColumns = 13;
    public const int MinimumPockets = 1;
    public const int MaximumPockets = 3;
    public const int MaximumStraightGoal = 5;
    public const float BossTarget = 160;
    public const float BossMinimumGap = 12;
    public const float DifficultyTolerance = 10;
    public const float ColumnPressure = 12;
    public const float PocketPressure = 18;
    public const float HiddenPressure = 15;
    public const float ShortColumnPressure = 4;
    public const float FirstEventModifier = -8;
    public const float BossEventModifier = 8;
    public static readonly int[] GoalPressure = { 0, -25, -12, 0, 15, 33 };
    public static readonly int[] EarlyGoalWeights = { 60, 40, 0 };
    public static readonly int[] MiddleGoalWeights = { 20, 70, 10 };
    public static readonly int[] LateGoalWeights = { 0, 65, 35 };
    public static readonly int[] TierWeights = { 25, 55, 20 };
    public static readonly int[] TierOffsets = { -10, 0, 10 };
    public static readonly int[] GuaranteedShopLayerWeights = { 20, 25, 25, 35 }; // 第 2～5 层
    public const double ShopChance = 0.25;
    public const double LateShopChance = 0.90;
    public const int MaxShopsPerPath = 2;
    public const double RewardJitter = 0.08;
    public const double Difficulty4RewardMultiplier = 0.85;
    public const int FirstMinimumReward = 120;
    public const int BossBonus = 80;
}
