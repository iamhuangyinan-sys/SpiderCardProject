using System.Collections.Generic;

namespace Framework.UI
{
    /// <summary>
    /// 场景输入锁 —— 只要有任意「遮挡型面板」（实现 ISceneInputBlockPanel）开着，
    /// IsLocked 就是 true，业务层据此忽略场景输入（牌桌拖拽等）
    ///
    /// 存面板引用列表而不是计数：
    ///   重复上锁 / 重复解锁都不会出错；
    ///   面板被直接销毁（换场景等）而没走 Close 时，查询时会自动剔除，不会永久锁死输入
    ///
    /// 上锁 / 解锁由 BasePanel 生命周期统一调用，业务层只读 IsLocked
    /// </summary>
    public static class SceneInputLock
    {
        /// <summary>当前打开着的遮挡型面板</summary>
        private static readonly List<BasePanel> _owners = new();

        /// <summary>场景输入是否被遮挡（业务层锁输入用）</summary>
        public static bool IsLocked
        {
            get
            {
                Prune();
                return _owners.Count > 0;
            }
        }

        /// <summary>面板打开：上锁（重复上锁无副作用）</summary>
        public static void Lock(BasePanel owner)
        {
            if (owner == null || _owners.Contains(owner)) return;

            _owners.Add(owner);
        }

        /// <summary>面板隐藏 / 关闭：解锁（没上过锁时调用也无副作用）</summary>
        public static void Unlock(BasePanel owner)
        {
            if (owner == null) return;

            _owners.Remove(owner);
        }

        /// <summary>剔除已被销毁的面板（Unity 的 == null 能识别已销毁对象）</summary>
        private static void Prune()
        {
            for (int i = _owners.Count - 1; i >= 0; i--)
            {
                if (_owners[i] == null) _owners.RemoveAt(i);
            }
        }
    }
}
