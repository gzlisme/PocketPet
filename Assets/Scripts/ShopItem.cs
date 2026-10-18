using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 商店商品配置（阶段三任务10）：纯 C# 静态列表，第一版不使用 ScriptableObject。
/// slot 取值："hat" / "scarf" / "glasses"，对应小鸡的三个装饰槽位。
/// </summary>
public class ShopItem
{
    public string id;          // 唯一标识，如 "hat_red"
    public string displayName; // 显示名，如 "红色帽子"
    public long price;         // 价格
    public string slot;        // 装备槽位
    public Color color;         // 装饰品颜色（场景中色块显示用）

    public static readonly List<ShopItem> AllItems = new List<ShopItem>
    {
        new ShopItem { id = "hat_red",       displayName = "红色帽子", price = 800L,     slot = "hat",     color = new Color(1f, 0f, 0f) },              // #FF0000
        new ShopItem { id = "scarf_blue",    displayName = "蓝色围巾", price = 2000L,    slot = "scarf",   color = new Color(0f, 0f, 1f) },              // #0000FF
        new ShopItem { id = "glasses_black", displayName = "黑色墨镜", price = 4000L,    slot = "glasses", color = new Color(0f, 0f, 0f) },             // #000000
        new ShopItem { id = "hat_gold",      displayName = "金色帽子", price = 7000L,    slot = "hat",     color = new Color(1f, 215f / 255f, 0f) },    // #FFD700
        new ShopItem { id = "scarf_purple",  displayName = "紫色围巾", price = 10000L,   slot = "scarf",   color = new Color(128f / 255f, 0f, 128f / 255f) } // #800080
    };

    public static ShopItem FindById(string itemId)
    {
        if (string.IsNullOrEmpty(itemId)) return null;
        foreach (var it in AllItems)
            if (it.id == itemId) return it;
        return null;
    }
}
