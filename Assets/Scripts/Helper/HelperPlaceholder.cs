namespace Framework.Helper
{
    /// <summary>
    /// Helper 工具类规范 —— 纯静态方法，无状态、无依赖
    ///
    /// 使用方式：Helper.XXX()
    /// 例如：MathHelper.Clamp()、StringHelper.Format()
    ///
    /// 注意：
    /// 1. 所有方法应为 static
    /// 2. 不持有任何实例数据
    /// 3. 不依赖 Store 或 Mgr
    /// </summary>
    public static class HelperPlaceholder
    {
        // 后续在此目录添加具体的 Helper 类，如：
        // MathHelper.cs    — 数学运算
        // StringHelper.cs  — 字符串处理
        // TimeHelper.cs    — 时间日期
        // IOHelper.cs      — 文件读写
        // ...
    }
}
