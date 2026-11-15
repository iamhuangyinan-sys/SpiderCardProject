public static class BuffHelper
{
    public static BuffConfig GetConfig(E_BuffEnum id)
    {
        return ConfigHelper.Get<BuffConfig>(((int)id).ToString());
    }
}
