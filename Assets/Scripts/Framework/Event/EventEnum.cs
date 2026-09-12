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
        OnColumnPrepend,     // 参数：int（列索引，该列堆底增量插入一张，沉底发牌用）
        OnDrawPileChanged,   // 参数：int（发牌堆剩余数量）
        OnDiscardPileChanged, // 参数：int（弃牌堆数量）
        OnPocketChanged,     // 参数：int（牌包索引）
        OnCardChanged,       // 参数：CardData（单张牌变化，如翻牌）
        OnCardToDiscard,     // 参数：CardData（单张牌回收，播放飞到弃牌堆动画）
        OnShuffleBack,       // 无参数：弃牌堆洗回发牌堆动画
        OnCardWashIn,        // 参数：string（牌 id，洗入发牌堆：中央生成一张牌飞向发牌堆）
        OnStraightCountChanged,  // 无参数：接龙次数变化（进度刷新）
        OnLevelStart,            // 无参数：一局游戏开始（选关后）
        OnLevelComplete,         // 无参数：通关
        OnDiscardToDrawPile,     // 无参数：结算收牌 —— 弃牌堆收回发牌堆动画（一张牌背代表整堆）
        OnCardToDrawPile,        // 参数：CardData（单张牌收回发牌堆动画，场上/牌包的牌本体飞行）
        OnCardsCollected,        // 无参数：结算收牌完成（关卡结束）

        // ---- 本局（一大局）资产 ----
        OnCoinChanged,           // 无参数：本局金币变化

        // ---- 商店 ----
        OnShopOpen,              // 无参数：选中商店关，打开商店面板
        OnShopChanged,           // 无参数：商店商品变化（刷新 / 售出）
        OnShopClosed,            // 无参数：商店结束（回选关）
    }
}
