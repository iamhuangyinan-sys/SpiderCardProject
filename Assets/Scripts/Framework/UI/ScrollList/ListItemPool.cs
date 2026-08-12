using System.Collections.Generic;
using UnityEngine;

namespace Framework.UI
{
    /// <summary>
    /// BaseListItem 对象池 —— 内部使用，不对外暴露
    /// </summary>
    public class ListItemPool
    {
        private readonly Stack<BaseListItem> _cache = new();
        private readonly GameObject _prefab;
        private readonly Transform _parent;

        public ListItemPool(GameObject prefab, Transform parent)
        {
            _prefab = prefab;
            _parent = parent;
        }

        /// <summary> 从池取一个（无则 Instantiate） </summary>
        public BaseListItem Get()
        {
            BaseListItem item;

            if (_cache.Count > 0)
            {
                item = _cache.Pop();
                item.gameObject.SetActive(true);
            }
            else
            {
                var go = Object.Instantiate(_prefab, _parent);
                item = go.GetComponent<BaseListItem>();
                if (item == null)
                    item = go.AddComponent<BaseListItem>();
                item.RectTransform.localScale = Vector3.one;
            }

            item.IsInUse = true;
            item.OwnerPool = this;
            item.OnActivate();
            return item;
        }

        /// <summary> 放回池 </summary>
        public void Put(BaseListItem item)
        {
            if (item == null) return;

            item.OnRecycle();
            item.IsInUse = false;
            item.gameObject.SetActive(false);
            _cache.Push(item);
        }

        /// <summary> 清空池（销毁所有缓存对象） </summary>
        public void Clear()
        {
            while (_cache.Count > 0)
            {
                var item = _cache.Pop();
                if (item != null) Object.Destroy(item.gameObject);
            }
        }
    }
}
