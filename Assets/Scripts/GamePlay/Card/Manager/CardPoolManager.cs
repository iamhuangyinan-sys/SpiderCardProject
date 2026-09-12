using System;
using Framework.Mgr;
using Framework.Pool;
using UnityEngine;

/// <summary>
/// 卡牌对象池封装 —— 在 PoolManager 之上提供卡牌专用的取用/回收接口
/// 只做语义化封装与回收时重置，不重复实现池的底层逻辑
/// </summary>
public class CardPoolManager : ManagerBase<CardPoolManager>
{
    /// <summary>卡牌预制体在 Resources 下的路径</summary>
    private const string PrefabPath = "Prefab/GamePlay/Card/CardPrefab";

    /// <summary>同步取一张牌（池中有则复用，无则实例化）</summary>
    public CardView GetCard()
    {
        var obj = PoolManager.Instance.Spawn(PrefabPath);
        return obj != null ? obj.GetComponent<CardView>() : null;
    }

    /// <summary>异步取一张牌</summary>
    public void GetCardAsync(Action<CardView> onGot)
    {
        PoolManager.Instance.SpawnAsync(PrefabPath, obj =>
        {
            onGot?.Invoke(obj != null ? obj.GetComponent<CardView>() : null);
        });
    }

    /// <summary>回收一张牌（先解绑数据 + 复位渲染排序，再回池）</summary>
    public void Recycle(CardView view)
    {
        if (view == null) return;
        AnimationHelper.Kill(view.transform);
        view.Unbind();

        // 复位渲染排序：临时牌（洗入 / 洗回 / 结算收牌）是在「当前排序 + FlySortingOffset」上飞行的，
        // 而池是 LIFO、每次都会复用刚回收的同一个实例，不复位就会随复用次数一路累加，
        // 涨过 16 位上限（±32767）后渲染异常（表现为牌看不见了）
        view.SetSortingOrder(0);

        PoolManager.Instance.Despawn(view.gameObject);
    }
}
