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

}

[Serializable]
public class RunMapData
{
    public int seed;
    public int generatorVersion;
    public List<RunLevelData> levels = new();
}
