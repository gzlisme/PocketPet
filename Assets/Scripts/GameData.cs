using System;
using System.IO;
using UnityEngine;

/// <summary>
/// 游戏数据底座（阶段二任务5）：单例 + JSON 存档 + 在线/离线衰减。
/// 存档层（wb 分支重构）：落盘于 Application.persistentDataPath/pocketpet_save.json，人类可读可手改；
/// 首次运行自动把旧版 PlayerPrefs 存档迁到 JSON（旧键保留不删，作回滚兜底）。
/// 写盘为"临时文件 → 备份 .bak → 原子替换"，读档失败时依次回退 .bak，绝不悄悄读回旧 PlayerPrefs。
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
    public long coins = 1000L;      // 金币：开局启动资金（经济重设单A）
    public long lastSaveTimeTicks;      // 上次退出时间戳（UTC Ticks）
    public bool isDead = false;          // 死亡标记（任务17E：health≤0 置位，重启不复活；领蛋重置清零）

    // ---- 商店字段（阶段三任务10，纯字符串存档，向后兼容：无存档时为空串）----
    public string ownedItems = "";       // 逗号分隔的已拥有商品 id，如 "hat_red,scarf_blue"
    public string equippedHat = "";      // 已装备帽子 id（空 = 未装备）
    public string equippedScarf = "";   // 已装备围巾 id
    public string equippedGlasses = ""; // 已装备墨镜 id
    public string equippedToy = "";     // 当前选中的玩具 id（空 = 还没有玩具；wb 分支新增，玩具不占装饰槽位）

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

    // ---- 回忆录字段（wb 分支：小鸡成长史，自动记录）----
    public string memories = "";           // 自动事件记录："日期|内容;..."，与玩家手写的碎碎念分开存，条数不设上限
    private const int MaxMemories = 2000;  // 硬保护：极端情况下防止存档无限膨胀（正常玩十年也到不了）

    // ---- 疲劳与睡觉（任务17D/17F）----
    private long awakeSinceTicks = 0;   // 连续醒着起点（Awake 设；睡着清零、醒着重启，不存档）
    private long nightAwakeUntilTicks = 0;  // 晚上唤醒截止（UtcTicks），0 或已过 = 睡着（任务17F，存档）
    private long lastMorningDropDateTicks = 0;  // 上次"早 6 点扣 hunger"的本地日期（Today.Ticks；任务17F，存档）

    // ---- 抚摸小鸡（任务17F-补丁，存档）----
    private int patCountToday = 0;      // 今日已抚摸次数（跨天由 PatChick 自动清零）
    private long lastPatDateTicks = 0;  // 上次抚摸日期（Today.Ticks）

    // ---- 散步日限（A+ 上限机制，存档）----
    private int yardCountToday = 0;      // 今日已散步次数（跨天由 TryConsumeYardToday 自动清零）
    private long lastYardDateTicks = 0;  // 上次散步日期（Today.Ticks）

    // ---- 陪玩玩具（wb 分支，存档）----
    private int toyCountToday = 0;       // 今日已陪玩玩具次数
    private long lastToyDateTicks = 0;   // 上次陪玩日期（Today.Ticks）

    // ---- 存档元数据（wb 分支）----
    private bool economyReset = false;   // 经济重设是否已执行（一次性标记，见 MigrateDto）

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

    // ---- A+ 上限机制（wb 分支第 2 步）----
    /// <summary>
    /// 心情软天花板：mood ≥ 此值时，一切"加心情"效果归零（食物、清洁照常，不受影响）。
    /// 存在理由：陪散步是免费且原先无限次的，能一路把心情点到 100，心情彻底失去稀缺性。
    /// 70 = 从低位靠散步/抚摸一天内能补到的上限，再往上必须靠"花金币的零食/泡泡浴"或等第二天。
    /// </summary>
    public const float MoodSoftCap = 70f;

    /// <summary>陪散步每日上限（次数，不是金币）：免费项必须有日限，否则金币再多也不影响它刷满心情</summary>
    public const int YardDailyLimit = 5;

    /// <summary>陪玩玩具每日上限（次数）：玩具是买断制，若不限次就等于用 300 金币买了个无限心情来源</summary>
    public const int ToyDailyLimit = 3;

    // ---- 疲劳阈值与倍率（任务17D，连续在线时长）----
    private const double FatigueThreshold1Hours = 16.0;
    private const double FatigueThreshold2Hours = 24.0;
    private const float FatigueMult1 = 1.5f;
    private const float FatigueMult2 = 2f;

    // ---- 旧版 PlayerPrefs 键（wb 存档层重构后仅作"一次性迁移源"，新档全部写入 JSON）----
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

    // ---- JSON 存档（wb 分支：存档层重构）----
    /// <summary>存档结构版本号，写入 SaveData.version；读档时低于此值的档会走 MigrateDto 就地升级</summary>
    private const int SaveVersion = 2;

    /// <summary>
    /// 存档文件绝对路径。编辑器 / PC 下为 %USERPROFILE%\AppData\LocalLow\&lt;公司名&gt;\&lt;产品名&gt;\pocketpet_save.json；
    /// 微信小游戏下由引擎映射到 wx.env.USER_DATA_PATH。可直接用文本编辑器查看、手改数值。
    /// </summary>
    public static string SaveFilePath
    {
        get { return Path.Combine(Application.persistentDataPath, "pocketpet_save.json"); }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        Load();                 // 先读档（内部：JSON → .bak → 旧 PlayerPrefs 迁移 → 新档）
        awakeSinceTicks = DateTime.UtcNow.Ticks; // 任务17D：连续在线起点（疲劳计时，不存档）
        if (lastMorningDropDateTicks == 0)
            lastMorningDropDateTicks = DateTime.Today.Ticks; // 任务17F：首次启动当天不扣，次日 6 点起扣
        if (birthTimeTicks <= 0)
        {
            birthTimeTicks = DateTime.Now.Ticks; // 首次启动初始化出生时间（任务11）
            AddMemory("我来到这个家啦");          // wb：回忆录第一条
        }
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
        if (!isDead && health <= 0f)
        {
            isDead = true;
            AddMemory("它睡着了，再也没醒过来"); // wb：回忆录最后一条
            Save();
        }
    }

    // ================= 存档层（wb 分支重构：PlayerPrefs → persistentDataPath JSON） =================

    /// <summary>
    /// 存档：刷新存档时刻（= "我活到这一刻了"，离线衰减的起点）→ 组装 SaveData → 原子写 JSON（顺带 .bak 备份）。
    /// 写盘异常只记日志不抛：存档失败不该把游戏搞崩，下一轮 Save 自会重试。
    /// </summary>
    public void Save()
    {
        lastSaveTimeTicks = DateTime.UtcNow.Ticks; // 先刷新存档时刻，再组装（顺序与重构前一致）
        WriteSaveQuiet();
    }

    /// <summary>
    /// 原样落盘：不刷新 lastSaveTimeTicks，只把内存现状写出去。
    /// 专供"读档后回写"场景（旧档迁移、.bak 恢复）—— 那些场景下 lastSaveTimeTicks 必须保留存档里的旧值，
    /// 否则 ApplyOfflineDecay() 会算出"离线 0 秒"，离线衰减全部丢失。注意：别拿它替普通存档。
    /// </summary>
    private void WriteSaveQuiet()
    {
        try
        {
            WriteSaveFile(BuildDto());
        }
        catch (Exception e)
        {
            Debug.LogError("[存档] 写入失败：" + e.Message + "  路径：" + SaveFilePath);
        }
    }

    /// <summary>把当前内存状态组装为存档 DTO（long 一律转 string，原因见 SaveData 类注释）</summary>
    private SaveData BuildDto()
    {
        return new SaveData
        {
            version = SaveVersion,
            economyReset = economyReset,
            hunger = hunger,
            health = health,
            mood = mood,
            clean = clean,
            coins = coins.ToString(),
            lastSaveTimeTicks = lastSaveTimeTicks.ToString(),
            ownedItems = ownedItems ?? "",
            equippedHat = equippedHat ?? "",
            equippedScarf = equippedScarf ?? "",
            equippedGlasses = equippedGlasses ?? "",
            equippedToy = equippedToy ?? "",
            birthTimeTicks = birthTimeTicks.ToString(),
            lastSignInDateTicks = lastSignInDateTicks.ToString(),
            signInStreak = signInStreak,
            feedCount = feedCount,
            bathCount = bathCount,
            yardCount = yardCount,
            sleepCount = sleepCount,
            unlockedAchievements = unlockedAchievements ?? "",
            diaryEntries = diaryEntries ?? "",
            memories = memories ?? "",
            isDead = isDead,
            nightAwakeUntilTicks = nightAwakeUntilTicks.ToString(),
            lastMorningDropDateTicks = lastMorningDropDateTicks.ToString(),
            patCountToday = patCountToday,
            lastPatDateTicks = lastPatDateTicks.ToString(),
            yardCountToday = yardCountToday,
            lastYardDateTicks = lastYardDateTicks.ToString(),
            toyCountToday = toyCountToday,
            lastToyDateTicks = lastToyDateTicks.ToString(),
        };
    }

    /// <summary>
    /// 原子写盘：先写 .tmp → 现有档备份成 .bak → rename 就位。
    /// 只用 WriteAllText / Copy / Delete / Move 四个最基础的 IO API，跨平台（含微信小游戏）兼容性最稳；
    /// 不用 File.Replace，那玩意在部分 Mono / 移动端封装下行为不一致。
    /// </summary>
    private static void WriteSaveFile(SaveData dto)
    {
        string path = SaveFilePath;
        string tmp = path + ".tmp";
        string bak = path + ".bak";

        File.WriteAllText(tmp, JsonUtility.ToJson(dto, true)); // true = 缩进美化，方便人肉查看与手改
        if (File.Exists(path))
        {
            File.Copy(path, bak, true); // 上一版快照，读档失败时回退用
            File.Delete(path);
        }
        File.Move(tmp, path);
    }

    /// <summary>
    /// 读档总入口，四级回退：JSON 主档 → .bak 备份（读成功立刻回写主档）→ 旧版 PlayerPrefs 一次性迁移 → 全新档。
    /// 关键安全点：只要 JSON 主档曾经存在，就绝不再悄悄回读 PlayerPrefs —— 那些键早停止更新，
    /// 读回来 = 数据倒退，比报错更坑。宁可停在默认值并大声报错，让人看到 .bak 在哪。
    /// </summary>
    public void Load()
    {
        string path = SaveFilePath;

        if (File.Exists(path))
        {
            if (TryLoadFromJson(path)) { Debug.Log("[存档] 已读取：" + path); return; }

            string bak = path + ".bak";
            if (File.Exists(bak) && TryLoadFromJson(bak))
            {
                Debug.LogWarning("[存档] 主档不可读，已从备份恢复：" + bak);
                WriteSaveQuiet(); // 立刻回写主档（保留备份里的到期时刻，否则离线衰减归零）
                return;
            }

            Debug.LogError("[存档] 主档与备份均不可读，停在默认值。请检查：" + path);
            return;
        }

        if (TryMigrateFromPlayerPrefs())
        {
            WriteSaveQuiet(); // 迁移结果落盘（保留旧档到期时刻，供 Awake 里的 ApplyOfflineDecay 使用）
            Debug.Log("[存档] 旧版 PlayerPrefs 存档已迁移到 JSON：" + path);
            return;
        }

        Debug.Log("[存档] 无历史存档，按新档启动：" + path);
    }

    /// <summary>
    /// 存档结构版本迁移 + 一次性数据修正：就地升级 DTO，返回 true 表示改动了内容（调用方应立刻回写主档）。
    /// 未提及的字段一律原样保留。
    /// </summary>
    private bool MigrateDto(SaveData dto)
    {
        bool changed = false;

        // 经济重设（一次性，用标记而非版本号判定）。
        // 旧档是从 PlayerPrefs 忠实搬过来的，把开发期测试数据一起带了进来：金币 1 亿级、食物喂到 1400+，
        // 导致"经济重设单A"每次测试都白做。这里一次性收敛到正规开局值，打过标记后永不再动。
        if (!dto.economyReset)
        {
            if (long.TryParse(dto.coins, out var c) && c > 100000L)
                dto.coins = "1000";        // 超 10 万 = 测试遗留，收敛到开局资金
            if (dto.hunger > 100f) dto.hunger = 100f;   // 食物本可超量，但 1400 属测试残留
            if (dto.clean > 100f) dto.clean = 100f;
            if (dto.mood > 100f) dto.mood = 100f;
            if (dto.health > 100f) dto.health = 100f;
            dto.economyReset = true;
            changed = true;
        }

        // 版本号推进，供以后真正的结构迁移使用
        if (dto.version < SaveVersion) { dto.version = SaveVersion; changed = true; }

        return changed;
    }

    /// <summary>读单一 JSON 文件并应用到内存；IO / 解析异常返回 false，由调用方决定往哪级回退</summary>
    private bool TryLoadFromJson(string path)
    {
        try
        {
            string json = File.ReadAllText(path);
            if (string.IsNullOrEmpty(json)) return false;
            var dto = new SaveData();                    // 字段初始化器即"缺字段时的默认值"
            JsonUtility.FromJsonOverwrite(json, dto);    // 用 Overwrite 而非 FromJson：保留缺字段默认值
            bool upgraded = MigrateDto(dto);             // 先升级版本，再应用到内存
            ApplyFromDto(dto);
            if (upgraded) WriteSaveQuiet();              // 迁移结果立刻落盘，避免每次启动重算
            return true;
        }
        catch (Exception e)
        {
            Debug.LogWarning("[存档] 读取失败 " + path + " → " + e.Message);
            return false;
        }
    }

    /// <summary>
    /// 旧版 PlayerPrefs 一次性迁移（只在 JSON 主档从未存在时被调用）。
    /// 逐键读取与重构前 1:1 对齐，"缺键保持字段初始值"靠 GetXxx(key, 当前值) 天然成立。
    /// 判定"是否有旧档"用 pp_lastSaveTimeTicks：只有真正 Save 过才会写下非 0 值。
    /// 旧键迁移后不删除，留作人工回滚兜底。
    /// </summary>
    private bool TryMigrateFromPlayerPrefs()
    {
        if (!long.TryParse(PlayerPrefs.GetString(KeyTicks, ""), out var oldTicks) || oldTicks <= 0)
            return false; // 从未存过档 → 不是旧档，走新档

        hunger = PlayerPrefs.GetFloat(KeyHunger, hunger);
        health = PlayerPrefs.GetFloat(KeyHealth, health);
        mood = PlayerPrefs.GetFloat(KeyMood, mood);
        clean = PlayerPrefs.GetFloat(KeyClean, clean);
        if (long.TryParse(PlayerPrefs.GetString(KeyCoins, ""), out var c)) coins = c;
        lastSaveTimeTicks = oldTicks;
        ownedItems = PlayerPrefs.GetString(KeyOwned, ownedItems);
        equippedHat = PlayerPrefs.GetString(KeyEquippedHat, equippedHat);
        equippedScarf = PlayerPrefs.GetString(KeyEquippedScarf, equippedScarf);
        equippedGlasses = PlayerPrefs.GetString(KeyEquippedGlasses, equippedGlasses);
        if (long.TryParse(PlayerPrefs.GetString(KeyBirthTicks, ""), out var bt)) birthTimeTicks = bt;
        if (long.TryParse(PlayerPrefs.GetString(KeyLastSignInDate, ""), out var lsd)) lastSignInDateTicks = lsd;
        signInStreak = PlayerPrefs.GetInt(KeySignInStreak, signInStreak);
        feedCount = PlayerPrefs.GetInt(KeyFeedCount, feedCount);
        bathCount = PlayerPrefs.GetInt(KeyBathCount, bathCount);
        yardCount = PlayerPrefs.GetInt(KeyYardCount, yardCount);
        sleepCount = PlayerPrefs.GetInt(KeySleepCount, sleepCount);
        unlockedAchievements = PlayerPrefs.GetString(KeyUnlockedAchievements, unlockedAchievements);
        diaryEntries = PlayerPrefs.GetString(KeyDiaryEntries, diaryEntries);
        isDead = PlayerPrefs.GetInt(KeyIsDead, 0) == 1;
        if (long.TryParse(PlayerPrefs.GetString(KeyNightAwakeUntil, ""), out var nau)) nightAwakeUntilTicks = nau;
        if (long.TryParse(PlayerPrefs.GetString(KeyLastMorningDrop, ""), out var lmd)) lastMorningDropDateTicks = lmd;
        patCountToday = PlayerPrefs.GetInt(KeyPatCountToday, patCountToday);
        if (long.TryParse(PlayerPrefs.GetString(KeyLastPatDate, ""), out var lpd)) lastPatDateTicks = lpd;

        // 经济重设收尾（与 MigrateDto 同一套规则）：
        // 从旧 PlayerPrefs 搬过来的档同样带着开发期测试数据，迁移时一并收敛，否则会直接写成新版本、再也改不掉。
        if (coins > 100000L) coins = 1000L;
        hunger = Mathf.Min(100f, hunger);
        clean = Mathf.Min(100f, clean);
        mood = Mathf.Min(100f, mood); // 任务17G：心情封顶 100（修旧档超 100，如 1453；食物不封顶→此处一并收敛测试残留）
        health = Mathf.Min(100f, health);
        economyReset = true;          // 已经收敛过，打标记，防止后续再次触发
        return true;
    }

    /// <summary>把 DTO 应用到内存字段；字符串空引用统一兜成空串，long 解析失败则保留当前值</summary>
    private void ApplyFromDto(SaveData dto)
    {
        economyReset = dto.economyReset;   // 存档元数据：标记是否已做过一次性经济重设
        hunger = dto.hunger;
        health = dto.health;
        mood = dto.mood;
        clean = dto.clean;
        if (long.TryParse(dto.coins, out var c)) coins = c;
        if (long.TryParse(dto.lastSaveTimeTicks, out var t)) lastSaveTimeTicks = t;
        ownedItems = dto.ownedItems ?? "";
        equippedHat = dto.equippedHat ?? "";
        equippedScarf = dto.equippedScarf ?? "";
        equippedGlasses = dto.equippedGlasses ?? "";
        equippedToy = dto.equippedToy ?? "";
        if (long.TryParse(dto.birthTimeTicks, out var bt)) birthTimeTicks = bt;
        if (long.TryParse(dto.lastSignInDateTicks, out var lsd)) lastSignInDateTicks = lsd;
        signInStreak = dto.signInStreak;
        feedCount = dto.feedCount;
        bathCount = dto.bathCount;
        yardCount = dto.yardCount;
        sleepCount = dto.sleepCount;
        unlockedAchievements = dto.unlockedAchievements ?? "";
        diaryEntries = dto.diaryEntries ?? "";
        memories = dto.memories ?? "";
        isDead = dto.isDead;
        if (long.TryParse(dto.nightAwakeUntilTicks, out var nau)) nightAwakeUntilTicks = nau;
        if (long.TryParse(dto.lastMorningDropDateTicks, out var lmd)) lastMorningDropDateTicks = lmd;
        patCountToday = dto.patCountToday;
        if (long.TryParse(dto.lastPatDateTicks, out var lpd)) lastPatDateTicks = lpd;
        yardCountToday = dto.yardCountToday;
        if (long.TryParse(dto.lastYardDateTicks, out var lyd)) lastYardDateTicks = lyd;
        toyCountToday = dto.toyCountToday;
        if (long.TryParse(dto.lastToyDateTicks, out var ltd)) lastToyDateTicks = ltd;
        mood = Mathf.Min(100f, mood); // 任务17G：心情封顶 100（手改 JSON 写超了也不越界）
    }

    /// <summary>调试用：把当前内存状态导出一份 JSON 到指定目录（不改变存档），返回写出路径；失败返回 null</summary>
    public string ExportSaveSnapshot(string directory)
    {
        try
        {
            if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);
            string file = Path.Combine(directory, "pocketpet_save_export.json");
            File.WriteAllText(file, JsonUtility.ToJson(BuildDto(), true));
            return file;
        }
        catch (Exception e)
        {
            Debug.LogError("[存档] 导出快照失败：" + e.Message);
            return null;
        }
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
        // A+ 软天花板：心情已到 70 就不再涨，但次数照消耗（老板拍板：用户愿意摸就让他摸）
        if (mood < MoodSoftCap) mood = Mathf.Clamp(mood + 5f, 0f, 100f);
        Save();
        return true;
    }

    // ---------------- 散步日限（A+ 上限机制） ----------------

    /// <summary>今天还能陪散步几次（跨天视为满额，不改动任何状态，供 UI 显示）</summary>
    public int GetYardRemainingToday()
    {
        if (lastYardDateTicks != DateTime.Today.Ticks) return YardDailyLimit; // 未跨天记录 → 新的一天，满额
        return Mathf.Max(0, YardDailyLimit - yardCountToday);
    }

    /// <summary>
    /// 消耗一次今日散步额度：还有额度则次数 +1 并返回 true；已用完返回 false 且不消耗。
    /// 跨天自动清零。注意：调用方必须先确认心情没到软天花板，否则等于白烧一次额度。
    /// </summary>
    public bool TryConsumeYardToday()
    {
        long today = DateTime.Today.Ticks;
        if (lastYardDateTicks != today) { lastYardDateTicks = today; yardCountToday = 0; }
        if (yardCountToday >= YardDailyLimit) return false;
        yardCountToday++;
        return true;
    }

    // ---------------- 陪玩玩具（wb 分支：豪华玩具下线，改由商店买断） ----------------

    /// <summary>是否已拥有某商品（ownedItems 逗号拆分精确比对；空 id / 空串一律 false）</summary>
    public bool IsOwnedItem(string itemId)
    {
        if (string.IsNullOrEmpty(itemId) || string.IsNullOrEmpty(ownedItems)) return false;
        foreach (var s in ownedItems.Split(','))
            if (s == itemId) return true;
        return false;
    }

    /// <summary>是否至少拥有一件玩具（供院子按钮决定显示"去买"还是"去玩"）</summary>
    public bool HasAnyToy()
    {
        foreach (var it in ShopItem.AllToys())
            if (IsOwnedItem(it.id)) return true;
        return false;
    }

    /// <summary>今天还能陪玩几次（跨天视为满额；不改动任何状态，供 UI 显示）</summary>
    public int GetToyRemainingToday()
    {
        if (lastToyDateTicks != DateTime.Today.Ticks) return ToyDailyLimit;
        return Mathf.Max(0, ToyDailyLimit - toyCountToday);
    }

    /// <summary>
    /// 陪玩一次当前选中的玩具：无玩具 / 次数用完 / id 无效 → 返回 null 且不改任何状态；
    /// 成功 → 次数 +1、按 toyMoodGain 加心情（钳 100）、立即存档，返回玩到的那件玩具。
    /// 注意：玩具**不受** MoodSoftCap 限制 —— 花钱买的那部分心情本来就该能突破免费项的 70 天花板。
    /// </summary>
    public ShopItem PlayToy()
    {
        var item = ShopItem.FindById(equippedToy);
        if (item == null || !item.IsToy) return null;

        long today = DateTime.Today.Ticks;
        if (lastToyDateTicks != today) { lastToyDateTicks = today; toyCountToday = 0; }
        if (toyCountToday >= ToyDailyLimit) return null;

        toyCountToday++;
        mood = Mathf.Clamp(mood + item.toyMoodGain, 0f, 100f);
        Save();
        return item;
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

    /// <summary>今天签到会得的金币（第 7 天连签 = 800，其余 = 150），供 UI 显示，不改动任何状态</summary>
    public int GetTodayReward()
    {
        long yesterday = DateTime.Today.AddDays(-1).Ticks;
        int streakToday = (lastSignInDateTicks == yesterday) ? signInStreak + 1 : 1; // 今天若签到将处的连签天数：昨天签过→+1，首次/断签→1
        if (streakToday > 7) streakToday = 1; // 一周一轮：第 8 天回到第 1 天
        return streakToday >= 7 ? 800 : 150;
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
        if (signInStreak == 7) AddMemory("连着签到 7 天，全勤！"); // wb：回忆录
        Save(); // 立即存档
        CheckAndGrantAchievements(); // 任务15B：签到后检测并发放成就（sign_7 全勤奖）
        return reward;
    }

    // ---------------- 成就（阶段四任务15A） ----------------

    /// <summary>累计喂食次数 +1（任务15B：+1 后检测并发放成就）</summary>
    public void AddFeedCount()
    {
        feedCount += 1;
        if (feedCount == 1) AddMemory("第一次吃到好吃的"); // wb：回忆录
        CheckAndGrantAchievements();
    }

    /// <summary>累计洗澡次数 +1（任务15B：+1 后检测并发放成就）</summary>
    public void AddBathCount()
    {
        bathCount += 1;
        if (bathCount == 1) AddMemory("第一次洗澡，香香的"); // wb：回忆录
        CheckAndGrantAchievements();
    }

    /// <summary>累计逗鸡次数 +1（任务15B：+1 后检测并发放成就）</summary>
    public void AddYardCount()
    {
        yardCount += 1;
        if (yardCount == 1) AddMemory("第一次出去玩，好开心"); // wb：回忆录
        CheckAndGrantAchievements();
    }

    /// <summary>累计睡觉次数 +1（任务15B：+1 后检测并发放成就）</summary>
    public void AddSleepCount()
    {
        sleepCount += 1;
        if (sleepCount == 1) AddMemory("第一次睡得这么香"); // wb：回忆录
        CheckAndGrantAchievements();
    }

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

    // ---------------- 金币格式化（经济重设单A） ----------------

    /// <summary>
    /// 金币分段格式化（单A·健壮版）：&lt;10000 直显；≥1亿 用"亿+万"（余数落到万位）；
    /// ≥10000 用"万"；其余直显。老档 1 亿 = "1亿"，1.5 亿 = "1亿5000万"。
    /// </summary>
    public static string FormatCoins(long amount)
    {
        if (amount < 10000L) return amount.ToString();
        if (amount >= 100000000L)
        {
            long yi = amount / 100000000L;
            long rest = amount % 100000000L;
            if (rest == 0) return yi + "亿";
            long wan = rest / 10000L;
            if (wan == 0) return yi + "亿";  // 边界修正：1 亿零 5000 → "1亿"（避免"1亿0万"）
            return yi + "亿" + wan + "万";   // 万以下零头丢弃（万以下无意义）
        }
        if (amount % 10000L == 0) return (amount / 10000L) + "万";
        return (amount / 10000L) + "." + (amount % 10000L / 1000L) + "万";
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
        new AchievementDef { id = "feed_10",  name = "新手饲养员", target = 10,  reward = 200L },
        new AchievementDef { id = "feed_100", name = "金牌饲养员", target = 100, reward = 1500L },
        new AchievementDef { id = "bath_10",  name = "爱干净",     target = 10,  reward = 200L },
        new AchievementDef { id = "yard_10",  name = "游戏伙伴",   target = 10,  reward = 200L },
        new AchievementDef { id = "sign_7",   name = "全勤奖",     target = 7,   reward = 800L },
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
            AddMemory("拿到了「" + def.name + "」"); // wb：回忆录（在 Save 之前，随这次存档一起落盘）
            Save();
            // 任务15D：解锁时缓存提示消息供 UI 反馈文本显示（多个用"；"分隔；解锁逻辑本身不变）
            string oneMessage = "成就达成：" + def.name + " +" + FormatCoins(def.reward) + " 金币"; // 单A：改走分段格式化；空格与签到飘字统一
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

    // ---------------- 小鸡回忆录（wb 分支） ----------------

    /// <summary>
    /// 追加一条回忆录：小鸡成长过程中的自动事件记录，与玩家手写的碎碎念分开存。
    /// 格式 "yyyy-MM-dd|内容;"，条数不设上限（老板要求），仅留 MaxMemories 硬保护防止极端膨胀。
    /// 内容里的分隔符会被替换成全角，避免解析错乱。不自动存档，由调用方决定时机。
    /// </summary>
    public void AddMemory(string content)
    {
        if (string.IsNullOrEmpty(content)) return;
        content = content.Replace(";", "；").Replace("|", "｜"); // 别让内容破坏分隔符
        string entry = DateTime.Today.ToString("yyyy-MM-dd") + "|" + content;
        memories = string.IsNullOrEmpty(memories) ? entry : memories + ";" + entry;

        string[] parts = memories.Split(';');
        if (parts.Length > MaxMemories)
            memories = string.Join(";", parts, parts.Length - MaxMemories, MaxMemories); // 丢弃最旧的
    }

    /// <summary>返回回忆录原始字符串（格式 "日期|内容;日期|内容"，供 UI 解析）</summary>
    public string GetMemories()
    {
        return memories;
    }
}
