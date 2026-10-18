using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 宠物第一句话生成器（2026-10-19 孵化系统 · Loop 4）。
///
/// 这是整个设计里情感浓度最高的一处：
/// 玩家在蛋壳外面说了一段话，破壳之后，它开口的第一句要接住那段话。
///
/// 没有接 AI 也能做出「被听见」的效果，靠三件事：
///   1. 从愿望里检出关键词（陪伴 / 安静 / 吃 / 睡 / 温柔 / 活泼 / 好看 / 保护 / 长大）
///   2. 把玩家原话里的那个词，用引号原样引出来 —— 这一步是关键，
///      玩家看到自己写的词被复述，就会相信它真的听懂了自己说的话
///   3. 接一句符合物种性格的反应（话痨系的会反悔，安静系的会沉默，贪吃的会跑题）
///
/// 一个刻意的设计：反应不承诺「我会做到」。会反悔、会讨价还价的台词比
/// 「好的主人我会乖的」可爱得多 —— 后者是客服，前者才是个活的东西。
/// </summary>
public static class PetVoice
{
    private class KeywordRule
    {
        public string[] keys;      // 命中任一即算
        public string[] replies;   // 该情境下的反应池
    }

    /// <summary>
    /// 关键词规则表。顺序即优先级：先命中的先用。
    /// 注意 keys 里不要放太短或太常见的字（比如"要"、"我"），否则几乎必中，规则就失效了。
    /// </summary>
    private static readonly KeywordRule[] Rules =
    {
        // ---- 陪伴：最常被写下的愿望，放在第一优先 ----
        new KeywordRule {
            keys = new[] { "陪着我", "陪着", "陪伴", "一起", "孤单", "一个人", "身边", "不离开" },
            replies = new[] {
                "这可是一辈子的事。你确定吗？",
                "行吧。不过我打呼噜，先跟你说好。",
                "嗯。那就从今天开始算。",
            }
        },

        // ---- 安静 ----
        new KeywordRule {
            keys = new[] { "安静", "不说话", "别吵", "清净", "沉默", "话少" },
            replies = new[] {
                "那我闭嘴。……好了，忍不住了，你好。",
                "我尽量。但我没什么把握。",
                "我听见了。这是最后一句。",
            }
        },

        // ---- 吃 ----
        new KeywordRule {
            keys = new[] { "吃", "好吃", "饿", "零食", "食物", "鸡腿" },
            replies = new[] {
                "你这话说得我饿了。",
                "先说好，我不吃素的。",
                "这个字我记住了，写在最前面。",
            }
        },

        // ---- 睡 / 懒 ----
        new KeywordRule {
            keys = new[] { "睡觉", "困", "晚安", "懒", "躺", "打盹" },
            replies = new[] {
                "这愿望我不用努力就能实现。",
                "我们是同类。",
                "那你别半夜叫醒我，说好了。",
            }
        },

        // ---- 温柔 / 乖 ----
        new KeywordRule {
            keys = new[] { "温柔", "听话", "乖", "软", "暖", "体贴" },
            replies = new[] {
                "那你也得对我温柔点。",
                "我先答应。能不能做到，另说。",
                "成交。不过要加一个条件：不许忘了我。",
            }
        },

        // ---- 活泼 / 玩 ----
        new KeywordRule {
            keys = new[] { "活泼", "热闹", "爱玩", "一起玩", "蹦", "闹", "运动" },
            replies = new[] {
                "那你可得跟上我。",
                "好。从明天早上六点开始。",
                "先把你的手伸过来。",
            }
        },

        // ---- 好看 ----
        new KeywordRule {
            keys = new[] { "好看", "漂亮", "可爱", "美", "帅气" },
            replies = new[] {
                "你眼光不错。",
                "我知道。你不用说出来。",
                "这话我收下了，不退。",
            }
        },

        // ---- 保护 / 怕 ----
        new KeywordRule {
            keys = new[] { "保护", "守护", "安全", "害怕", "怕黑", "勇敢" },
            replies = new[] {
                "我现在还小。但这句话我记住了。",
                "怕什么？我在这儿。虽然我也挺小的。",
                "那你睡觉的时候，我睡门口。",
            }
        },

        // ---- 长大 / 变强 ----
        new KeywordRule {
            keys = new[] { "长大", "变强", "厉害", "强壮" },
            replies = new[] {
                "急什么。我还没学会走路。",
                "行。那你别半路把我弄丢了。",
                "长大了我也不会走的。……大概。",
            }
        },
    };

    /// <summary>没命中任何关键词时的兜底台词：不假装听懂，但承认"我一直在听"</summary>
    private static readonly string[] DefaultReplies =
    {
        "你说的每一个字，我在蛋壳里都听见了。",
        "我想了想你说的那些。……我还需要一点时间。",
        "蛋壳挺闷的。我一直听着你说话，才熬过来。",
    };

    /// <summary>
    /// 生成破壳第一句话。
    /// 命中关键词 → 「原话片段」 + 反应；没命中 → 兜底台词 + 物种自述。
    /// 返回值一定非空（最差也会给兜底台词）。
    /// </summary>
    public static string FirstLine(PetSpecies.Def def, string wish)
    {
        string w = wish != null ? wish.Trim() : "";

        if (w.Length > 0)
        {
            for (int r = 0; r < Rules.Length; r++)
            {
                string hit = FindKey(w, Rules[r].keys);
                if (hit == null) continue;

                string reply = Rules[r].replies[Random.Range(0, Rules[r].replies.Length)];
                // 「引号原样复述」是这个效果成立的关键，不要省掉
                return "「" + hit + "」？……" + reply;
            }
        }

        string fallback = DefaultReplies[Random.Range(0, DefaultReplies.Length)];
        return fallback;
    }

    /// <summary>在愿望里找第一个出现的关键词；找不到返回 null</summary>
    private static string FindKey(string wish, string[] keys)
    {
        for (int i = 0; i < keys.Length; i++)
            if (wish.Contains(keys[i])) return keys[i];
        return null;
    }

    /// <summary>
    /// 名字建议（备用，暂时没用上）：如果以后要做"它自己说了个名字"，
    /// 可以从物种 traitWords 拼一个。保留接口，避免以后重新设计。
    /// </summary>
    public static string SuggestName(PetSpecies.Def def)
    {
        if (def == null) return "小家伙";
        string[] parts = def.traitWords.Split(',');
        if (parts.Length == 0) return def.displayName;
        return parts[0] + "的" + def.displayName;
    }
}
