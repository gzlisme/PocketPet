using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 投蛋材料表（2026-10-19 天材地宝投蛋 · 任务1）。风格照抄 ShopItem：纯静态配置，加材料只加一行。
///
/// 两类材料，互不打架：
///   定系材料（family 非空）—— 投入后对应系的物种在抽牌时权重 ×PetSpecies.MaterialFamilyBoost；
///                              同一系可以叠投（两个羽系材料 = 倍数相乘），但一个格子只能放一个。
///   贵重物（family 为空）—— 不定系，改写四档稀有度权重并设定本次抽取的稀有度上限；
///                              投多个贵重物时只取上限最高的一件生效（星髓覆盖月光碎片），不叠加。
///
/// 库存不在这张表里：运行时库存存 GameData.materialInventory（"id:数量,..." 字符串），
/// 解析 / 回拼工具在本类底部，跟表放一起方便维护。
/// 获取渠道：本版只有开局赠送（DefaultInventory，老档缺字段时同样补发一份）；
/// 后续接商店 / 活动掉落时统一调 GameData.AddMaterial，不用动这张表。
/// </summary>
public class PetMaterial
{
    public string id;           // 存档用 id（英文，永不改）
    public string displayName;  // 中文名
    public string family;       // 定系：羽/绒/鳞/木/光/影；空 = 贵重物
    public Color color;         // 材料格选中态的底色（占位渲染）
    public string desc;         // 玩家可读的效果说明（愿望页 hint 显示用）
    public int rarityCap;       // 贵重物：本次抽取的稀有度上限；定系材料不参与封顶（恒填传说档）
    public int[] rarityWeights; // 贵重物：重塑后的四档稀有度权重（普通/少见/稀有/传说）；定系材料为 null

    /// <summary>是否贵重物（贵重物定稀有度上限，不参与定系加权）</summary>
    public bool IsTreasure { get { return string.IsNullOrEmpty(family); } }

    public static readonly PetMaterial[] All =
    {
        // ---- 定系材料：六个系各一种，投入后对应系权重 ×4 ----
        new PetMaterial { id = "feather_down", displayName = "风绒羽", family = "羽",
            color = new Color(0.95f, 0.92f, 0.80f), desc = "更容易孵出「羽」系",
            rarityCap = PetSpecies.RarityLegend },
        new PetMaterial { id = "fluff_ball", displayName = "暖绒团", family = "绒",
            color = new Color(0.98f, 0.85f, 0.88f), desc = "更容易孵出「绒」系",
            rarityCap = PetSpecies.RarityLegend },
        new PetMaterial { id = "snake_slough", displayName = "银蛇蜕", family = "鳞",
            color = new Color(0.72f, 0.88f, 0.82f), desc = "更容易孵出「鳞」系",
            rarityCap = PetSpecies.RarityLegend },
        new PetMaterial { id = "heartwood_nut", displayName = "心木果", family = "木",
            color = new Color(0.62f, 0.48f, 0.30f), desc = "更容易孵出「木」系",
            rarityCap = PetSpecies.RarityLegend },
        new PetMaterial { id = "morning_glow", displayName = "晨光砂", family = "光",
            color = new Color(1f, 0.93f, 0.60f), desc = "更容易孵出「光」系",
            rarityCap = PetSpecies.RarityLegend },
        new PetMaterial { id = "night_mist", displayName = "夜雾晶", family = "影",
            color = new Color(0.45f, 0.42f, 0.62f), desc = "更容易孵出「影」系",
            rarityCap = PetSpecies.RarityLegend },

        // ---- 贵重物：定稀有度上限（投多个只取上限最高的一件生效）----
        // 月光碎片：不出传说了，但「稀有」稳得多（55/28/14/3 → 40/35/25/0）。
        new PetMaterial { id = "moon_shard", displayName = "月光碎片", family = "",
            color = new Color(0.80f, 0.87f, 1f), desc = "稀有度封顶「稀有」，出稀有的机会大增",
            rarityCap = PetSpecies.RarityRare,   rarityWeights = new[] { 40, 35, 25, 0 } },
        // 星髓：传说概率 3% → 10%，全游戏最贵的定向手段。
        new PetMaterial { id = "star_marrow", displayName = "星髓", family = "",
            color = new Color(1f, 0.88f, 0.45f), desc = "传说概率从 3% 提到约 10%",
            rarityCap = PetSpecies.RarityLegend, rarityWeights = new[] { 30, 30, 30, 10 } },
    };

    /// <summary>开局赠送（新档默认值；老档缺 materialInventory 字段时按同一份补发）。改这里 = 改所有人的初始材料。</summary>
    public const string DefaultInventory =
        "feather_down:2,fluff_ball:2,snake_slough:2,heartwood_nut:2,morning_glow:2,night_mist:2,moon_shard:2,star_marrow:1";

    public static PetMaterial FindById(string materialId)
    {
        if (string.IsNullOrEmpty(materialId)) return null;
        foreach (var m in All)
            if (m.id == materialId) return m;
        return null;
    }

    // ---------------- 库存字符串工具 ----------------

    /// <summary>
    /// "moon_shard:2,star_marrow:1" → 字典。
    /// 容错：空段 / 无冒号 / 数量非正数 / 未知 id 一律跳过；重复 id 数量累加（手改档兜底）。
    /// </summary>
    public static Dictionary<string, int> ParseInventory(string inventory)
    {
        var result = new Dictionary<string, int>();
        if (string.IsNullOrEmpty(inventory)) return result;
        foreach (var pair in inventory.Split(','))
        {
            if (string.IsNullOrEmpty(pair)) continue;
            int colon = pair.LastIndexOf(':');
            if (colon <= 0) continue;
            string id = pair.Substring(0, colon).Trim();
            int count;
            if (!int.TryParse(pair.Substring(colon + 1).Trim(), out count) || count <= 0) continue;
            if (FindById(id) == null) continue; // 材料下架后老档里的残留 id，直接忽略
            result[id] = result.ContainsKey(id) ? result[id] + count : count;
        }
        return result;
    }

    /// <summary>字典 → 存档字符串。按 All 表顺序输出保证存档 diff 稳定；数量 ≤0 的不入串（用光即消失）。</summary>
    public static string BuildInventory(Dictionary<string, int> inventory)
    {
        if (inventory == null || inventory.Count == 0) return "";
        var sb = new System.Text.StringBuilder();
        foreach (var m in All)
        {
            int count;
            if (!inventory.TryGetValue(m.id, out count) || count <= 0) continue;
            if (sb.Length > 0) sb.Append(',');
            sb.Append(m.id).Append(':').Append(count);
        }
        return sb.ToString();
    }
}
