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

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        Load();                 // 先读档
        ApplyOfflineDecay();    // 再按离线时长扣减
    }

    private void Update()
    {
        // 在线衰减（帧率无关）
        hunger = Mathf.Clamp(hunger - HungerDecayPerSec * Time.deltaTime, 0f, 100f);
        health = Mathf.Clamp(health - HealthDecayPerSec * Time.deltaTime, 0f, 100f);
        mood = Mathf.Clamp(mood - MoodDecayPerSec * Time.deltaTime, 0f, 100f);
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
        PlayerPrefs.Save();
    }

    /// <summary>读档：无存档时保持字段初始值</summary>
    public void Load()
    {
        hunger = PlayerPrefs.GetFloat(KeyHunger, hunger);
        health = PlayerPrefs.GetFloat(KeyHealth, health);
        mood = PlayerPrefs.GetFloat(KeyMood, mood);
        if (long.TryParse(PlayerPrefs.GetString(KeyCoins, ""), out var c)) coins = c;
        if (long.TryParse(PlayerPrefs.GetString(KeyTicks, ""), out var t)) lastSaveTimeTicks = t;
    }

    /// <summary>离线衰减：按上次存档至今的秒数 × 同速率扣除，最多按 8 小时计</summary>
    public void ApplyOfflineDecay()
    {
        if (lastSaveTimeTicks <= 0) return; // 从未存档（首次进入）不衰减
        double offlineSeconds = (DateTime.UtcNow.Ticks - lastSaveTimeTicks) / (double)TimeSpan.TicksPerSecond;
        if (offlineSeconds <= 0) return;
        double capped = Math.Min(offlineSeconds, MaxOfflineSeconds);
        hunger = Mathf.Clamp((float)(hunger - HungerDecayPerSec * capped), 0f, 100f);
        health = Mathf.Clamp((float)(health - HealthDecayPerSec * capped), 0f, 100f);
        mood = Mathf.Clamp((float)(mood - MoodDecayPerSec * capped), 0f, 100f);
    }

    // 退出 / 失焦时自动存档（编辑器停止 Play 也会触发 OnApplicationQuit）
    private void OnApplicationQuit() { Save(); }
    private void OnApplicationPause(bool pause) { if (pause) Save(); }
}
