using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 商店商品配置（阶段三任务10）：纯 C# 静态列表，第一版不使用 ScriptableObject。
/// slot 取值："hat" / "scarf" / "glasses" 对应小鸡三个装饰槽位；"toy" 为玩具（wb 分支新增）。
/// 玩具与装饰品的区别：买断后不"装备"到小鸡身上，而是选为"当前玩的玩具"，在院子里陪玩加心情。
/// </summary>
public class ShopItem
{
    public string id;          // 唯一标识，如 "hat_red"
    public string displayName; // 显示名，如 "红色帽子"
    public long price;         // 价格
    public string slot;        // 装备槽位（hat/scarf/glasses/toy）
    public Color color;         // 装饰品颜色（场景中色块显示用；玩具仅作图标色）

    /// <summary>玩具专用：陪玩一次加多少心情；非玩具为 0</summary>
    public float toyMoodGain;

    /// <summary>是否玩具（玩具不进装饰槽位体系，购买后选为"当前玩具"）</summary>
    public bool IsToy { get { return slot == "toy"; } }

    public static readonly List<ShopItem> AllItems = new List<ShopItem>
    {
        new ShopItem { id = "hat_red",       displayName = "红色帽子", price = 800L,     slot = "hat",     color = new Color(1f, 0f, 0f) },              // #FF0000
        new ShopItem { id = "scarf_blue",    displayName = "蓝色围巾", price = 2000L,    slot = "scarf",   color = new Color(0f, 0f, 1f) },              // #0000FF
        new ShopItem { id = "glasses_black", displayName = "黑色墨镜", price = 4000L,    slot = "glasses", color = new Color(0f, 0f, 0f) },             // #000000
        new ShopItem { id = "hat_gold",      displayName = "金色帽子", price = 4000L,    slot = "hat",     color = new Color(1f, 215f / 255f, 0f) },    // #FFD700（2026-10-19 调价 7000→4000，老板拍板）
        new ShopItem { id = "scarf_purple",  displayName = "紫色围巾", price = 6000L,    slot = "scarf",   color = new Color(128f / 255f, 0f, 128f / 255f) }, // #800080（2026-10-19 调价 10000→6000，老板拍板）

        // ---- 玩具（wb 分支：豪华玩具下线，改由商店购买）----
        // 买断制，每天可陪玩 5 次；玩具不受心情软天花板（80）限制，是玩家"花钱突破上限"的唯一途径。
        // 价格与加成必须同向递增，否则会出现"贵但亏"的陷阱商品（2026-10-19 老板拍板加中价位两档）。
        new ShopItem { id = "toy_ball",      displayName = "花皮球",   price = 300L,     slot = "toy",     color = new Color(1f, 90f / 255f, 90f / 255f), toyMoodGain = 15f },
        new ShopItem { id = "toy_frisbee",   displayName = "小飞盘",   price = 800L,     slot = "toy",     color = new Color(0f, 206f / 255f, 209f / 255f), toyMoodGain = 30f },
        new ShopItem { id = "toy_rattle",    displayName = "拨浪鼓",   price = 1200L,    slot = "toy",     color = new Color(210f / 255f, 105f / 255f, 30f / 255f), toyMoodGain = 40f }, // #D2691E
        new ShopItem { id = "toy_swing",     displayName = "小秋千",   price = 2000L,    slot = "toy",     color = new Color(76f / 255f, 175f / 255f, 80f / 255f), toyMoodGain = 50f },  // #4CAF50
    };

    /// <summary>全部玩具（供院子/商店展示用）</summary>
    public static List<ShopItem> AllToys()
    {
        var list = new List<ShopItem>();
        foreach (var it in AllItems)
            if (it.IsToy) list.Add(it);
        return list;
    }

    public static ShopItem FindById(string itemId)
    {
        if (string.IsNullOrEmpty(itemId)) return null;
        foreach (var it in AllItems)
            if (it.id == itemId) return it;
        return null;
    }
}
