using Framework.Store;

/// <summary>玩家设置数据，由 PlayerSettingManager 统一读写。</summary>
public class PlayerSettingStore : StoreBase<PlayerSettingStore>
{
    public PlayerSettingData data = new();
}
