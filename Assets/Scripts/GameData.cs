using System;
using UnityEngine;

/// <summary>
/// 游戏数据底座（阶段二任务5）：单例 + PlayerPrefs 存档 + 在线/离线衰减。
/// 本单只含数据与存档，不含任何房间交互逻辑（喂食/洗澡/睡觉等属后续工单）。
/// 衰减规则（工单指定）：在线每秒 hunger-0.01 / health-0.005 / mood-0.005；
/// 离线按离线秒数 × 同样速率，上限 8 小时；所有状态钳制在 0~100。
/// </summary>
public class GameData : MonoBehaviour
{
    public static GameData Instance { get; private set; }

    // ---- 数据字段（初始值为工单指定）----
    public float hunger = 100f;          // 饥饿 0~100
    public float health = 100f;          // 健康 0~100
    public float mood = 100f;            // 心情 0~100
    public long coins = 100000000L;      // 金币：一个亿
    public long lastSaveTimeTicks;      // 上次退出时间戳（UTC Ticks）

    // ---- 商店字段（阶段三任务10，纯字符串存档，向后兼容：无存档时为空串）----
    public string ownedItems = "";       // 逗号分隔的已拥有商品 id，如 "hat_red,scarf_blue"
    public string equippedHat = "";      // 已装备帽子 id（空 = 未装备）
    public string equippedScarf = "";   // 已装备围巾 id
    public string equippedGlasses = ""; // 已装备墨镜 id

    // ---- 进化字段（阶段三任务11）----
    public long careScore = 0;        // 照顾质量分，每次互动成功 +10
    public long birthTimeTicks = 0;  // 出生时间戳（首次启动在 Awake 初始化，DateTime.Now）
    public int debugDayOffset = 0;   // 仅测试用：天数偏移，交付时必须为 0

    // ---- 签到字段（阶段三任务14）----
    public long lastSignInDateTicks = 0; // 上次签到日的本地日期（当天 0 点，DateTime.Today.Ticks；0=从未签到）
    public int signInStreak = 0;         // 当前连签天数（1~7 一周一轮；0=从未签到/旧档无此键）

    // ---- 成就字段（阶段四任务15A）----
    public int feedCount = 0;                 // 累计喂食次数
    public int bathCount = 0;                 // 累计洗澡次数
    public int yardCount = 0;                 // 累计逗鸡次数
    public int sleepCount = 0;                // 累计睡觉次数
    public string unlockedAchievements = "";  // 已解锁成就 id，逗号分隔（空串 = 尚无成就）
    private string pendingAchievementMessage = ""; // 待提示的成就解锁消息缓存（任务15D，不存档；ConsumeAchievementMessage 取走即清空）

    // ---- 碎碎念字段（阶段四任务16A）----
    public string diaryEntries = "";       // 碎碎念记录："日期|内容;日期|内容"，最多保留最近 30 条（空串 = 尚无记录）

    // ---- 衰减速率与离线上限（工单指定）----
    private const float HungerDecayPerSec = 0.01f;
    private const float HealthDecayPerSec = 0.005f;
    private const float MoodDecayPerSec = 0.005f;
    private const float MaxOfflineSeconds = 8f * 3600f; // 离线最多按 8 小时计

    // ---- PlayerPrefs 键 ----
    private const string KeyHunger = "pp_hunger";
    private const string KeyHealth = "pp_health";
    private const string KeyMood = "pp_mood";
    private const string KeyCoins = "pp_coins";
    private const string KeyTicks = "pp_lastSaveTimeTicks";
    private const string KeyOwned = "pp_ownedItems";           // 任务10
    private const string KeyEquippedHat = "pp_equippedHat";     // 任务10
    private const string KeyEquippedScarf = "pp_equippedScarf"; // 任务10
    private const string KeyEquippedGlasses = "pp_equippedGlasses"; // 任务10
    private const string KeyCareScore = "pp_careScore";           // 任务11
    private const string KeyBirthTicks = "pp_birthTimeTicks";     // 任务11
    private const string KeyDebugDayOffset = "pp_debugDayOffset"; // 任务11
    private const string KeyLastSignInDate = "pp_lastSignInDateTicks"; // 任务14
    private const string KeySignInStreak = "pp_signInStreak";           // 任务14
    private const string KeyFeedCount = "pp_feedCount";                 // 任务15A
    private const string KeyBathCount = "pp_bathCount";                 // 任务15A
    private const string KeyYardCount = "pp_yardCount";                 // 任务15A
    private const string KeySleepCount = "pp_sleepCount";               // 任务15A
    private const string KeyUnlockedAchievements = "pp_unlockedAchievements"; // 任务15A
    private const string KeyDiaryEntries = "pp_diaryEntries";                // 任务16A

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        Load();                 // 先读档
        if (birthTimeTicks <= 0) birthTimeTicks = DateTime.Now.Ticks; // 首次启动初始化出生时间（任务11）
        ApplyOfflineDecay();    // 再按离线时长扣减
    }

    private void Update()
    {
        // 在线衰减（帧率无关）
        hunger = Mathf.Max(0f, hunger - HungerDecayPerSec * Time.deltaTime);
        health = Mathf.Max(0f, health - HealthDecayPerSec * Time.deltaTime);
        mood = Mathf.Max(0f, mood - MoodDecayPerSec * Time.deltaTime);
    }

    /// <summary>存档：全部字段写入 PlayerPrefs，并记录存档时刻</summary>
    public void Save()
    {
        PlayerPrefs.SetFloat(KeyHunger, hunger);
        PlayerPrefs.SetFloat(KeyHealth, health);
        PlayerPrefs.SetFloat(KeyMood, mood);
        PlayerPrefs.SetString(KeyCoins, coins.ToString());
        lastSaveTimeTicks = DateTime.UtcNow.Ticks;
        PlayerPrefs.SetString(KeyTicks, lastSaveTimeTicks.ToString());
        PlayerPrefs.SetString(KeyOwned, ownedItems);
        PlayerPrefs.SetString(KeyEquippedHat, equippedHat);
        PlayerPrefs.SetString(KeyEquippedScarf, equippedScarf);
        PlayerPrefs.SetString(KeyEquippedGlasses, equippedGlasses);
        PlayerPrefs.SetString(KeyCareScore, careScore.ToString());
        PlayerPrefs.SetString(KeyBirthTicks, birthTimeTicks.ToString());
        PlayerPrefs.SetInt(KeyDebugDayOffset, debugDayOffset);
        PlayerPrefs.SetString(KeyLastSignInDate, lastSignInDateTicks.ToString()); // 任务14：long 沿用字符串存档风格
        PlayerPrefs.SetInt(KeySignInStreak, signInStreak);
        PlayerPrefs.SetInt(KeyFeedCount, feedCount);               // 任务15A
        PlayerPrefs.SetInt(KeyBathCount, bathCount);
        PlayerPrefs.SetInt(KeyYardCount, yardCount);
        PlayerPrefs.SetInt(KeySleepCount, sleepCount);
        PlayerPrefs.SetString(KeyUnlockedAchievements, unlockedAchievements);
        PlayerPrefs.SetString(KeyDiaryEntries, diaryEntries);          // 任务16A：碎碎念存档
        PlayerPrefs.Save();
    }

    /// <summary>读档：无存档时保持字段初始值（旧存档缺商店键时保持空串，向后兼容）</summary>
    public void Load()
    {
        hunger = PlayerPrefs.GetFloat(KeyHunger, hunger);
        health = PlayerPrefs.GetFloat(KeyHealth, health);
        mood = PlayerPrefs.GetFloat(KeyMood, mood);
        if (long.TryParse(PlayerPrefs.GetString(KeyCoins, ""), out var c)) coins = c;
        if (long.TryParse(PlayerPrefs.GetString(KeyTicks, ""), out var t)) lastSaveTimeTicks = t;
        ownedItems = PlayerPrefs.GetString(KeyOwned, ownedItems);
        equippedHat = PlayerPrefs.GetString(KeyEquippedHat, equippedHat);
        equippedScarf = PlayerPrefs.GetString(KeyEquippedScarf, equippedScarf);
        equippedGlasses = PlayerPrefs.GetString(KeyEquippedGlasses, equippedGlasses);
        if (long.TryParse(PlayerPrefs.GetString(KeyCareScore, ""), out var cs)) careScore = cs;
        if (long.TryParse(PlayerPrefs.GetString(KeyBirthTicks, ""), out var bt)) birthTimeTicks = bt;
        debugDayOffset = PlayerPrefs.GetInt(KeyDebugDayOffset, debugDayOffset); // 旧档缺键→保持 0
        if (long.TryParse(PlayerPrefs.GetString(KeyLastSignInDate, ""), out var lsd)) lastSignInDateTicks = lsd; // 旧档缺键→保持 0
        signInStreak = PlayerPrefs.GetInt(KeySignInStreak, signInStreak); // 旧档缺键→保持 0
        feedCount = PlayerPrefs.GetInt(KeyFeedCount, feedCount);                     // 旧档缺键→保持 0（任务15A）
        bathCount = PlayerPrefs.GetInt(KeyBathCount, bathCount);                     // 旧档缺键→保持 0
        yardCount = PlayerPrefs.GetInt(KeyYardCount, yardCount);                     // 旧档缺键→保持 0
        sleepCount = PlayerPrefs.GetInt(KeySleepCount, sleepCount);                 // 旧档缺键→保持 0
        unlockedAchievements = PlayerPrefs.GetString(KeyUnlockedAchievements, unlockedAchievements); // 旧档缺键→保持空串
        diaryEntries = PlayerPrefs.GetString(KeyDiaryEntries, diaryEntries); // 旧档缺键→保持空串（任务16A）
    }

    /// <summary>离线衰减：按上次存档至今的秒数 × 同速率扣除，最多按 8 小时计</summary>
    public void ApplyOfflineDecay()
    {
        if (lastSaveTimeTicks <= 0) return; // 从未存档（首次进入）不衰减
        double offlineSeconds = (DateTime.UtcNow.Ticks - lastSaveTimeTicks) / (double)TimeSpan.TicksPerSecond;
        if (offlineSeconds <= 0) return;
        double capped = Math.Min(offlineSeconds, MaxOfflineSeconds);
        hunger = Mathf.Max(0f, (float)(hunger - HungerDecayPerSec * capped));
        health = Mathf.Max(0f, (float)(health - HealthDecayPerSec * capped));
        mood = Mathf.Max(0f, (float)(mood - MoodDecayPerSec * capped));
    }

    // 退出 / 失焦时自动存档（编辑器停止 Play 也会触发 OnApplicationQuit）
    private void OnApplicationQuit() { Save(); }
    private void OnApplicationPause(bool pause) { if (pause) Save(); }

    // ---------------- 进化计算（阶段三任务11） ----------------

    /// <summary>当前天数：出生至今的自然日数 + 测试偏移，最小 0</summary>
    public int GetCurrentDay()
    {
        long day = 0;
        if (birthTimeTicks > 0)
            day = (DateTime.Now.Ticks - birthTimeTicks) / TimeSpan.TicksPerDay;
        return Mathf.Max(0, (int)day + debugDayOffset);
    }

    /// <summary>成长阶段：Day0 蛋 / Day1~2 幼鸡 / Day3~6 少年鸡 / Day7+ 成年鸡</summary>
    public string GetCurrentStage()
    {
        int day = GetCurrentDay();
        if (day <= 0) return "蛋";
        if (day <= 2) return "幼鸡";
        if (day <= 6) return "少年鸡";
        return "成年鸡";
    }

    /// <summary>成年形态：careScore≥300 漂亮鸡 / ≥150 普通鸡 / 其余 邋遢鸡</summary>
    public string GetFinalForm()
    {
        if (careScore >= 300) return "漂亮鸡";
        if (careScore >= 150) return "普通鸡";
        return "邋遢鸡";
    }

    // ---------------- 签到（阶段三任务14） ----------------

    /// <summary>今天是否还没签到（本地日期对比：lastSignInDateTicks==今天0点 视为已签；0=从未签到→可签）</summary>
    public bool CanSignInToday()
    {
        return lastSignInDateTicks != DateTime.Today.Ticks;
    }

    /// <summary>今天签到会得的金币（第 7 天连签 = 10万+100万，其余 = 10万），供 UI 显示，不改动任何状态</summary>
    public int GetTodayReward()
    {
        long yesterday = DateTime.Today.AddDays(-1).Ticks;
        int streakToday = (lastSignInDateTicks == yesterday) ? signInStreak + 1 : 1; // 今天若签到将处的连签天数：昨天签过→+1，首次/断签→1
        if (streakToday > 7) streakToday = 1; // 一周一轮：第 8 天回到第 1 天
        return streakToday >= 7 ? 100000 + 1000000 : 100000;
    }

    /// <summary>执行签到：连签判定（昨天签过→+1，否则→1；满 7 后新一轮回 1）、记录今天 0 点、金币入账、立即存档；返回本次奖励（当天已签返回 0，不重复发奖）</summary>
    public int DoSignIn()
    {
        if (!CanSignInToday()) return 0; // 防重复：当天已签
        int reward = GetTodayReward();     // 须在改动连签状态之前计算本次奖励
        long today = DateTime.Today.Ticks;
        long yesterday = DateTime.Today.AddDays(-1).Ticks;
        signInStreak = (lastSignInDateTicks == yesterday) ? signInStreak + 1 : 1; // 昨天签过→连签+1；首次/断签→1
        if (signInStreak > 7) signInStreak = 1; // 一周一轮：第 8 天回到第 1 天
        lastSignInDateTicks = today;
        coins += reward;
        Save(); // 立即存档
        CheckAndGrantAchievements(); // 任务15B：签到后检测并发放成就（sign_7 全勤奖）
        return reward;
    }

    // ---------------- 成就（阶段四任务15A） ----------------

    /// <summary>累计喂食次数 +1（任务15B：+1 后检测并发放成就）</summary>
    public void AddFeedCount() { feedCount += 1; CheckAndGrantAchievements(); }

    /// <summary>累计洗澡次数 +1（任务15B：+1 后检测并发放成就）</summary>
    public void AddBathCount() { bathCount += 1; CheckAndGrantAchievements(); }

    /// <summary>累计逗鸡次数 +1（任务15B：+1 后检测并发放成就）</summary>
    public void AddYardCount() { yardCount += 1; CheckAndGrantAchievements(); }

    /// <summary>累计睡觉次数 +1（任务15B：+1 后检测并发放成就）</summary>
    public void AddSleepCount() { sleepCount += 1; CheckAndGrantAchievements(); }

    /// <summary>成就是否已解锁：unlockedAchievements 按逗号拆分精确比对；空串（尚无成就）返回 false</summary>
    public bool IsAchievementUnlocked(string id)
    {
        if (unlockedAchievements == "") return false; // 空串 = 尚无任何成就
        foreach (var s in unlockedAchievements.Split(','))
        {
            if (s == id) return true;
        }
        return false;
    }

    /// <summary>解锁成就：已解锁直接返回；否则追加到 unlockedAchievements（空串则直接赋值）。不自动存档、不发奖励（由后续工单决定）</summary>
    public void UnlockAchievement(string id)
    {
        if (IsAchievementUnlocked(id)) return; // 已解锁，不重复
        unlockedAchievements = (unlockedAchievements == "") ? id : unlockedAchievements + "," + id;
    }

    // ---------------- 成就定义与发放（阶段四任务15B） ----------------

    /// <summary>成就定义（任务15B）：id 唯一标识 / name 显示名 / target 达成所需次数 / reward 解锁时发放的金币</summary>
    public class AchievementDef
    {
        public string id;     // 成就 id，如 "feed_10"
        public string name;   // 显示名，如 "新手饲养员"
        public int target;    // 达成所需次数
        public long reward;   // 解锁时的金币奖励
    }

    /// <summary>全部成就定义表（任务15B，6 项）：喂食×2 / 洗澡 / 逗鸡 / 睡觉 / 签到</summary>
    public static readonly AchievementDef[] AchievementDefs =
    {
        new AchievementDef { id = "feed_10",  name = "新手饲养员", target = 10,  reward = 10000L },
        new AchievementDef { id = "feed_100", name = "金牌饲养员", target = 100, reward = 100000L },
        new AchievementDef { id = "bath_10",  name = "爱干净",     target = 10,  reward = 10000L },
        new AchievementDef { id = "yard_10",  name = "游戏伙伴",   target = 10,  reward = 10000L },
        new AchievementDef { id = "sleep_10", name = "安睡天使",   target = 10,  reward = 10000L },
        new AchievementDef { id = "sign_7",   name = "全勤奖",     target = 7,   reward = 100000L },
    };

    /// <summary>成就当前进度（任务15B；任务15C 经老板改单由 private 转 public 供 UI 直调）：id → 对应计数字段；未知 id 返回 0</summary>
    public int GetAchievementProgress(string id)
    {
        switch (id)
        {
            case "feed_10":
            case "feed_100": return feedCount;
            case "bath_10":  return bathCount;
            case "yard_10":  return yardCount;
            case "sleep_10": return sleepCount;
            case "sign_7":   return signInStreak;
            default:         return 0;
        }
    }

    /// <summary>检测并发放成就（任务15B）：遍历定义表，已解锁跳过；未解锁且进度≥target → UnlockAchievement、金币入账、立即存档；一次可解锁多个，循环不提前退出</summary>
    private void CheckAndGrantAchievements()
    {
        foreach (var def in AchievementDefs)
        {
            if (IsAchievementUnlocked(def.id)) continue;
            if (GetAchievementProgress(def.id) < def.target) continue;
            UnlockAchievement(def.id);
            coins += def.reward;
            Save();
            // 任务15D：解锁时缓存提示消息供 UI 反馈文本显示（多个用"；"分隔；解锁逻辑本身不变）
            string oneMessage = "成就达成：" + def.name + " +" + def.reward + "金币";
            pendingAchievementMessage = (pendingAchievementMessage == "") ? oneMessage : pendingAchievementMessage + "；" + oneMessage;
        }
    }

    /// <summary>取走待提示的成就解锁消息并清空缓存（任务15D；无消息时返回空串）</summary>
    public string ConsumeAchievementMessage()
    {
        string message = pendingAchievementMessage;
        pendingAchievementMessage = "";
        return message;
    }

    // ---------------- 碎碎念（阶段四任务16A） ----------------

    /// <summary>今天是否已签到（任务16A）：CanSignInToday 的反义，复用原判定，不改签到逻辑</summary>
    public bool HasSignedInToday()
    {
        return !CanSignInToday();
    }

    /// <summary>
    /// 追加一条碎碎念（任务16A）：空/空白输入返回 -1，不记录不发奖；超 50 字截断；
    /// 按 "yyyy-MM-dd|内容" 组装并以 ";" 追加，只保留最近 30 条；
    /// 每天第一条碎碎念触发签到奖励（当天已签则奖励为 0），最后 Save 并返回本次奖励。
    /// </summary>
    public long AddDiaryEntry(string content)
    {
        if (content == null || content.Trim().Length == 0) return -1; // 无效输入：不记录、不发奖
        if (content.Length > 50) content = content.Substring(0, 50);  // 超长截断到 50 字
        string entry = DateTime.Today.ToString("yyyy-MM-dd") + "|" + content;
        diaryEntries = (diaryEntries == "") ? entry : diaryEntries + ";" + entry;
        string[] parts = diaryEntries.Split(';');
        if (parts.Length > 30) // 超出 30 条：丢弃最旧的，保留最后 30 条重新拼接
            diaryEntries = string.Join(";", parts, parts.Length - 30, 30);
        long reward = CanSignInToday() ? DoSignIn() : 0; // 每天第一条碎碎念 = 当天签到，拿签到奖励
        Save();
        return reward;
    }

    /// <summary>返回碎碎念原始字符串（任务16A，格式 "日期|内容;日期|内容"，供 UI 解析）</summary>
    public string GetDiaryEntries()
    {
        return diaryEntries;
    }
}
