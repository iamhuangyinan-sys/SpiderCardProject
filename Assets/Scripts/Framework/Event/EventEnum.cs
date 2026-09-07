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
        OnColumnChanged,     // 参数：int（列索引，该列整列重建）
        OnColumnAppend,      // 参数：int（列索引，该列末尾增量追加一张）
        OnDrawPileChanged,   // 参数：int（发牌堆剩余数量）
        OnDiscardPileChanged, // 参数：int（弃牌堆数量）
        OnPocketChanged,     // 参数：int（牌包索引）
        OnCardChanged,       // 参数：CardData（单张牌变化，如翻牌）
        OnCardToDiscard,     // 参数：CardData（单张牌回收，播放飞到弃牌堆动画）
        OnShuffleBack,       // 无参数：弃牌堆洗回发牌堆动画
    }
}
