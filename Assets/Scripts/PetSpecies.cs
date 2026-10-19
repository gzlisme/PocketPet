using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 物种表（2026-10-19 孵化系统 · Loop 2）。
///
/// 产品定位：这不是「养鸡游戏」，是「从蛋里孵出来的东西，每次都不一样」。
/// 所以物种必须是一张可扩展的静态配置表 —— 加一个新物种只需在这里加一行，
/// 跟 ShopItem.AllItems 加商品是同一个套路。
///
/// 设计铁律（来自对同类产品的调研）：
///   1. 数值 / 体型 / 轮廓必须由本地规则锁死，绝不能让 AI 随意决定，否则游戏会崩。
///   2. 玩法上不做对战，所以这里没有任何攻击力/防御力字段 —— 这是「养」的游戏。
///   3. 现阶段的视觉靠「颜色 + 体型缩放」占位，不依赖任何美术资源；
///      等美术到位后，把 bodyColor/sizeScale 换成真正的 Sprite 即可，数据层不用动。
///
/// 六个「系」：羽 / 绒 / 鳞 / 木 / 光 / 影
/// 四个稀有度：普通 / 少见 / 稀有 / 传说
/// </summary>
public static class PetSpecies
{
    // ---------------- 稀有度 ----------------

    public const int RarityCommon   = 0; // 普通
    public const int RarityUncommon = 1; // 少见
    public const int RarityRare     = 2; // 稀有
    public const int RarityLegend   = 3; // 传说

    /// <summary>各稀有度的抽取权重（万分比思路：直接当整数权重用）</summary>
    private static readonly int[] RarityWeights = { 55, 28, 14, 3 };

    /// <summary>保底阈值：连续这么多次没出「稀有及以上」，下一次强制出</summary>
    public const int PityThreshold = 10;

    /// <summary>保底触发时出传说的概率（其余给稀有）</summary>
    private const float PityLegendChance = 0.15f;

    public static string GetRarityName(int rarity)
    {
        switch (rarity)
        {
            case RarityUncommon: return "少见";
            case RarityRare:     return "稀有";
            case RarityLegend:   return "传说";
            default:             return "普通";
        }
    }

    /// <summary>稀有度对应的展示色（用于抽牌/揭晓时的文字与边框）</summary>
    public static Color GetRarityColor(int rarity)
    {
        switch (rarity)
        {
            case RarityUncommon: return new Color(0.45f, 0.85f, 0.55f);  // 绿
            case RarityRare:     return new Color(0.42f, 0.68f, 1f);     // 蓝
            case RarityLegend:   return new Color(1f, 0.78f, 0.28f);     // 金
            default:             return new Color(0.82f, 0.80f, 0.76f);  // 灰白
        }
    }

    // ---------------- 物种定义 ----------------

    /// <summary>一个物种的全部静态属性。加字段时注意：这里是纯配置，不进存档。</summary>
    public class Def
    {
        public string id;            // 存档用 id（英文，永不改）
        public string displayName;   // 中文名
        public string family;        // 系：羽/绒/鳞/木/光/影
        public int rarity;           // 稀有度 0~3
        public Color bodyColor;      // 主体色（现阶段占位渲染用）
        public float sizeScale;      // 体型缩放：视觉差异的第二维度（0.85~1.35）
        public string traitWords;    // 性格词，逗号分隔，供台词生成使用
        public string desc;          // 图鉴里的一句话
    }

    /// <summary>全部物种。新增物种在这里加一行即可，UI 与抽取逻辑自动适配。</summary>
    public static readonly Def[] All =
    {
        // ---- 羽：轻快、爱跳、话多 ----
        new Def { id = "chicken",  displayName = "小鸡",   family = "羽", rarity = RarityCommon,
                  bodyColor = new Color(1f, 0.86f, 0.42f), sizeScale = 1.00f,
                  traitWords = "好奇,贪吃,话多", desc = "看见什么都要啄一口，包括你的手指。" },
        new Def { id = "duck",     displayName = "小鸭",   family = "羽", rarity = RarityUncommon,
                  bodyColor = new Color(0.96f, 0.95f, 0.88f), sizeScale = 1.05f,
                  traitWords = "憨,爱水,慢半拍", desc = "走路一摆一摆，但从来不觉得自己慢。" },
        new Def { id = "owl",      displayName = "小鸮",   family = "羽", rarity = RarityRare,
                  bodyColor = new Color(0.62f, 0.48f, 0.34f), sizeScale = 0.90f,
                  traitWords = "安静,夜行,盯着你", desc = "白天不理你，半夜眼睛比灯还亮。" },

        // ---- 绒：软、憨、贪吃 ----
        new Def { id = "rabbit",   displayName = "小兔",   family = "绒", rarity = RarityCommon,
                  bodyColor = new Color(0.95f, 0.90f, 0.90f), sizeScale = 0.95f,
                  traitWords = "胆小,爱吃菜,耳朵灵", desc = "听见你开冰箱的声音，比听见自己名字还快。" },
        new Def { id = "sheep",    displayName = "小羊",   family = "绒", rarity = RarityUncommon,
                  bodyColor = new Color(0.97f, 0.96f, 0.93f), sizeScale = 1.10f,
                  traitWords = "温顺,爱睡,粘人", desc = "睡着了会往你身上靠，压得你腿麻也不敢动。" },
        new Def { id = "cotton",   displayName = "棉花团", family = "绒", rarity = RarityRare,
                  bodyColor = new Color(1f, 0.99f, 0.97f), sizeScale = 0.88f,
                  traitWords = "轻,飘,怕风", desc = "一阵风就能吹走，所以总抓着你衣角。" },

        // ---- 鳞：冷、慢、有点神秘 ----
        new Def { id = "turtle",   displayName = "小龟",   family = "鳞", rarity = RarityCommon,
                  bodyColor = new Color(0.44f, 0.66f, 0.42f), sizeScale = 0.92f,
                  traitWords = "慢,固执,长寿", desc = "你以为它没动，其实它走了一整天。" },
        new Def { id = "lizard",   displayName = "小蜥",   family = "鳞", rarity = RarityUncommon,
                  bodyColor = new Color(0.42f, 0.72f, 0.62f), sizeScale = 0.95f,
                  traitWords = "警觉,晒太阳,冷", desc = "最喜欢趴在窗台那道阳光里，一趴一整个下午。" },
        new Def { id = "dragonet", displayName = "小蛟",   family = "鳞", rarity = RarityRare,
                  bodyColor = new Color(0.30f, 0.62f, 0.78f), sizeScale = 1.15f,
                  traitWords = "骄傲,认主,威严", desc = "还没长大，但看你的眼神已经像在看臣子。" },

        // ---- 木：安静、爱睡、不说话 ----
        new Def { id = "mossball", displayName = "苔球",   family = "木", rarity = RarityCommon,
                  bodyColor = new Color(0.42f, 0.60f, 0.32f), sizeScale = 0.90f,
                  traitWords = "安静,慢,晒太阳", desc = "一整天不动。你要是忘了浇水，它会生闷气。" },
        new Def { id = "mushroom", displayName = "小菇",   family = "木", rarity = RarityUncommon,
                  bodyColor = new Color(0.86f, 0.42f, 0.38f), sizeScale = 0.93f,
                  traitWords = "害羞,怕光,夜长", desc = "只在你关灯之后偷偷长高一点点。" },
        new Def { id = "vine",     displayName = "藤芽",   family = "木", rarity = RarityRare,
                  bodyColor = new Color(0.30f, 0.52f, 0.26f), sizeScale = 1.05f,
                  traitWords = "缠人,倔,向上", desc = "会顺着你的手臂往上爬，直到够到你的脸。" },

        // ---- 光：贵、会发光、稀有 ----
        new Def { id = "lampling", displayName = "灯芯",   family = "光", rarity = RarityUncommon,
                  bodyColor = new Color(1f, 0.90f, 0.58f), sizeScale = 0.85f,
                  traitWords = "暖,怕冷,照亮", desc = "靠近它，掌心会热一点。冬天它最抢手。" },
        new Def { id = "phoenixlet", displayName = "凤凰雏", family = "光", rarity = RarityLegend,
                  bodyColor = new Color(1f, 0.60f, 0.30f), sizeScale = 1.20f,
                  traitWords = "高傲,不驯,灼热", desc = "窝里那点光，是它还没学会收敛的火。" },
        new Def { id = "qilin",    displayName = "小麒麟", family = "光", rarity = RarityLegend,
                  bodyColor = new Color(1f, 0.86f, 0.55f), sizeScale = 1.18f,
                  traitWords = "温和,高贵,认人", desc = "踩过的地方，草会长得格外好。" },

        // ---- 影：调皮、坏、爱捉弄 ----
        new Def { id = "shadowcat", displayName = "影猫",  family = "影", rarity = RarityUncommon,
                  bodyColor = new Color(0.34f, 0.30f, 0.46f), sizeScale = 0.94f,
                  traitWords = "顽皮,夜里跑,不认生", desc = "你找不到它的时候，它通常就在你脚边。" },
        new Def { id = "ghostie",  displayName = "小幽灵", family = "影", rarity = RarityRare,
                  bodyColor = new Color(0.82f, 0.85f, 0.92f), sizeScale = 1.02f,
                  traitWords = "飘,胆小,爱吓人", desc = "想吓你一跳，但每次都自己先被吓到。" },
    };

    /// <summary>按 id 查物种；找不到返回 null（存档里存了已删除的物种时兜底用）</summary>
    public static Def FindById(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        for (int i = 0; i < All.Length; i++)
            if (All[i].id == id) return All[i];
        return null;
    }

    /// <summary>默认物种：存档为空或 id 失效时回退到小鸡（保证老存档不炸）</summary>
    public static Def Default { get { return All[0]; } }

    /// <summary>某个稀有度下的物种数量（用于 UI 展示图鉴进度）</summary>
    public static int CountByRarity(int rarity)
    {
        int n = 0;
        for (int i = 0; i < All.Length; i++) if (All[i].rarity == rarity) n++;
        return n;
    }

    // ---------------- 抽取 ----------------

    /// <summary>
    /// 掷一个稀有度。pityCount = 连续未出「稀有及以上」的次数。
    /// 达到 PityThreshold 后强制出稀有/传说，避免玩家长期挫败。
    /// </summary>
    private static int RollRarity(int pityCount)
    {
        if (pityCount >= PityThreshold)
            return Random.value < PityLegendChance ? RarityLegend : RarityRare;

        int total = 0;
        for (int i = 0; i < RarityWeights.Length; i++) total += RarityWeights[i];

        int r = Random.Range(0, total);
        int acc = 0;
        for (int i = 0; i < RarityWeights.Length; i++)
        {
            acc += RarityWeights[i];
            if (r < acc) return i;
        }
        return RarityCommon;
    }

    /// <summary>
    /// 抽一个物种。先掷稀有度、再在该稀有度内等概率挑一个。
    /// 若目标稀有度下没有任何物种（例如以后删掉了传说），自动向下回退到最近的可用档。
    /// </summary>
    public static Def Roll(int pityCount)
    {
        int rarity = RollRarity(pityCount);

        for (int attempt = 0; attempt <= RarityLegend; attempt++)
        {
            int want = rarity - attempt;
            if (want < RarityCommon) break;

            int count = CountByRarity(want);
            if (count <= 0) continue;

            int pick = Random.Range(0, count);
            for (int i = 0; i < All.Length; i++)
            {
                if (All[i].rarity != want) continue;
                if (pick == 0) return All[i];
                pick--;
            }
        }

        return Default; // 理论不可达：兜底
    }

    // ---------------- 投蛋加权抽取（2026-10-19 天材地宝 · 任务1）----------------

    /// <summary>定系材料的系别权重倍数：一个 ×4，同系叠投相乘（三个同类 = ×64，几乎锁定该系）</summary>
    public const int MaterialFamilyBoost = 4;

    /// <summary>
    /// 带材料的抽取（任务1）：定系材料把对应系权重乘上去，贵重物改写稀有度权重并封上限。
    /// materials 为空 / null 时直接走原始 Roll —— 验收标准：不投材料时概率与现状完全一致。
    /// 保底逻辑照旧生效；贵重物的上限对保底结果同样生效（保底掷出传说但上限是稀有 → 出稀有）。
    /// </summary>
    public static Def Roll(int pityCount, List<PetMaterial> materials)
    {
        if (materials == null || materials.Count == 0) return Roll(pityCount);

        // ---- 贵重物：取上限最高的一件生效，不叠加 ----
        int cap = RarityLegend;
        int[] weights = RarityWeights;
        PetMaterial bestTreasure = null;
        for (int i = 0; i < materials.Count; i++)
        {
            var m = materials[i];
            if (m == null || !m.IsTreasure) continue;
            if (bestTreasure == null || m.rarityCap > bestTreasure.rarityCap) bestTreasure = m;
        }
        if (bestTreasure != null)
        {
            cap = bestTreasure.rarityCap;
            weights = bestTreasure.rarityWeights != null ? bestTreasure.rarityWeights : RarityWeights;
        }

        // ---- 定系倍数：同系叠投相乘 ----
        var familyMult = new Dictionary<string, float>();
        for (int i = 0; i < materials.Count; i++)
        {
            var m = materials[i];
            if (m == null || m.IsTreasure) continue;
            float cur = familyMult.ContainsKey(m.family) ? familyMult[m.family] : 1f;
            familyMult[m.family] = cur * MaterialFamilyBoost;
        }

        int rarity = RollRarity(pityCount, weights);
        if (rarity > cap) rarity = cap; // 上限对随机结果与保底结果一视同仁

        // 在稀有度内按系别倍数加权挑选；该档没有物种时向下回退（与原始 Roll 同思路）
        for (int want = rarity; want >= RarityCommon; want--)
        {
            Def picked = PickWeighted(want, familyMult);
            if (picked != null) return picked;
        }
        return Default; // 理论不可达：兜底
    }

    /// <summary>加权版稀有度掷骰：权重数组可被贵重物替换，保底分支与原版一致</summary>
    private static int RollRarity(int pityCount, int[] weights)
    {
        if (pityCount >= PityThreshold)
            return Random.value < PityLegendChance ? RarityLegend : RarityRare;

        int total = 0;
        for (int i = 0; i < weights.Length; i++) total += weights[i];
        if (total <= 0) return RarityCommon;

        int r = Random.Range(0, total);
        int acc = 0;
        for (int i = 0; i < weights.Length; i++)
        {
            acc += weights[i];
            if (r < acc) return i;
        }
        return RarityCommon;
    }

    /// <summary>在指定稀有度内按系别倍数加权挑一个物种；该稀有度没有物种返回 null</summary>
    private static Def PickWeighted(int rarity, Dictionary<string, float> familyMult)
    {
        float total = 0f;
        for (int i = 0; i < All.Length; i++)
        {
            if (All[i].rarity != rarity) continue;
            total += WeightOf(All[i], familyMult);
        }
        if (total <= 0f) return null;

        float r = Random.Range(0f, total);
        float acc = 0f;
        for (int i = 0; i < All.Length; i++)
        {
            if (All[i].rarity != rarity) continue;
            acc += WeightOf(All[i], familyMult);
            if (r < acc) return All[i];
        }
        return null;
    }

    /// <summary>物种的抽取权重：命中的系吃倍数，未命中 = 1（与不投材料时的档内等概率一致）</summary>
    private static float WeightOf(Def def, Dictionary<string, float> familyMult)
    {
        float mult;
        if (familyMult != null && familyMult.TryGetValue(def.family, out mult)) return mult;
        return 1f;
    }
}
