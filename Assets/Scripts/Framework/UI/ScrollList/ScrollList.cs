using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Framework.UI
{
    /// <summary>
    /// 虚拟滚动列表 —— 挂载在 Scroll View 物体上
    ///
    /// Inspector 设置：
    ///   ScrollRect、Content、CellPrefab、ItemWidth/Height、Columns、Gap
    ///
    /// 使用方式：
    ///   scrollList.ItemCount = dataList.Count;
    ///   scrollList.OnItemRender = (index, item) => { item.comps.txtName.text = ... };
    ///   scrollList.Refresh();
    /// </summary>
    [RequireComponent(typeof(ScrollRect))]
    public class ScrollList : MonoBehaviour
    {
        [Header("滚动组件")]
        [SerializeField] private ScrollRect _scrollRect;
        [SerializeField] private RectTransform _content;
        [SerializeField] private GameObject _cellPrefab;

        [Header("Item 尺寸")]
        [SerializeField] private float _itemWidth = 200f;
        [SerializeField] private float _itemHeight = 80f;
        [SerializeField] private float _columnGap = 10f;
        [SerializeField] private float _rowGap = 10f;

        [Header("布局")]
        [SerializeField] private int _columns = 1;

        // ==================== 公开 API ====================

        /// <summary> 数据总数，设值即刷新 </summary>
        public int ItemCount
        {
            get => _totalCount;
            set
            {
                _totalCount = value;
                UpdateContentSize();
                Refresh();
            }
        }

        /// <summary> 渲染回调（int: 数据索引, BaseListItem: Cell 实例） </summary>
        public Action<int, BaseListItem> OnItemRender;

        // ==================== 内部 ====================

        private ListItemPool _pool;
        private readonly List<Slot> _slots = new();
        private int _totalCount;
        private int _firstVisibleIndex;
        private int _scrollVer;

        private struct Slot
        {
            public int index;
            public BaseListItem item;
            public int version;
        }

        private void Awake()
        {
            if (_scrollRect == null) _scrollRect = GetComponent<ScrollRect>();
            _scrollRect.onValueChanged.AddListener(_ => HandleScroll(false));
        }

        /// <summary> 刷新列表 </summary>
        public void Refresh()
        {
            if (_pool == null)
            {
                if (_cellPrefab == null)
                {
                    Debug.LogError("[ScrollList] CellPrefab 未设置");
                    return;
                }
                _pool = new ListItemPool(_cellPrefab, _content);
            }

            // 回收所有当前活跃的
            for (int i = _slots.Count - 1; i >= 0; i--)
                _pool.Put(_slots[i].item);
            _slots.Clear();

            // 视口高度还没算好（布局未完成）→ 延迟一帧
            if (_scrollRect.viewport.rect.height <= 0f)
            {
                StopAllCoroutines();
                StartCoroutine(RefreshNextFrame());
                return;
            }

            HandleScroll(true);
        }

        private System.Collections.IEnumerator RefreshNextFrame()
        {
            yield return null;
            HandleScroll(true);
        }

        /// <summary> 滚动到指定索引 </summary>
        public void ScrollTo(int index)
        {
            if (_totalCount == 0) return;
            index = Mathf.Clamp(index, 0, _totalCount - 1);
            int row = index / _columns;
            float targetY = row * (_itemHeight + _rowGap);
            float maxY = Mathf.Max(0, _content.sizeDelta.y - _scrollRect.viewport.rect.height);
            _content.anchoredPosition = new Vector2(0, Mathf.Min(targetY, maxY));
            Refresh();
        }

        /// <summary> 刷新指定索引的 item </summary>
        public void RefreshItem(int index)
        {
            foreach (var slot in _slots)
            {
                if (slot.index == index)
                {
                    OnItemRender?.Invoke(index, slot.item);
                    return;
                }
            }
        }

        // ==================== 虚拟滚动核心 ====================

        private void HandleScroll(bool force)
        {
            if (_pool == null || _totalCount == 0) return;

            _firstVisibleIndex = GetFirstVisibleIndex();
            _scrollVer++;

            RenderVisible();
            RecycleInvisible();
        }

        private int GetFirstVisibleIndex()
        {
            float rowH = _itemHeight + _rowGap;
            int row = Mathf.Max(0, Mathf.FloorToInt(_content.anchoredPosition.y / rowH));
            return row * _columns;
        }

        private void RenderVisible()
        {
            float viewHeight = _scrollRect.viewport.rect.height;
            float contentY = _content.anchoredPosition.y;
            float viewBottom = contentY + viewHeight + _itemHeight; // 多渲染一行做缓冲

            int idx = _firstVisibleIndex;
            float rowH = _itemHeight + _rowGap;
            float y = (idx / _columns) * rowH;

            while (idx < _totalCount && y < viewBottom)
            {
                int col = idx % _columns;
                float x = col * (_itemWidth + _columnGap);

                // 先查找已有 slot，复用；没有再向池取
                BaseListItem item = null;
                for (int i = 0; i < _slots.Count; i++)
                {
                    if (_slots[i].index == idx)
                    {
                        item = _slots[i].item;
                        _slots[i] = new Slot { index = idx, item = item, version = _scrollVer };
                        break;
                    }
                }

                if (item == null)
                {
                    item = _pool.Get();
                    item.RectTransform.sizeDelta = new Vector2(_itemWidth, _itemHeight);
                    _slots.Add(new Slot { index = idx, item = item, version = _scrollVer });
                }

                item.RectTransform.anchoredPosition = new Vector2(x, -y);
                item.DataIndex = idx;
                OnItemRender?.Invoke(idx, item);

                idx++;
                if (idx % _columns == 0) y += rowH;
            }
        }

        private void RecycleInvisible()
        {
            for (int i = _slots.Count - 1; i >= 0; i--)
            {
                if (_slots[i].version != _scrollVer)
                {
                    _pool.Put(_slots[i].item);
                    _slots.RemoveAt(i);
                }
            }
        }

        private void UpdateContentSize()
        {
            int rows = Mathf.CeilToInt((float)_totalCount / _columns);
            float height = rows * (_itemHeight + _rowGap) - _rowGap;
            _content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Mathf.Max(height, 0));
        }

        private void OnDestroy()
        {
            _scrollRect?.onValueChanged.RemoveAllListeners();
            for (int i = _slots.Count - 1; i >= 0; i--)
                _pool?.Put(_slots[i].item);
            _slots.Clear();
            _pool?.Clear();
        }
    }
}
