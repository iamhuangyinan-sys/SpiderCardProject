using System;
using System.Collections.Generic;

/// <summary>本局关卡快照，字段名保持与已有存档一致。</summary>
[Serializable]
public class RunLevelData
{
    public string Id;
    public int LevelType;
    public int FinishReward;
    public int ColumnNum;
    public int PocketNum;
    public string NextLevelId;
    public int NeedStraightNum;
    public int LevelEvent;
    public List<int> initialCards = new();
    public List<int> actualInitialCards = new();
    public float difficulty;

    public static int GetChapter(string id) => int.Parse(id.Substring(0, 2));
    public static int GetLayer(string id) => int.Parse(id.Substring(2, 2));
    public static int GetLayerIndex(string id) => int.Parse(id.Substring(4, 2));
}

[Serializable]
public class RunChapterData
{
    public List<RunLevelData> levels = new();
}

[Serializable]
public class RunMapData
{
    public int seed;
    public int generatorVersion;
    public List<RunChapterData> chapters = new();
}
