using System;
using UnityEngine;

/// <summary>
/// 游戏数据底座（阶段二任务5）：单例 + PlayerPrefs 存档 + 在线/离线衰减。
/// 本单只含数据与存档，不含任何房间交互逻辑（喂食/洗澡/睡觉等属后续工单）。
/// 衰减规则（任务17D/17F，速率基准=清醒日 14 小时=50400 秒）：hunger 自然 -20/清醒日 + 晨扣 30/天（合计 50/天，17F 平衡设计）；
/// clean -30/清醒日；mood 分 5 档（≥75/≥55/≥40/≥30/其余 → 25/20/15/10/5 每清醒日）× 疲劳倍率（连续醒 >16h×1.5、>24h×2；睡着清零）；
/// health 为结果指标（互动不再直接加）：衰减=基础 2 + 心情<60 线性(满14) + 饥饿≤0 加 23 + 清洁<40 线性(满10)，每清醒日；
/// 三状态（hunger/mood/clean）均 ≥60 时 health 自然恢复 +5/清醒日；
/// 睡觉（17F）：白天 6:00–20:00 恒醒；晚上默认睡（全值不掉），夜间互动唤醒 5 分钟（不叠加）；离线晚上一律睡；
/// 离线按小时迭代，上限 60 天；衰减下限钳 0。
/// 任务17E：health≤0 即死亡（isDead 即时存档持久化，在线/离线全停；领养新蛋 ReviveAsEgg 重置回蛋）。
/// </summary>
public class GameData : MonoBehaviour
{
    public static GameData Instance { get; private set; }

    // ---- 数据字段（初始值为工单指定）----
    public float hunger = 100f;          // 饥饿 0~100
    public float health = 100f;          // 健康 0~100
    public float mood = 100f;            // 心情 0~100
    public float clean = 100f;           // 清洁 0~100（任务17D）
    public long coins = 100000000L;      // 金币：一个亿
    public long lastSaveTimeTicks;      // 上次退出时间戳（UTC Ticks）
    public bool isDead = false;          // 死亡标记（任务17E：health≤0 置位，重启不复活；领蛋重置清零）

    // ---- 商店字段（阶段三任务10，纯字符串存档，向后兼容：无存档时为空串）----
    public string ownedItems = "";       // 逗号分隔的已拥有商品 id，如 "hat_red,scarf_blue"
    public string equippedHat = "";      // 已装备帽子 id（空 = 未装备）
    public string equippedScarf = "";   // 已装备围巾 id
    public string equippedGlasses = ""; // 已装备墨镜 id

    // ---- 进化字段（阶段三任务11；17G 起只剩出生时间——照顾质量分体系已删）----
    public long birthTimeTicks = 0;  // 出生时间戳（首次启动在 Awake 初始化，DateTime.Now）

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

    // ---- 疲劳与睡觉（任务17D/17F）----
    private long awakeSinceTicks = 0;   // 连续醒着起点（Awake 设；睡着清零、醒着重启，不存档）
    private long nightAwakeUntilTicks = 0;  // 晚上唤醒截止（UtcTicks），0 或已过 = 睡着（任务17F，存档）
    private long lastMorningDropDateTicks = 0;  // 上次"早 6 点扣 hunger"的本地日期（Today.Ticks；任务17F，存档）

    // ---- 抚摸小鸡（任务17F-补丁，存档）----
    private int patCountToday = 0;      // 今日已抚摸次数（跨天由 PatChick 自动清零）
    private long lastPatDateTicks = 0;  // 上次抚摸日期（Today.Ticks）

    // ---- 衰减速率与作息（任务17D/17F，每秒 = 每清醒日/50400；清醒日=14 小时=白天 6:00–20:00）----
    private const float HungerDecayPerSec = 20f / 50400f; // 任务17F：50→20（一天自然 20 + 晨扣 30 = 50，保原平衡）
    private const float CleanDecayPerSec = 30f / 50400f;
    private const float HealthBaseDecayPerSec = 2f / 50400f;
    private const float HealthMoodPenaltyMaxPerSec = 14f / 50400f;
    private const float HealthHungerPenaltyPerSec = 23f / 50400f;
    private const float HealthCleanPenaltyMaxPerSec = 10f / 50400f;
    private const float HealthRecoverPerSec = 5f / 50400f;
    private const int SleepStartHour = 20;
    private const int SleepEndHour = 6;
    private const float MorningHungerDrop = 30f;  // 任务17F：早 6 点后一次性晨扣（每天一次）
    private const int NightAwakeMinutes = 5;      // 任务17F：夜间互动唤醒分钟数（每次重置，不叠加）
    private const int PatDailyLimit = 3;          // 任务17F-补丁：每天抚摸上限

    // ---- 疲劳阈值与倍率（任务17D，连续在线时长）----
    private const double FatigueThreshold1Hours = 16.0;
    private const double FatigueThreshold2Hours = 24.0;
    private const float FatigueMult1 = 1.5f;
    private const float FatigueMult2 = 2f;

    // ---- PlayerPrefs 键 ----
    private const string KeyHunger = "pp_hunger";
    private const string KeyHealth = "pp_health";
    private const string KeyMood = "pp_mood";
    private const string KeyClean = "pp_clean";                  // 任务17D
    private const string KeyCoins = "pp_coins";
    private const string KeyTicks = "pp_lastSaveTimeTicks";
    private const string KeyOwned = "pp_ownedItems";           // 任务10
    private const string KeyEquippedHat = "pp_equippedHat";     // 任务10
    private const string KeyEquippedScarf = "pp_equippedScarf"; // 任务10
    private const string KeyEquippedGlasses = "pp_equippedGlasses"; // 任务10
    private const string KeyBirthTicks = "pp_birthTimeTicks";     // 任务11
    private const string KeyLastSignInDate = "pp_lastSignInDateTicks"; // 任务14
    private const string KeySignInStreak = "pp_signInStreak";           // 任务14
    private const string KeyFeedCount = "pp_feedCount";                 // 任务15A
    private const string KeyBathCount = "pp_bathCount";                 // 任务15A
    private const string KeyYardCount = "pp_yardCount";                 // 任务15A
    private const string KeySleepCount = "pp_sleepCount";               // 任务15A
    private const string KeyUnlockedAchievements = "pp_unlockedAchievements"; // 任务15A
    private const string KeyDiaryEntries = "pp_diaryEntries";                // 任务16A
    private const string KeyIsDead = "pp_isDead";                          // 任务17E
    private const string KeyNightAwakeUntil = "pp_nightAwakeUntilTicks";   // 任务17F
    private const string KeyLastMorningDrop = "pp_lastMorningDropDateTicks"; // 任务17F
    private const string KeyPatCountToday = "pp_patCountToday";             // 任务17F-补丁
    private const string KeyLastPatDate = "pp_lastPatDateTicks";            // 任务17F-补丁

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        Load();                 // 先读档
        awakeSinceTicks = DateTime.UtcNow.Ticks; // 任务17D：连续在线起点（疲劳计时，不存档）
        if (lastMorningDropDateTicks == 0)
            lastMorningDropDateTicks = DateTime.Today.Ticks; // 任务17F：首次启动当天不扣，次日 6 点起扣
        if (birthTimeTicks <= 0) birthTimeTicks = DateTime.Now.Ticks; // 首次启动初始化出生时间（任务11）
        ApplyOfflineDecay();    // 再按离线时长扣减
    }

    /// <summary>心情分段衰减速率（任务17D）：≥75→25，≥55→20，≥40→15，≥30→10，其余 5（每清醒日，除以 50400 换算每秒）</summary>
    private static float GetMoodDecayPerSec(float mood)
    {
        if (mood >= 75f) return 25f / 50400f;
        if (mood >= 55f) return 20f / 50400f;
        if (mood >= 40f) return 15f / 50400f;
        if (mood >= 30f) return 10f / 50400f;
        return 5f / 50400f;
    }

    /// <summary>健康衰减速率（任务17D）：基础 2 + mood&lt;60 按 (60-mood)/60 线性（满 14）+ hunger≤0 加 23 + clean&lt;40 按 (40-clean)/40 线性（满 10），每清醒日</summary>
    private static float GetHealthDecayPerSec(float hunger, float mood, float clean)
    {
        float rate = HealthBaseDecayPerSec;
        if (mood < 60f) rate += (60f - mood) / 60f * HealthMoodPenaltyMaxPerSec;
        if (hunger <= 0f) rate += HealthHungerPenaltyPerSec;
        if (clean < 40f) rate += (40f - clean) / 40f * HealthCleanPenaltyMaxPerSec;
        return rate;
    }

    /// <summary>健康自然恢复条件（任务17D）：hunger/mood/clean 三状态均 ≥60</summary>
    private static bool IsHealthRecovering(float hunger, float mood, float clean)
    {
        return hunger >= 60f && mood >= 60f && clean >= 60f;
    }

    /// <summary>当前是否睡着（任务17F）：白天恒醒；晚上在唤醒窗口（nightAwakeUntilTicks）内为醒，否则睡</summary>
    private bool IsSleepingNow()
    {
        int h = DateTime.Now.Hour;
        bool isNight = h >= SleepStartHour || h < SleepEndHour;
        if (!isNight) return false;
        return DateTime.UtcNow.Ticks >= nightAwakeUntilTicks;
    }

    /// <summary>夜间互动唤醒（任务17F）：晚上任意互动重置 5 分钟醒窗（不叠加）；白天调用无效果</summary>
    public void NotifyInteraction()
    {
        int h = DateTime.Now.Hour;
        bool isNight = h >= SleepStartHour || h < SleepEndHour;
        if (!isNight) return;
        nightAwakeUntilTicks = DateTime.UtcNow.Ticks + TimeSpan.TicksPerMinute * NightAwakeMinutes;
    }

    /// <summary>早 6 点晨扣（任务17F）：每天首个白天帧扣 hunger 30 并记日期；夜里或当天已扣则跳过</summary>
    private void CheckMorningDrop()
    {
        long today = DateTime.Today.Ticks;
        if (lastMorningDropDateTicks == today) return;
        int h = DateTime.Now.Hour;
        if (h < SleepEndHour || h >= SleepStartHour) return; // 不在白天区间，不扣
        hunger = Mathf.Max(0f, hunger - MorningHungerDrop);
        lastMorningDropDateTicks = today;
    }

    /// <summary>连续醒着疲劳倍率（任务17D/17F）：醒着累计 &gt;24h→×2、&gt;16h→×1.5，其余 ×1；睡着清零计时由 Update 睡着分支执行</summary>
    private float GetFatigueMultiplier()
    {
        if (IsSleepingNow()) { awakeSinceTicks = 0; return 1f; }
        if (awakeSinceTicks <= 0) awakeSinceTicks = DateTime.UtcNow.Ticks;
        double hours = (DateTime.UtcNow.Ticks - awakeSinceTicks) / (double)TimeSpan.TicksPerHour;
        if (hours >= FatigueThreshold2Hours) return FatigueMult2;
        if (hours >= FatigueThreshold1Hours) return FatigueMult1;
        return 1f;
    }

    private void Update()
    {
        if (isDead) return; // 死亡期间停止一切衰减与恢复
        CheckMorningDrop(); // 任务17F：每天 6 点后第一次触发扣食物；睡着时也调（去重靠 lastMorningDropDateTicks）

        if (IsSleepingNow()) { awakeSinceTicks = 0; return; } // 睡着：不衰减 + 疲劳计时清零（17F 一行修复，老板已批）

        // 在线衰减（帧率无关；任务17D：疲劳倍率作用于 mood/health、三状态≥60 健康自然恢复）
        float dt = Time.deltaTime;
        float fatigue = GetFatigueMultiplier();
        hunger = Mathf.Max(0f, hunger - HungerDecayPerSec * dt);
        clean = Mathf.Max(0f, clean - CleanDecayPerSec * dt);
        mood = Mathf.Max(0f, mood - GetMoodDecayPerSec(mood) * dt * fatigue);
        if (IsHealthRecovering(hunger, mood, clean))
            health = Mathf.Min(100f, health + HealthRecoverPerSec * dt);
        else
            health = Mathf.Max(0f, health - GetHealthDecayPerSec(hunger, mood, clean) * dt * fatigue);

        CheckDeath();
    }

    /// <summary>死亡判定（任务17E）：未死且 health≤0 → 置 isDead 并立即存档（任务17F 起即时落盘，防强杀丢标记）</summary>
    private void CheckDeath()
    {
        if (!isDead && health <= 0f) { isDead = true; Save(); }
    }

    /// <summary>存档：全部字段写入 PlayerPrefs，并记录存档时刻</summary>
    public void Save()
    {
        PlayerPrefs.SetFloat(KeyHunger, hunger);
        PlayerPrefs.SetFloat(KeyHealth, health);
        PlayerPrefs.SetFloat(KeyMood, mood);
        PlayerPrefs.SetFloat(KeyClean, clean);                  // 任务17D
        PlayerPrefs.SetString(KeyCoins, coins.ToString());
        lastSaveTimeTicks = DateTime.UtcNow.Ticks;
        PlayerPrefs.SetString(KeyTicks, lastSaveTimeTicks.ToString());
        PlayerPrefs.SetString(KeyOwned, ownedItems);
        PlayerPrefs.SetString(KeyEquippedHat, equippedHat);
        PlayerPrefs.SetString(KeyEquippedScarf, equippedScarf);
        PlayerPrefs.SetString(KeyEquippedGlasses, equippedGlasses);
        PlayerPrefs.SetString(KeyBirthTicks, birthTimeTicks.ToString());
        PlayerPrefs.SetString(KeyLastSignInDate, lastSignInDateTicks.ToString()); // 任务14：long 沿用字符串存档风格
        PlayerPrefs.SetInt(KeySignInStreak, signInStreak);
        PlayerPrefs.SetInt(KeyFeedCount, feedCount);               // 任务15A
        PlayerPrefs.SetInt(KeyBathCount, bathCount);
        PlayerPrefs.SetInt(KeyYardCount, yardCount);
        PlayerPrefs.SetInt(KeySleepCount, sleepCount);
        PlayerPrefs.SetString(KeyUnlockedAchievements, unlockedAchievements);
        PlayerPrefs.SetString(KeyDiaryEntries, diaryEntries);          // 任务16A：碎碎念存档
        PlayerPrefs.SetInt(KeyIsDead, isDead ? 1 : 0);                  // 任务17E：死亡标记
        PlayerPrefs.SetString(KeyNightAwakeUntil, nightAwakeUntilTicks.ToString()); // 任务17F：夜间醒窗截止
        PlayerPrefs.SetString(KeyLastMorningDrop, lastMorningDropDateTicks.ToString()); // 任务17F：晨扣日期
        PlayerPrefs.SetInt(KeyPatCountToday, patCountToday);               // 任务17F-补丁：今日抚摸次数
        PlayerPrefs.SetString(KeyLastPatDate, lastPatDateTicks.ToString()); // 任务17F-补丁：抚摸日期
        PlayerPrefs.Save();
    }

    /// <summary>读档：无存档时保持字段初始值（旧存档缺商店键时保持空串，向后兼容）</summary>
    public void Load()
    {
        hunger = PlayerPrefs.GetFloat(KeyHunger, hunger);
        health = PlayerPrefs.GetFloat(KeyHealth, health);
        mood = PlayerPrefs.GetFloat(KeyMood, mood);
        clean = PlayerPrefs.GetFloat(KeyClean, clean); // 旧档缺键→保持 100（任务17D）
        if (long.TryParse(PlayerPrefs.GetString(KeyCoins, ""), out var c)) coins = c;
        if (long.TryParse(PlayerPrefs.GetString(KeyTicks, ""), out var t)) lastSaveTimeTicks = t;
        ownedItems = PlayerPrefs.GetString(KeyOwned, ownedItems);
        equippedHat = PlayerPrefs.GetString(KeyEquippedHat, equippedHat);
        equippedScarf = PlayerPrefs.GetString(KeyEquippedScarf, equippedScarf);
        equippedGlasses = PlayerPrefs.GetString(KeyEquippedGlasses, equippedGlasses);
        if (long.TryParse(PlayerPrefs.GetString(KeyBirthTicks, ""), out var bt)) birthTimeTicks = bt;
        if (long.TryParse(PlayerPrefs.GetString(KeyLastSignInDate, ""), out var lsd)) lastSignInDateTicks = lsd; // 旧档缺键→保持 0
        signInStreak = PlayerPrefs.GetInt(KeySignInStreak, signInStreak); // 旧档缺键→保持 0
        feedCount = PlayerPrefs.GetInt(KeyFeedCount, feedCount);                     // 旧档缺键→保持 0（任务15A）
        bathCount = PlayerPrefs.GetInt(KeyBathCount, bathCount);                     // 旧档缺键→保持 0
        yardCount = PlayerPrefs.GetInt(KeyYardCount, yardCount);                     // 旧档缺键→保持 0
        sleepCount = PlayerPrefs.GetInt(KeySleepCount, sleepCount);                 // 旧档缺键→保持 0
        unlockedAchievements = PlayerPrefs.GetString(KeyUnlockedAchievements, unlockedAchievements); // 旧档缺键→保持空串
        diaryEntries = PlayerPrefs.GetString(KeyDiaryEntries, diaryEntries); // 旧档缺键→保持空串（任务16A）
        isDead = PlayerPrefs.GetInt(KeyIsDead, 0) == 1;                // 旧档缺键→false（任务17E）
        if (long.TryParse(PlayerPrefs.GetString(KeyNightAwakeUntil, ""), out var nau)) nightAwakeUntilTicks = nau; // 旧档缺键→保持 0（任务17F）
        if (long.TryParse(PlayerPrefs.GetString(KeyLastMorningDrop, ""), out var lmd)) lastMorningDropDateTicks = lmd; // 旧档缺键→保持 0（任务17F）
        patCountToday = PlayerPrefs.GetInt(KeyPatCountToday, patCountToday); // 旧档缺键→保持 0（任务17F-补丁）
        if (long.TryParse(PlayerPrefs.GetString(KeyLastPatDate, ""), out var lpd)) lastPatDateTicks = lpd; // 旧档缺键→保持 0（任务17F-补丁）
        mood = Mathf.Min(100f, mood); // 任务17G：心情封顶 100（修旧档超 100，如 1453；食物不封顶）
    }

    /// <summary>离线衰减（任务17F）：按小时迭代，晚上一律睡（不掉值）；每天 6 点后第一个白天 chunk 晨扣 30（与在线同日去重）；上限 60 天；死亡跳过，末尾判死亡（即时存档）</summary>
    public void ApplyOfflineDecay()
    {
        if (isDead || lastSaveTimeTicks <= 0) return; // 死亡期间不衰减 / 从未存档（首次进入）不衰减
        DateTime startUtc = new DateTime(lastSaveTimeTicks, DateTimeKind.Utc);
        DateTime nowUtc = DateTime.UtcNow;
        double offlineSeconds = (nowUtc - startUtc).TotalSeconds;
        if (offlineSeconds <= 0) return;
        double simSeconds = Math.Min(offlineSeconds, 60.0 * 86400.0);
        const double stepSeconds = 3600.0;
        int steps = (int)Math.Ceiling(simSeconds / stepSeconds);
        float dt = (float)(simSeconds / steps);

        long lastDropDay = lastMorningDropDateTicks;

        for (int i = 0; i < steps; i++)
        {
            DateTime chunkLocal = startUtc.AddSeconds(i * dt).ToLocalTime();
            int h = chunkLocal.Hour;
            bool night = h >= SleepStartHour || h < SleepEndHour;
            long chunkDay = chunkLocal.Date.Ticks;

            // 每天 6 点后第一个白天 chunk：扣食物（同在线逻辑，任务17F）
            if (!night && h >= SleepEndHour && lastDropDay != chunkDay)
            {
                hunger = Mathf.Max(0f, hunger - MorningHungerDrop);
                lastDropDay = chunkDay;
            }

            if (night) continue; // 睡着：不衰减

            hunger = Mathf.Max(0f, hunger - HungerDecayPerSec * dt);
            clean = Mathf.Max(0f, clean - CleanDecayPerSec * dt);
            mood = Mathf.Max(0f, mood - GetMoodDecayPerSec(mood) * dt);
            if (IsHealthRecovering(hunger, mood, clean))
                health = Mathf.Min(100f, health + HealthRecoverPerSec * dt);
            else
                health = Mathf.Max(0f, health - GetHealthDecayPerSec(hunger, mood, clean) * dt);
        }
        lastMorningDropDateTicks = lastDropDay;
        CheckDeath(); // 任务17E：离线补算后判死亡
    }

    /// <summary>领养新蛋（任务17E；17F-补丁补清睡眠窗/晨扣日期）：数值回满、出生时间重置为当前（回蛋）、疲劳计时重启、清死亡标记并立即存档；金币/成就/物品/签到/碎碎念全保留</summary>
    public void ReviveAsEgg()
    {
        hunger = 100f;
        health = 100f;
        mood = 100f;
        clean = 100f;
        birthTimeTicks = DateTime.Now.Ticks;
        awakeSinceTicks = DateTime.UtcNow.Ticks;
        nightAwakeUntilTicks = 0;                         // 任务17F-补丁：不继承旧睡眠窗口（新蛋回默认状态）
        lastMorningDropDateTicks = DateTime.Today.Ticks;  // 任务17F-补丁：死亡必发生在白天晨扣后，领蛋当天不重扣
        isDead = false;
        Save();
    }

    /// <summary>抚摸小鸡（任务17F-补丁；17G 起改加心情）：每天最多 3 次，每次 +5 心情（钳 0~100）；跨天自动清零计数，成功立即存档。返回本次是否成功。</summary>
    public bool PatChick()
    {
        long today = DateTime.Today.Ticks;
        if (lastPatDateTicks != today) { lastPatDateTicks = today; patCountToday = 0; }
        if (patCountToday >= PatDailyLimit) return false;
        patCountToday++;
        mood = Mathf.Clamp(mood + 5f, 0f, 100f);
        Save();
        return true;
    }

    // 退出 / 失焦时自动存档（编辑器停止 Play 也会触发 OnApplicationQuit）
    private void OnApplicationQuit() { Save(); }
    private void OnApplicationPause(bool pause) { if (pause) Save(); }

    // ---------------- 进化计算（阶段三任务11） ----------------

    /// <summary>当前天数：出生至今的自然日数，最小 0（17J 起删测试偏移）</summary>
    public int GetCurrentDay()
    {
        long day = 0;
        if (birthTimeTicks > 0)
            day = (DateTime.Now.Ticks - birthTimeTicks) / TimeSpan.TicksPerDay;
        return Mathf.Max(0, (int)day);
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

    /// <summary>全部成就定义表（任务15B；17D补丁删 sleep_10，5 项）：喂食×2 / 洗澡 / 逗鸡 / 签到</summary>
    public static readonly AchievementDef[] AchievementDefs =
    {
        new AchievementDef { id = "feed_10",  name = "新手饲养员", target = 10,  reward = 10000L },
        new AchievementDef { id = "feed_100", name = "金牌饲养员", target = 100, reward = 100000L },
        new AchievementDef { id = "bath_10",  name = "爱干净",     target = 10,  reward = 10000L },
        new AchievementDef { id = "yard_10",  name = "游戏伙伴",   target = 10,  reward = 10000L },
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
