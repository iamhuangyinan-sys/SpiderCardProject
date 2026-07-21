namespace Framework.Event
{
    /// <summary>
    /// 全局事件 Key 枚举 —— 所有游戏事件在此集中定义
    ///
    /// 使用方式：
    ///   EventManager.Instance.AddListener(E_EventEnum.OnScoreChanged, OnScoreChanged);
    ///   EventManager.Instance.RemoveListener(E_EventEnum.OnScoreChanged, OnScoreChanged);
    ///   EventManager.Instance.Dispatch(E_EventEnum.OnScoreChanged, 100);
    /// </summary>
    public enum E_EventEnum
    {
        // ---- 示例，按需增删 ----
        // OnScoreChanged,         // 参数：int (新分数)
        // OnCardPlayed,           // 参数：CardData
        // OnGameStart,            // 无参数
        // OnGameEnd,              // 参数：bool (是否胜利)
        // OnRoundChanged,         // 参数：int (当前回合)
        // OnPlayerNameChanged,    // 参数：string (新名字)
    }
}
