using System;

/// <summary>
/// 存档数据结构（存档层重构 v1）：GameData 与 JSON 文件之间的纯数据载体。
///
/// 【三条硬约束，改动时勿破】
/// 1. JsonUtility 不支持 long —— coins 及全部 Ticks 字段一律用 string 存原始数值文本。
///    且 Ticks 约 6.4e17，远超 double 精确整数上限 2^53（约 9e15），用数值型必丢精度、
///    会让签到/晨扣/出生日判定全部出错，所以只能用 string。
/// 2. 字段必须 public —— JsonUtility 不认 private / property。
/// 3. 字段初始化器 = 新档默认值。读档走 JsonUtility.FromJsonOverwrite（而非 FromJson），
///    这样 JSON 里缺失的字段会保留这里的默认值，精确复刻旧版
///    PlayerPrefs.GetInt(key, 字段初始值) 的语义（旧档缺键不该被读成 0）。
///
/// 【维护提醒】新增存档字段时，需同步三处：本类 + GameData.Save() + GameData.ApplyFromDto()。
/// </summary>
[Serializable]
public class SaveData
{
    /// <summary>存档格式版本：新档直接写当前版本；读档时低于当前版本的会走 GameData.MigrateDto 升级</summary>
    public int version = 2;

    /// <summary>
    /// 经济重设是否已执行（一次性）。用显式标记而不是版本号来判断：
    /// 版本号会漏掉"档已写成 v2 但数值仍是开发期测试数据"的情况，标记则不管档是什么版本都能兜住。
    /// </summary>
    public bool economyReset = false;

    // ---- 四维状态（0~100；hunger 允许超 100，其余封顶 100）----
    public float hunger = 100f;
    public float health = 100f;
    public float mood = 100f;
    public float clean = 100f;

    // ---- 经济 ----
    public string coins = "1000";                  // long → string（经济重设单A：开局 1000）
    public string lastSaveTimeTicks = "0";         // long → string（离线衰减起点，UTC Ticks）

    // ---- 商店 / 装扮（阶段三任务10）----
    public string ownedItems = "";                 // 逗号分隔的已拥有商品 id
    public string equippedHat = "";
    public string equippedScarf = "";
    public string equippedGlasses = "";
    public string equippedToy = "";                // 当前选中的玩具 id（空 = 还没有玩具）

    // ---- 进化（阶段三任务11）----
    public string birthTimeTicks = "0";            // 出生时间戳（DateTime.Now.Ticks）

    // ---- 签到（阶段三任务14）----
    public string lastSignInDateTicks = "0";       // 上次签到日 0 点（DateTime.Today.Ticks）
    public int signInStreak = 0;                   // 连签天数 1~7 一轮

    // ---- 成就（阶段四任务15A）----
    public int feedCount = 0;
    public int bathCount = 0;
    public int yardCount = 0;
    public int sleepCount = 0;
    public string unlockedAchievements = "";       // 已解锁成就 id，逗号分隔

    // ---- 碎碎念（阶段四任务16A）----
    public string diaryEntries = "";               // "日期|内容;日期|内容"，最多最近 30 条

    // ---- 回忆录（wb 分支）----
    public string memories = "";                   // "日期|内容;..."，自动事件记录，条数不设上限

    // ---- 死亡（任务17E）----
    public bool isDead = false;

    // ---- 睡眠 / 晨扣（任务17F）----
    public string nightAwakeUntilTicks = "0";      // 夜间唤醒截止（UTC Ticks）
    public string lastMorningDropDateTicks = "0";  // 上次晨扣的本地日期（Today.Ticks）

    // ---- 抚摸小鸡（任务17F-补丁）----
    public int patCountToday = 0;                  // 今日已抚摸次数
    public string lastPatDateTicks = "0";          // 上次抚摸日期（Today.Ticks）

    // ---- 散步日限（A+ 上限机制）----
    public int yardCountToday = 0;                 // 今日已散步次数
    public string lastYardDateTicks = "0";         // 上次散步日期（Today.Ticks）

    // ---- 陪玩玩具（wb 分支）----
    public int toyCountToday = 0;                  // 今日已陪玩玩具次数
    public string lastToyDateTicks = "0";          // 上次陪玩日期（Today.Ticks）
}
