/// <summary>集中调参入口</summary>
public static class LevelGenerationSettings
{
    public const int Version = 12;
    public const int ChapterCount = 3;
    public const int LayerCount = 7;
    public const int FirstEventId = 11001;
    public const int BossEventId = 11002;
    public const int CandidateCount = 160;
    public const int FirstTarget = 55;
    public static readonly (int Start, int End)[] NormalTargets = { (90, 122), (132, 172), (194, 236) };
    public static readonly int[] BossChapterTargetBonuses = { 0, 12, 24 };
    public static readonly int[] ChapterRewardBonuses = { 0, 10, 20 };
    public static readonly int[] Difficulty3TargetBonuses = { 8, 14, 22 };
    public const int Difficulty3ExtraDealCards = 6;
    public const int FirstScoreLimit = 80;
    public const int MinimumColumns = 10;
    public const int MaximumColumns = 13;
    public const int MinimumPockets = 1;
    public const int MaximumPockets = 3;
    public static readonly (int Minimum, int Maximum)[] ChapterStraightGoals = { (2, 5), (4, 7), (7, 10) };
    public const int BossTarget = 160;
    public const int BossMinimumGap = 12;
    public const float DifficultyTolerance = 10;
    public const float ColumnPressure = 12;
    public const float PocketPressure = 18;
    public const float HiddenPressure = 15;
    public const float ShortColumnPressure = 4;
    public const float FirstEventModifier = -8;
    public const float BossEventModifier = 8;
    public static readonly int[] GoalPressure = { 0, -25, -12, 0, 15, 33, 51, 69, 87, 105, 123 };
    // 从本大关接龙目标下限开始，依次对应四个候选目标。
    public static readonly int[] EarlyGoalWeights = { 60, 30, 8, 2 };
    public static readonly int[] MiddleGoalWeights = { 20, 50, 25, 5 };
    public static readonly int[] LateGoalWeights = { 5, 35, 40, 20 };
    public static readonly int[] TierWeights = { 25, 55, 20 };
    public static readonly int[][] TierOffsets = { new[] { -10, 0, 10 }, new[] { -15, 0, 20 }, new[] { -20, 0, 35 } };
    public static readonly int[] GuaranteedShopLayerWeights = { 20, 25, 25, 35 }; // 第 2～5 层
    public const double ShopChance = 0.25;
    public const double LateShopChance = 0.90;
    public const int MaxShopsPerPath = 2;
    public const double RewardJitter = 0.08;
    public const double Difficulty4RewardMultiplier = 0.85;
    public const int FirstMinimumReward = 120;
    public const int BossBonus = 80;
}
