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
        return reward;
    }
}
