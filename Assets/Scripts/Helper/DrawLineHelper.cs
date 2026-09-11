using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 连线绘制助手 —— 在指定父物体下生成一条纯白 Image 线段（纯静态，不管销毁、不做动画）
///
/// 线段用无 sprite 的 Image 表示（Unity 会用内置白图渲染成纯白矩形），
/// 位置/旋转/长度都在父物体的局部坐标系下计算。
///
/// 使用方式：
///   var line = DrawLineHelper.Draw(parent, startPos, endPos, 6f, 0.2f);
///   Destroy(line.gameObject);   // 用完自行销毁
/// </summary>
public static class DrawLineHelper
{
    /// <summary>默认线粗（像素）</summary>
    public const float DefaultThickness = 6f;

    /// <summary>默认两头收缩比例</summary>
    public const float DefaultShrinkRatio = 0.2f;

    /// <summary>
    /// 在 parent 下画一条线段（局部坐标）
    /// </summary>
    /// <param name="parent">线段父物体（UI 节点）</param>
    /// <param name="start">起点（parent 局部坐标）</param>
    /// <param name="end">终点（parent 局部坐标）</param>
    /// <param name="thickness">线粗（像素）</param>
    /// <param name="shrinkRatio">
    /// 两头各往里收缩的比例（0~0.5），按原始线长算：
    /// 实际线长 = 原始长度 × (1 - 2 × shrinkRatio)，两头各留白 shrinkRatio × 原始长度
    /// </param>
    /// <returns>生成的线段 Image；parent 为空时返回 null</returns>
    public static Image Draw(Transform parent, Vector2 start, Vector2 end, float thickness, float shrinkRatio)
    {
        if (parent == null) return null;

        var go = new GameObject("Line", typeof(RectTransform), typeof(Image));

        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);

        var img = go.GetComponent<Image>();
        img.sprite = null;              // 无 sprite → 纯白矩形
        img.color = Color.white;
        img.raycastTarget = false;      // 不挡点击

        ApplyLine(rt, start, end, thickness, shrinkRatio);

        rt.SetAsFirstSibling();         // 默认压在最底层，需要置顶时调用方自行 SetAsLastSibling()
        return img;
    }

    /// <summary>在 parent 下画一条线段（使用默认粗细与收缩比例）</summary>
    public static Image Draw(Transform parent, Vector2 start, Vector2 end) =>
        Draw(parent, start, end, DefaultThickness, DefaultShrinkRatio);

    /// <summary>
    /// 把已有 RectTransform 摆成线段（位置/旋转/尺寸），可用于刷新已有线段
    /// </summary>
    public static void ApplyLine(RectTransform rt, Vector2 start, Vector2 end, float thickness, float shrinkRatio)
    {
        if (rt == null) return;

        Vector2 dir = end - start;
        float rawLength = dir.magnitude;

        // 锚点收成一点 → sizeDelta 即真实尺寸；pivot 居中 → localPosition 即线段中点
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.localScale = Vector3.one;
        rt.localPosition = (start + end) * 0.5f;

        // 起终点重合 → 只保留粗细，避免 NaN 旋转
        if (rawLength <= Mathf.Epsilon)
        {
            rt.localRotation = Quaternion.identity;
            rt.sizeDelta = new Vector2(0f, thickness);
            return;
        }

        float length = rawLength * (1f - 2f * Mathf.Clamp(shrinkRatio, 0f, 0.5f));

        rt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
        rt.sizeDelta = new Vector2(length, thickness);
    }
}
