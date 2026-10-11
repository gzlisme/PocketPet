using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UI 交互逻辑（阶段二）：
/// - ToggleDrawer/CloseDrawer：☰ 开关抽屉 + 抽屉外透明点击区 / 抽屉内 X 按钮关闭（任务6.1）
/// - SwitchRoom：任务4假切换方法原样保留（四房间现均绑定各自功能面板，无按钮再调用）
/// - OpenFeedPanel/FeedNormal/FeedPremium/CloseFeedPanel：餐厅喂食（任务6.2，扣金币加状态并立即存档）
/// - OpenBathPanel/BathNormal/BathPremium/CloseBathPanel：浴室洗澡（任务7，扣金币加健康并立即存档）
/// - OpenYardPanel/PlayFree/PlayPremium/CloseYardPanel：院子逗鸡（任务8，扣金币加心情并立即存档）
/// - OpenBedroomPanel/SleepNormal/SleepDeep/CloseBedroomPanel：卧室睡觉（任务9，扣金币加健康/心情并立即存档）
/// </summary>
public class UIManager : MonoBehaviour
{
    [SerializeField] private GameObject drawerPanel;     // 左侧抽屉
    [SerializeField] private GameObject closeDrawerArea; // 抽屉外的全屏透明点击区（打开抽屉时激活）
    [SerializeField] private GameObject feedPanel;       // 喂食面板
    [SerializeField] private Text feedFeedbackText;     // 喂食结果提示文本（金币不足/喂食成功）
    [SerializeField] private GameObject bathPanel;       // 洗澡面板（任务7）
    [SerializeField] private Text bathFeedbackText;     // 洗澡结果提示文本（金币不足/洗澡成功）
    [SerializeField] private GameObject yardPanel;      // 逗鸡面板（任务8）
    [SerializeField] private Text yardFeedbackText;    // 逗鸡结果提示文本（金币不足/逗鸡成功）
    [SerializeField] private GameObject bedroomPanel;  // 睡觉面板（任务9）
    [SerializeField] private Text bedroomFeedbackText;// 睡觉结果提示文本（金币不足/小鸡睡得好香）

    private Camera cam;
    private string currentRoom;
    private float hintUntil = -999f;

    // 假切换的背景色映射（任务4原样保留）
    private static readonly Color RoomYard    = new Color(76f / 255f, 175f / 255f, 80f / 255f);    // 院子 #4CAF50
    private static readonly Color RoomDining   = new Color(1f, 215f / 255f, 0f);                    // 餐厅 #FFD700
    private static readonly Color RoomBedroom  = new Color(138f / 255f, 43f / 255f, 226f / 255f);   // 卧室 #8A2BE2
    private static readonly Color RoomBathroom = new Color(0f, 191f / 255f, 1f);                    // 浴室 #00BFFF

    private void Awake()
    {
        cam = Camera.main;
    }

    /// <summary>☰ 按钮：开/关抽屉；打开时激活外部点击区，关闭时隐藏（任务6.1）</summary>
    public void ToggleDrawer()
    {
        if (drawerPanel == null) return;
        bool open = !drawerPanel.activeSelf;
        drawerPanel.SetActive(open);
        if (closeDrawerArea != null) closeDrawerArea.SetActive(open);
    }

    /// <summary>关闭抽屉（外部透明点击区 / 抽屉内 X 按钮共用，任务6.1）</summary>
    public void CloseDrawer()
    {
        if (drawerPanel != null) drawerPanel.SetActive(false);
        if (closeDrawerArea != null) closeDrawerArea.SetActive(false);
    }

    /// <summary>房间假切换（任务4；餐厅按钮已改绑喂食面板）</summary>
    public void SwitchRoom(string roomName)
    {
        currentRoom = roomName;
        switch (roomName)
        {
            case "院子": cam.backgroundColor = RoomYard; break;
            case "餐厅": cam.backgroundColor = RoomDining; break;
            case "卧室": cam.backgroundColor = RoomBedroom; break;
            case "浴室": cam.backgroundColor = RoomBathroom; break;
        }
        hintUntil = Time.time + 2f;
    }

    // ---------------- 餐厅喂食（任务6.2） ----------------

    /// <summary>打开喂食面板（清空上次提示）</summary>
    public void OpenFeedPanel()
    {
        if (feedPanel == null) return;
        if (feedFeedbackText != null) feedFeedbackText.text = "";
        feedPanel.SetActive(true);
    }

    /// <summary>关闭喂食面板，恢复正常 UI</summary>
    public void CloseFeedPanel()
    {
        if (feedPanel != null) feedPanel.SetActive(false);
    }

    /// <summary>普通饲料：花费 1000 金币，饥饿 +15</summary>
    public void FeedNormal() { TryFeed(1000L, 15f, 0f); }

    /// <summary>豪华零食：花费 50000 金币，饥饿 +40，心情 +10</summary>
    public void FeedPremium() { TryFeed(50000L, 40f, 10f); }

    /// <summary>喂食核心：金币不足→提示不扣钱；足够→扣钱、加状态（钳 100）、立即存档、提示成功</summary>
    private void TryFeed(long cost, float hungerGain, float moodGain)
    {
        var gd = GameData.Instance;
        if (gd == null) return;
        if (gd.coins < cost)
        {
            if (feedFeedbackText != null) feedFeedbackText.text = "金币不足";
            return; // 不扣钱
        }
        gd.coins -= cost;
        gd.hunger = Mathf.Min(100f, gd.hunger + hungerGain);
        if (moodGain > 0f) gd.mood = Mathf.Min(100f, gd.mood + moodGain);
        gd.Save(); // 立即存档
        if (feedFeedbackText != null) feedFeedbackText.text = "喂食成功";
    }

    private void OnGUI()
    {
        if (string.IsNullOrEmpty(currentRoom) || Time.time >= hintUntil) return;
        var style = new GUIStyle(GUI.skin.label) { fontSize = 64, alignment = TextAnchor.MiddleCenter };
        style.normal.textColor = Color.white;
        GUI.Label(new Rect(0f, Screen.height * 0.5f - 50f, Screen.width, 100f), "当前房间：" + currentRoom, style);
    }

    // ---------------- 浴室洗澡（任务7） ----------------

    /// <summary>打开洗澡面板（清空上次提示）</summary>
    public void OpenBathPanel()
    {
        if (bathPanel == null) return;
        if (bathFeedbackText != null) bathFeedbackText.text = "";
        bathPanel.SetActive(true);
    }

    /// <summary>关闭洗澡面板，恢复正常 UI</summary>
    public void CloseBathPanel()
    {
        if (bathPanel != null) bathPanel.SetActive(false);
    }

    /// <summary>普通淋浴：花费 2000 金币，健康 +20</summary>
    public void BathNormal() { TryBath(2000L, 20f, 0f); }

    /// <summary>泡泡浴：花费 10000 金币，健康 +50，心情 +5</summary>
    public void BathPremium() { TryBath(10000L, 50f, 5f); }

    /// <summary>洗澡核心：金币不足→提示不扣钱；足够→扣钱、加健康（钳 100）、加心情、立即存档、提示成功</summary>
    private void TryBath(long cost, float healthGain, float moodGain)
    {
        var gd = GameData.Instance;
        if (gd == null) return;
        if (gd.coins < cost)
        {
            if (bathFeedbackText != null) bathFeedbackText.text = "金币不足";
            return; // 不扣钱
        }
        gd.coins -= cost;
        gd.health = Mathf.Min(100f, gd.health + healthGain);
        if (moodGain > 0f) gd.mood = Mathf.Min(100f, gd.mood + moodGain);
        gd.Save(); // 立即存档
        if (bathFeedbackText != null) bathFeedbackText.text = "洗澡成功";
    }

    // ---------------- 院子逗鸡（任务8） ----------------

    /// <summary>打开逗鸡面板（清空上次提示）</summary>
    public void OpenYardPanel()
    {
        if (yardPanel == null) return;
        if (yardFeedbackText != null) yardFeedbackText.text = "";
        yardPanel.SetActive(true);
    }

    /// <summary>关闭逗鸡面板，恢复正常 UI</summary>
    public void CloseYardPanel()
    {
        if (yardPanel != null) yardPanel.SetActive(false);
    }

    /// <summary>陪它散步：免费，心情 +5</summary>
    public void PlayFree() { TryYard(0L, 5f); }

    /// <summary>豪华玩具：花费 20000 金币，心情 +30</summary>
    public void PlayPremium() { TryYard(20000L, 30f); }

    /// <summary>逗鸡核心：收费项金币不足→提示不扣钱；免费直接走成功；mood 钳 100、立即存档</summary>
    private void TryYard(long cost, float moodGain)
    {
        var gd = GameData.Instance;
        if (gd == null) return;
        if (cost > 0L && gd.coins < cost)
        {
            if (yardFeedbackText != null) yardFeedbackText.text = "金币不足";
            return; // 不扣钱
        }
        gd.coins -= cost; // 免费项为 0，等效不扣
        gd.mood = Mathf.Min(100f, gd.mood + moodGain);
        gd.Save(); // 立即存档
        if (yardFeedbackText != null) yardFeedbackText.text = "逗鸡成功，小鸡很开心！";
    }

    // ---------------- 卧室睡觉（任务9） ----------------

    /// <summary>打开睡觉面板（清空上次提示）</summary>
    public void OpenBedroomPanel()
    {
        if (bedroomPanel == null) return;
        if (bedroomFeedbackText != null) bedroomFeedbackText.text = "";
        bedroomPanel.SetActive(true);
    }

    /// <summary>关闭睡觉面板，恢复正常 UI</summary>
    public void CloseBedroomPanel()
    {
        if (bedroomPanel != null) bedroomPanel.SetActive(false);
    }

    /// <summary>打个盹：免费，健康 +10，心情 +5</summary>
    public void SleepNormal() { TrySleep(0L, 10f, 5f); }

    /// <summary>深度睡眠：花费 5000 金币，健康 +30，心情 +15</summary>
    public void SleepDeep() { TrySleep(5000L, 30f, 15f); }

    /// <summary>睡觉核心：收费项金币不足→提示不扣钱；免费直接走成功；健康/心情钳 100、立即存档</summary>
    private void TrySleep(long cost, float healthGain, float moodGain)
    {
        var gd = GameData.Instance;
        if (gd == null) return;
        if (cost > 0L && gd.coins < cost)
        {
            if (bedroomFeedbackText != null) bedroomFeedbackText.text = "金币不足";
            return; // 不扣钱
        }
        gd.coins -= cost; // 免费项为 0，等效不扣
        gd.health = Mathf.Min(100f, gd.health + healthGain);
        gd.mood = Mathf.Min(100f, gd.mood + moodGain);
        gd.Save(); // 立即存档
        if (bedroomFeedbackText != null) bedroomFeedbackText.text = "小鸡睡得好香！";
    }
}
