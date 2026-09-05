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
        // ---- 卡牌 ----
        OnTableChanged,      // 无参数：整桌刷新（发牌 / 重开）
        OnColumnChanged,     // 参数：int（列索引，该列局部刷新）
        OnDrawPileChanged,   // 无参数：发牌堆变化
        OnCardChanged,       // 参数：CardData（单张牌变化，如翻牌）
    }
}
