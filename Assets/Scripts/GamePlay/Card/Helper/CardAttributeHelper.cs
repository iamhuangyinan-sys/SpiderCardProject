using System;
using System.Collections.Generic;

/// <summary>
/// 卡牌属性的文本 ↔ 枚举转换（控制台命令 / 调试用）。
///
/// 文本格式：枚举值，多个用分号隔开（中英文分号都行），空串 = 没有属性
///   例："10001;10002"、""（= 无属性）
///
/// 合法性交给枚举本身判断（Enum.IsDefined）—— 表里没有的值直接算不合法，
/// 避免「打了一张不存在的属性、表现层却什么都不显示」这种难查的情况
/// </summary>
public static class CardAttributeHelper
{
    /// <summary>解析属性文本；空 / 空白 = 空列表（也返回 true）</summary>
    /// <param name="text">形如 "10001;10002" 的文本</param>
    /// <param name="attributes">解析结果（重复的只保留一个）</param>
    /// <returns>全部都是合法属性返回 true</returns>
    public static bool TryParse(string text, out List<E_CardAttributeEnum> attributes)
    {
        attributes = new List<E_CardAttributeEnum>();
        if (string.IsNullOrWhiteSpace(text)) return true;

        var parts = text.Split(new[] { ';', '；' }, StringSplitOptions.RemoveEmptyEntries);

        foreach (var part in parts)
        {
            if (!int.TryParse(part.Trim(), out int value)) return false;
            if (!Enum.IsDefined(typeof(E_CardAttributeEnum), value)) return false;

            var attribute = (E_CardAttributeEnum)value;
            if (!attributes.Contains(attribute)) attributes.Add(attribute);
        }

        return true;
    }
}
