using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UI 交互逻辑（阶段二 + 阶段三）：
/// - ToggleDrawer/CloseDrawer：☰ 开关抽屉 + 抽屉外透明点击区 / 抽屉内 X 按钮关闭（任务6.1）
/// - SwitchRoom：任务4假切换方法原样保留（四房间现均绑定各自功能面板，无按钮再调用）
/// - OpenFeedPanel/FeedNormal/FeedPremium/CloseFeedPanel：餐厅喂食（任务6.2，扣金币加状态并立即存档）
/// - OpenBathPanel/BathNormal/BathPremium/CloseBathPanel：浴室洗澡（任务7+17D，扣金币加清洁度（递减）并立即存档，健康不再直接加）
/// - OpenYardPanel/PlayFree/PlayPremium/CloseYardPanel：院子逗鸡（任务8，扣金币加心情并立即存档）
/// - OpenBedroomPanel/CloseBedroomPanel：卧室面板（任务9+17D+17F：睡觉改全局被动机制，两睡觉按钮已删；SleepNormal/SleepDeep/TrySleep 保留为死代码）
/// - OpenShopPanel/CloseShopPanel/BuyOrToggleItem/RefreshChickDecoration：商店（任务10，买装饰、装备/卸下、小鸡色块显示）
/// - OnClickSignIn/RefreshSignInButton：抽屉内每日签到（任务14，领10万金币，第7天连签再+100万，已签置灰）
/// - OpenAchievementPanel/CloseAchievementPanel/RefreshAchievementPanel：成就面板（任务15C，成就名称/进度/状态列表）
/// - ShowDeathPopup/CloseDeathPopup/OnClickRebirth：死亡与领养新蛋（任务17E，health=0 死亡弹窗、重置回蛋）
/// - OnClickPatChick：抚摸小鸡（任务17F-补丁+17G，每天 3 次、每次 +5 心情，卧室面板按钮）
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
    [SerializeField] private Text bedroomFeedbackText;// 睡觉状态提示文本（任务17D：小鸡在睡觉/小鸡醒着）
    [SerializeField] private GameObject shopPanel;       // 商店面板（任务10）
    [SerializeField] private Text shopCoinsText;        // 商店内金币显示
    [SerializeField] private Text shopFeedbackText;     // 商店结果提示文本
    [SerializeField] private Button[] shopItemButtons; // 任务17H：五个商品按钮，顺序必须与 ShopItem.AllItems 一致
    [SerializeField] private GameObject slotHat;         // 小鸡帽子槽位（Chick 子对象）
    [SerializeField] private GameObject slotScarf;       // 小鸡围巾槽位
    [SerializeField] private GameObject slotGlasses;    // 小鸡墨镜槽位
    [SerializeField] private GameObject evolutionPopup;  // 进化弹窗根（任务12，Canvas 最后子对象，默认不激活）
    [SerializeField] private Text popupText;             // 进化弹窗文本（PopupPanel/PopupText）
    [SerializeField] private Button signInButton;        // 抽屉内签到按钮 Btn_SignIn（任务14）
    [SerializeField] private Text signInText;             // 签到按钮文本 Text_SignIn（任务14）
    [SerializeField] private GameObject achievementPanel; // 成就面板 AchievementPanel（任务15C）
    [SerializeField] private Text achievementListText;    // 成就列表文本 AchievementListText（任务15C）
    [SerializeField] private GameObject diaryPanel;      // 碎碎念面板 DiaryPanel（任务16B）
    [SerializeField] private InputField diaryInputField; // 碎碎念输入框 InputField_Diary（任务16B）
    [SerializeField] private Text diaryListText;          // 碎碎念列表文本 DiaryListText（任务16B）
    [SerializeField] private GameObject deathPopup;   // 死亡弹窗 DeathPopup（任务17E，Canvas 最后子对象，默认不激活）
    [SerializeField] private Text deathPopupText;    // 死亡弹窗文本 Text_DeathMsg（任务17E）

    private Camera cam;
    private string currentRoom;
    private float hintUntil = -999f;
    private float popupShownAt = -999f;              // 任务12：进化弹窗弹出时刻（激活满 2 秒自动关闭）

    // ---- 进化（阶段三任务11）----
    private Transform chickTr;      // Chick 缓存（位置不动，只改 scale/color）
    private SpriteRenderer chickSr;
    private string lastStage;       // 上次阶段（阶段变化 Console 提示 + 避免每帧重设）

    // 假切换的背景色映射（任务4原样保留）
    private static readonly Color RoomYard    = new Color(76f / 255f, 175f / 255f, 80f / 255f);    // 院子 #4CAF50
    private static readonly Color RoomDining   = new Color(1f, 215f / 255f, 0f);                    // 餐厅 #FFD700
    private static readonly Color RoomBedroom  = new Color(138f / 255f, 43f / 255f, 226f / 255f);   // 卧室 #8A2BE2
    private static readonly Color RoomBathroom = new Color(0f, 191f / 255f, 1f);                    // 浴室 #00BFFF

    private void Awake()
    {
        cam = Camera.main;
        var chick = GameObject.Find("Chick");
        if (chick != null)
        {
            chickTr = chick.transform;
            chickSr = chick.GetComponent<SpriteRenderer>();
        }
        ApplyEvolutionVisual(); // 任务11：进场即应用进化视觉
    }

    private void Start()
    {
        RefreshSignInButton(); // 任务14：进场刷新签到按钮（Start 时全部 Awake 已完成，GameData 必已读档——工单拍板 B 方案）
    }

    private void Update()
    {
        // 任务17E：死亡检测——只弹一次死亡弹窗；死亡期间不跑进化检测等
        var gdDeath = GameData.Instance;
        if (gdDeath != null && gdDeath.isDead)
        {
            if (deathPopup != null && !deathPopup.activeSelf) ShowDeathPopup();
            return; // 死亡时不再跑进化检测等
        }

        // 任务12：进化弹窗激活满 2 秒自动关闭（放最前，不依赖 GameData/小鸡状态）
        if (evolutionPopup != null && evolutionPopup.activeSelf && Time.time - popupShownAt >= 2f)
            CloseEvolutionPopup();

        // 任务11：阶段变化时才重设（避免每帧重设）；任务17G 起只比阶段（形态分化已删）
        var gd = GameData.Instance;
        if (gd == null || chickTr == null) return;
        string stage = gd.GetCurrentStage();
        if (stage == lastStage) return;
        if (lastStage != null && StageOrder(stage) > StageOrder(lastStage))
            ShowEvolutionPopup(lastStage, stage); // 任务12：阶段升高弹「进化了！」（回退/相同不弹）
        ApplyEvolutionVisual();
    }

    /// <summary>☰ 按钮：开/关抽屉；打开时先关全部功能面板再开抽屉并激活外部点击区，关闭时隐藏点击区（任务6.1 + 互斥单）</summary>
    public void ToggleDrawer()
    {
        if (drawerPanel == null) return;
        bool open = !drawerPanel.activeSelf;
        if (open)
        {
            CloseAllPanels(); // 抽屉与面板互斥：开抽屉先关五个功能面板；关抽屉不动面板
            RefreshSignInButton(); // 修正单：开抽屉时刷新签到按钮
        }
        drawerPanel.SetActive(open);
        if (closeDrawerArea != null) closeDrawerArea.SetActive(open);
    }

    /// <summary>关闭抽屉（外部透明点击区 / 抽屉内 X 按钮共用，任务6.1）</summary>
    public void CloseDrawer()
    {
        if (drawerPanel != null) drawerPanel.SetActive(false);
        if (closeDrawerArea != null) closeDrawerArea.SetActive(false);
    }

    /// <summary>关闭全部六个功能面板（不关抽屉）——抽屉打开分支与 CloseAllPanelsAndDrawer 共用（任务15C 起含成就面板）</summary>
    private void CloseAllPanels()
    {
        CloseFeedPanel();
        CloseBathPanel();
        CloseYardPanel();
        CloseBedroomPanel();
        CloseShopPanel();
        CloseAchievementPanel(); // 任务15C：开其他面板/开抽屉时成就面板一并关闭
        CloseDiaryPanel();       // 任务16B：开其他面板/开抽屉时碎碎念面板一并关闭
    }

    /// <summary>Bug-3 修复：打开任一房间面板前统一调用——关闭全部五个功能面板，并关抽屉（DrawerPanel 置不激活 + 隐藏抽屉外透明点击区，复用 CloseDrawer 任务6语义）</summary>
    private void CloseAllPanelsAndDrawer()
    {
        CloseAllPanels();
        CloseDrawer();
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
        if (GameData.Instance != null && GameData.Instance.isDead) return; // 任务17E：死亡期间禁止互动
        CloseAllPanelsAndDrawer();
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

    /// <summary>豪华零食：花费 50000 金币，饥饿 +30，心情 +10（任务17：+40→+30）</summary>
    public void FeedPremium() { TryFeed(50000L, 30f, 10f); }

    /// <summary>喂食核心：金币不足→提示不扣钱；足够→扣钱、加状态（钳 100）、立即存档、提示成功</summary>
    private void TryFeed(long cost, float hungerGain, float moodGain)
    {
        var gd = GameData.Instance;
        if (gd == null || gd.isDead) return; // 任务17E：死亡期间禁止互动
        gd.NotifyInteraction(); // 任务17F：夜间互动唤醒 5 分钟（不论金币是否够）
        if (gd.coins < cost)
        {
            if (feedFeedbackText != null) feedFeedbackText.text = "金币不足";
            return; // 不扣钱
        }
        gd.coins -= cost;
        gd.hunger = Mathf.Max(0f, gd.hunger + hungerGain);
        if (moodGain > 0f) gd.mood = Mathf.Clamp(gd.mood + moodGain, 0f, 100f); // 任务17G：心情封顶 100
        gd.Save(); // 立即存档
        gd.AddFeedCount(); // 任务15B：互动成功计数（成就系统）
        if (feedFeedbackText != null)
        {
            feedFeedbackText.text = "喂食成功" + GetAchievementProgressSuffix("feed"); // 任务15D：反馈带成就进度
            string msg = gd.ConsumeAchievementMessage(); // 任务15D：本次解锁的成就提示追加（取走即清空）
            if (!string.IsNullOrEmpty(msg)) feedFeedbackText.text += " · " + msg;
        }
        // 任务15E：互动成功飘字——加值向上飘；cost>0 再飘一条扣金币向下（金币不足已在上方 return，到不了这里）
        string gainText = "+" + hungerGain + " 食物";
        if (moodGain > 0f) gainText += " +" + moodGain + " 心情";
        SpawnFloatingText(gainText, FloatGainColor, 0f, 200f, 120f, 1.5f);
        if (cost > 0L) SpawnFloatingText("-" + cost + " 金币", FloatCostColor, 0f, 100f, -120f, 1.5f);
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
        if (GameData.Instance != null && GameData.Instance.isDead) return; // 任务17E：死亡期间禁止互动
        CloseAllPanelsAndDrawer();
        if (bathPanel == null) return;
        if (bathFeedbackText != null) bathFeedbackText.text = "";
        bathPanel.SetActive(true);
    }

    /// <summary>关闭洗澡面板，恢复正常 UI</summary>
    public void CloseBathPanel()
    {
        if (bathPanel != null) bathPanel.SetActive(false);
    }

    /// <summary>普通淋浴：花费 2000 金币（任务17D：加清洁度，健康不再直接加）</summary>
    public void BathNormal() { TryBath(2000L, 20f, 0f); }

    /// <summary>泡泡浴：花费 10000 金币，心情 +5（任务17D：加清洁度，健康不再直接加）</summary>
    public void BathPremium() { TryBath(10000L, 50f, 5f); }

    /// <summary>洗澡核心（任务17D）：金币不足→提示不扣钱；足够→扣钱、加清洁度（递减：≤50→+30、≤80→+15、其余 +5，钳 100）、加心情、立即存档</summary>
    private void TryBath(long cost, float healthGain, float moodGain)
    {
        var gd = GameData.Instance;
        if (gd == null || gd.isDead) return; // 任务17E：死亡期间禁止互动
        gd.NotifyInteraction(); // 任务17F：夜间互动唤醒 5 分钟（不论金币是否够）
        if (gd.coins < cost)
        {
            if (bathFeedbackText != null) bathFeedbackText.text = "金币不足";
            return; // 不扣钱
        }
        gd.coins -= cost;
        // 洗澡加清洁度，递减：越接近满加越少（任务17D；healthGain 参数仅保留签名兼容，健康不再直接加）
        float cleanGain = gd.clean <= 50f ? 30f : (gd.clean <= 80f ? 15f : 5f);
        gd.clean = Mathf.Min(100f, gd.clean + cleanGain);
        if (moodGain > 0f) gd.mood = Mathf.Clamp(gd.mood + moodGain, 0f, 100f); // 任务17G：心情封顶 100
        gd.Save(); // 立即存档
        gd.AddBathCount(); // 任务15B：互动成功计数（成就系统）
        if (bathFeedbackText != null)
        {
            bathFeedbackText.text = "洗澡成功" + GetAchievementProgressSuffix("bath"); // 任务15D：反馈带成就进度
            string msg = gd.ConsumeAchievementMessage(); // 任务15D：本次解锁的成就提示追加（取走即清空）
            if (!string.IsNullOrEmpty(msg)) bathFeedbackText.text += " · " + msg;
        }
        // 任务15E：互动成功飘字——加值向上飘；cost>0 再飘一条扣金币向下（金币不足已在上方 return，到不了这里）
        string gainText = "+" + cleanGain + " 清洁"; // 任务17D：洗澡飘字改清洁
        if (moodGain > 0f) gainText += " +" + moodGain + " 心情";
        SpawnFloatingText(gainText, FloatGainColor, 0f, 200f, 120f, 1.5f);
        if (cost > 0L) SpawnFloatingText("-" + cost + " 金币", FloatCostColor, 0f, 100f, -120f, 1.5f);
    }

    // ---------------- 院子逗鸡（任务8） ----------------

    /// <summary>打开逗鸡面板（清空上次提示）</summary>
    public void OpenYardPanel()
    {
        if (GameData.Instance != null && GameData.Instance.isDead) return; // 任务17E：死亡期间禁止互动
        CloseAllPanelsAndDrawer();
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
        if (gd == null || gd.isDead) return; // 任务17E：死亡期间禁止互动
        gd.NotifyInteraction(); // 任务17F：夜间互动唤醒 5 分钟（不论金币是否够）
        if (cost > 0L && gd.coins < cost)
        {
            if (yardFeedbackText != null) yardFeedbackText.text = "金币不足";
            return; // 不扣钱
        }
        gd.coins -= cost; // 免费项为 0，等效不扣
        gd.mood = Mathf.Clamp(gd.mood + moodGain, 0f, 100f); // 任务17G：心情封顶 100
        gd.Save(); // 立即存档
        gd.AddYardCount(); // 任务15B：互动成功计数（成就系统）
        if (yardFeedbackText != null)
        {
            yardFeedbackText.text = "逗鸡成功，小鸡很开心！" + GetAchievementProgressSuffix("yard"); // 任务15D：反馈带成就进度
            string msg = gd.ConsumeAchievementMessage(); // 任务15D：本次解锁的成就提示追加（取走即清空）
            if (!string.IsNullOrEmpty(msg)) yardFeedbackText.text += " · " + msg;
        }
        // 任务15E：互动成功飘字——加值向上飘；免费项（cost==0）只飘加值，不飘扣金币
        SpawnFloatingText("+" + moodGain + " 心情", FloatGainColor, 0f, 200f, 120f, 1.5f);
        if (cost > 0L) SpawnFloatingText("-" + cost + " 金币", FloatCostColor, 0f, 100f, -120f, 1.5f);
    }

    // ---------------- 卧室睡觉（任务9；任务17F：场景两睡觉按钮已删，SleepNormal/SleepDeep/TrySleep 保留为死代码待后续清理单） ----------------

    /// <summary>打开睡觉面板（清空上次提示）</summary>
    public void OpenBedroomPanel()
    {
        if (GameData.Instance != null && GameData.Instance.isDead) return; // 任务17E：死亡期间禁止互动
        CloseAllPanelsAndDrawer();
        if (bedroomPanel == null) return;
        if (bedroomFeedbackText != null) bedroomFeedbackText.text = "";
        bedroomPanel.SetActive(true);
    }

    /// <summary>关闭睡觉面板，恢复正常 UI</summary>
    public void CloseBedroomPanel()
    {
        if (bedroomPanel != null) bedroomPanel.SetActive(false);
    }

    /// <summary>打个盹（任务17D：睡觉不再加值/扣钱，仅展示小鸡状态）</summary>
    public void SleepNormal() { TrySleep(0L, 10f, 5f); }

    /// <summary>深度睡眠（任务17D：睡觉不再加值/扣钱，仅展示小鸡状态）</summary>
    public void SleepDeep() { TrySleep(5000L, 30f, 15f); }

    /// <summary>睡觉核心（任务17D）：不加值不扣钱，仅按本地时间展示小鸡睡觉（20:00–6:00）/醒着状态</summary>
    private void TrySleep(long cost, float healthGain, float moodGain)
    {
        var gd = GameData.Instance;
        if (gd == null || gd.isDead || bedroomFeedbackText == null) return; // 任务17E：死亡期间禁止互动
        DateTime nowLocal = DateTime.Now;
        int h = nowLocal.Hour;
        bool sleepTime = h >= 20 || h < 6;
        bedroomFeedbackText.text = (sleepTime ? "小鸡在睡觉（20:00-6:00）" : "小鸡醒着")
            + "｜玩一会就休息吧";
    }

    // ---------------- 商店（阶段三任务10） ----------------

    /// <summary>打开商店面板：关其余面板与抽屉、清提示、刷新金币显示</summary>
    public void OpenShopPanel()
    {
        if (GameData.Instance != null && GameData.Instance.isDead) return; // 任务17E：死亡期间禁止互动
        CloseAllPanelsAndDrawer();
        if (shopPanel == null) return;
        if (shopFeedbackText != null) shopFeedbackText.text = "";
        RefreshShopCoins();
        RefreshShopButtons(); // 任务17H：按购买/穿着状态刷新按钮文字
        shopPanel.SetActive(true);
    }

    /// <summary>关闭商店面板</summary>
    public void CloseShopPanel()
    {
        if (shopPanel != null) shopPanel.SetActive(false);
    }

    /// <summary>买/切换商品：未拥有→扣钱购买并装备；已装备→卸下；已拥有未装备→装备（同槽自动换装）</summary>
    public void BuyOrToggleItem(string itemId)
    {
        var item = ShopItem.FindById(itemId);
        if (item == null) return;
        var gd = GameData.Instance;
        if (gd == null) return;

        bool owned = IsOwned(gd, itemId);
        string equippedId = GetEquipped(gd, item.slot);

        if (!owned)
        {
            if (gd.coins < item.price)
            {
                if (shopFeedbackText != null) shopFeedbackText.text = "金币不足";
                return; // 不扣钱
            }
            gd.coins -= item.price;
            AddOwned(gd, itemId);
            SetEquipped(gd, item.slot, itemId); // 购买即装备（同槽自动替换旧物品）
            if (shopFeedbackText != null) shopFeedbackText.text = "购买成功";
        }
        else if (equippedId == itemId)
        {
            SetEquipped(gd, item.slot, ""); // 卸下
            if (shopFeedbackText != null) shopFeedbackText.text = "已卸下";
        }
        else
        {
            SetEquipped(gd, item.slot, itemId); // 装备（同槽自动替换旧物品）
            if (shopFeedbackText != null) shopFeedbackText.text = "已装备";
        }

        gd.Save(); // 每次操作后立即存档
        RefreshChickDecoration();
        RefreshShopCoins();
        RefreshShopButtons(); // 任务17H：按购买/穿着状态刷新按钮文字
    }

    /// <summary>根据已装备项刷新小鸡三个装饰槽位的显示与颜色</summary>
    private void RefreshChickDecoration()
    {
        var gd = GameData.Instance;
        if (gd == null) return;
        RefreshSlot(slotHat, gd.equippedHat);
        RefreshSlot(slotScarf, gd.equippedScarf);
        RefreshSlot(slotGlasses, gd.equippedGlasses);
    }

    /// <summary>单个槽位：无装备→隐藏；有装备→显示并着色（蛋阶段一律隐藏，任务11.4）</summary>
    private void RefreshSlot(GameObject slotObj, string itemId)
    {
        if (slotObj == null || slotObj.transform.childCount == 0) return;
        var deco = slotObj.transform.GetChild(0).gameObject; // 槽位下的装饰精灵子物体
        var gdNow = GameData.Instance;
        if (gdNow != null && gdNow.GetCurrentStage() == "蛋") { deco.SetActive(false); return; }
        var item = ShopItem.FindById(itemId);
        if (item == null) { deco.SetActive(false); return; }
        deco.SetActive(true);
        deco.GetComponent<SpriteRenderer>().color = item.color;
    }

    /// <summary>刷新商店内金币显示</summary>
    private void RefreshShopCoins()
    {
        var gd = GameData.Instance;
        if (shopCoinsText != null && gd != null)
            shopCoinsText.text = "当前金币：" + gd.coins;
    }

    /// <summary>任务17H：刷新商店按钮文字三态（未购买/已拥有未穿/使用中）</summary>
    private void RefreshShopButtons()
    {
        if (shopItemButtons == null) return;
        var gd = GameData.Instance;
        if (gd == null) return;
        for (int i = 0; i < ShopItem.AllItems.Count && i < shopItemButtons.Length; i++)
        {
            var item = ShopItem.AllItems[i];
            var btn = shopItemButtons[i];
            if (btn == null) continue;
            var txt = btn.GetComponentInChildren<Text>();
            if (txt == null) continue;

            bool owned = IsOwned(gd, item.id);
            string equippedId = GetEquipped(gd, item.slot);
            string priceWan = (item.price / 10000L) + "万";

            if (!owned)
                txt.text = item.displayName + "（" + priceWan + "）";
            else if (equippedId == item.id)
                txt.text = item.displayName + "（使用中 · 点击卸下）";
            else
                txt.text = item.displayName + "（已拥有 · 点击装备）";
        }
    }

    // ---- 装备字段读写小工具（任务10） ----

    private static string GetEquipped(GameData gd, string slot)
    {
        switch (slot)
        {
            case "hat": return gd.equippedHat;
            case "scarf": return gd.equippedScarf;
            case "glasses": return gd.equippedGlasses;
            default: return "";
        }
    }

    private static void SetEquipped(GameData gd, string slot, string itemId)
    {
        switch (slot)
        {
            case "hat": gd.equippedHat = itemId; break;
            case "scarf": gd.equippedScarf = itemId; break;
            case "glasses": gd.equippedGlasses = itemId; break;
        }
    }

    private static bool IsOwned(GameData gd, string itemId)
    {
        if (string.IsNullOrEmpty(gd.ownedItems)) return false;
        foreach (var s in gd.ownedItems.Split(','))
            if (s == itemId) return true;
        return false;
    }

    private static void AddOwned(GameData gd, string itemId)
    {
        gd.ownedItems = string.IsNullOrEmpty(gd.ownedItems) ? itemId : gd.ownedItems + "," + itemId;
    }

    // ---------------- 进化系统（阶段三任务11） ----------------

    /// <summary>
    /// 按 GameData 阶段设置小鸡大小与颜色（任务11.4；17G 起成年鸡固定金色、不再分形态）：
    /// 蛋=0.6 白+隐藏装饰 / 幼鸡=0.8 / 少年鸡=1.0 / 成年鸡=1.2 固定金 #FFD700。
    /// 只改 scale 与 color，不改位置 (0,0,0)，不改槽位局部参数。
    /// </summary>
    public void ApplyEvolutionVisual()
    {
        var gd = GameData.Instance;
        if (gd == null || chickTr == null || chickSr == null) return;
        string stage = gd.GetCurrentStage();

        // 任务11.6：阶段变化 Console 提示（首次初始化不算变化，不输出）
        if (lastStage != null && lastStage != stage)
            Debug.Log("[PocketPet] 进化：" + lastStage + " → " + stage);
        lastStage = stage;

        switch (stage)
        {
            case "蛋":
                chickTr.localScale = new Vector3(0.6f, 0.6f, 1f);
                chickSr.color = new Color(1f, 1f, 1f);                 // #FFFFFF
                HideDeco(slotHat); HideDeco(slotScarf); HideDeco(slotGlasses);
                break;
            case "幼鸡":
                chickTr.localScale = new Vector3(0.8f, 0.8f, 1f);
                chickSr.color = new Color(1f, 215f / 255f, 0f);       // #FFD700
                RefreshChickDecoration();
                break;
            case "少年鸡":
                chickTr.localScale = new Vector3(1f, 1f, 1f);
                chickSr.color = new Color(1f, 215f / 255f, 0f);       // #FFD700
                RefreshChickDecoration();
                break;
            default: // 成年鸡
                chickTr.localScale = new Vector3(1.2f, 1.2f, 1f);
                chickSr.color = new Color(1f, 215f / 255f, 0f); // 固定金色 #FFD700（任务17G：形态分化已删）
                RefreshChickDecoration();
                break;
        }
    }

    /// <summary>隐藏单个装饰（蛋阶段用）</summary>
    private void HideDeco(GameObject slotObj)
    {
        if (slotObj == null || slotObj.transform.childCount == 0) return;
        slotObj.transform.GetChild(0).gameObject.SetActive(false);
    }

    // ---------------- 进化仪式弹窗（任务12） ----------------

    /// <summary>阶段升级弹窗：激活 EvolutionPopup、设置文本、关全部面板与抽屉、记录弹出时刻（2 秒自动关）</summary>
    private void ShowEvolutionPopup(string fromStage, string toStage)
    {
        if (evolutionPopup == null) return;
        evolutionPopup.SetActive(true);
        if (popupText != null) popupText.text = "进化了！\n" + fromStage + " → " + toStage;
        CloseAllPanelsAndDrawer();
        popupShownAt = Time.time;
    }

    /// <summary>关闭进化弹窗（DimOverlay 按钮 onClick 持久化接线与 Update 2 秒自动关闭共用）</summary>
    public void CloseEvolutionPopup()
    {
        if (evolutionPopup != null) evolutionPopup.SetActive(false);
    }

    /// <summary>阶段序：蛋 0 &lt; 幼鸡 1 &lt; 少年鸡 2 &lt; 成年鸡 3（未知 -1）——只升不退才弹窗</summary>
    private static int StageOrder(string stage)
    {
        switch (stage)
        {
            case "蛋": return 0;
            case "幼鸡": return 1;
            case "少年鸡": return 2;
            case "成年鸡": return 3;
            default: return -1;
        }
    }

    // ---------------- 死亡与领养新蛋（任务17E） ----------------

    /// <summary>死亡弹窗：关全部面板与抽屉、置文案、激活（只弹一次，不自动关，必须点"领养新蛋"）</summary>
    public void ShowDeathPopup()
    {
        if (deathPopup == null) return;
        CloseAllPanelsAndDrawer();
        if (deathPopupText != null) deathPopupText.text = "小鸡走了…";
        deathPopup.SetActive(true);
    }

    /// <summary>关闭死亡弹窗（任务17E）</summary>
    public void CloseDeathPopup()
    {
        if (deathPopup != null) deathPopup.SetActive(false);
    }

    /// <summary>领养新蛋按钮：重置小鸡（ReviveAsEgg 回蛋）、关死亡弹窗、刷新进化视觉（任务17E）</summary>
    public void OnClickRebirth()
    {
        var gd = GameData.Instance;
        if (gd == null) return;
        gd.ReviveAsEgg();
        CloseDeathPopup();
        ApplyEvolutionVisual();
    }

    // ---------------- 抚摸小鸡（任务17F-补丁） ----------------

    /// <summary>抚摸小鸡：调 GameData.PatChick（每天 3 次上限，17G 起 +5 心情），死亡期间拦截；成功/失败均飘字提示</summary>
    public void OnClickPatChick()
    {
        var gd = GameData.Instance;
        if (gd == null || gd.isDead) return;
        if (gd.PatChick())
            SpawnFloatingText("+5 心情", FloatGainColor, 0f, 200f, 120f, 1.5f);
        else
            SpawnFloatingText("今天摸够啦", Color.white, 0f, 200f, 120f, 1.5f);
    }

    // ---------------- 签到（阶段三任务14） ----------------

    /// <summary>签到按钮点击：当天已签→直接返回不做事；否则执行签到并刷新按钮显示</summary>
    public void OnClickSignIn()
    {
        var gd = GameData.Instance;
        if (gd == null) return;
        if (!gd.CanSignInToday()) return; // 当天已签，不做事
        gd.DoSignIn();
        RefreshSignInButton();
    }

    /// <summary>刷新抽屉入口按钮（任务16B：签到入口已改名碎碎念）——始终显示"碎碎念"且可点（签到并入发布流程，不再按签到状态置灰）</summary>
    public void RefreshSignInButton()
    {
        if (signInButton == null || signInText == null) return;
        signInText.text = "碎碎念";
        signInButton.interactable = true;
    }

    // ---------------- 成就面板（阶段四任务15C） ----------------

    /// <summary>打开成就面板：先关全部功能面板与抽屉，再刷新成就列表，最后激活面板</summary>
    public void OpenAchievementPanel()
    {
        if (GameData.Instance != null && GameData.Instance.isDead) return; // 任务17E：死亡期间禁止互动
        CloseAllPanelsAndDrawer();
        RefreshAchievementPanel();
        if (achievementPanel != null) achievementPanel.SetActive(true);
    }

    /// <summary>关闭成就面板</summary>
    public void CloseAchievementPanel()
    {
        if (achievementPanel != null) achievementPanel.SetActive(false);
    }

    /// <summary>刷新成就列表：每行「名字 进度/目标 状态」（状态=已达成/未完成），多行用 \n 拼接，写入 achievementListText</summary>
    public void RefreshAchievementPanel()
    {
        var gd = GameData.Instance;
        if (achievementListText == null || gd == null) return;
        achievementListText.color = Color.white; // 任务15D：半透明黑底上列表文字改白，保证可读
        if (achievementPanel != null) // 任务15D：标题同样改白（运行时着色，不改场景）
        {
            var titleTr = achievementPanel.transform.Find("Title");
            var titleText = titleTr != null ? titleTr.GetComponent<Text>() : null;
            if (titleText != null) titleText.color = Color.white;
        }
        string text = "";
        foreach (var def in GameData.AchievementDefs)
        {
            if (text.Length > 0) text += "\n";
            text += def.name + " " + gd.GetAchievementProgress(def.id) + "/" + def.target + " "
                    + (gd.IsAchievementUnlocked(def.id) ? "已达成" : "未完成");
        }
        achievementListText.text = text;
    }

    // ---------------- 成就反馈（阶段四任务15D） ----------------

    /// <summary>互动成功反馈的成就进度后缀（任务15D；17D补丁删 sleep 分支）：feed 按当前进度显示下一目标，bath/yard 固定 /10；未知类型返回空串</summary>
    private string GetAchievementProgressSuffix(string type)
    {
        var gd = GameData.Instance;
        if (gd == null) return "";
        switch (type)
        {
            case "feed":
                if (gd.feedCount < 10) return "（新手饲养员 " + gd.feedCount + "/10）";
                if (gd.feedCount < 100) return "（金牌饲养员 " + gd.feedCount + "/100）";
                return "（喂食成就已全达成）";
            case "bath": return "（爱干净 " + gd.bathCount + "/10）";
            case "yard": return "（游戏伙伴 " + gd.yardCount + "/10）";
            default: return "";
        }
    }

    // ---------------- 互动飘字反馈（阶段四任务15E） ----------------

    private static readonly Color FloatGainColor = new Color(0.3f, 1f, 0.3f); // 加值飘字颜色（工单指定绿）
    private static readonly Color FloatCostColor = new Color(1f, 0.3f, 0.3f); // 扣金币飘字颜色（工单指定红）

    /// <summary>
    /// 互动成功飘字（任务15E）：Canvas 下动态创建 400×80 Text，duration 秒内 y 从 startY 线性到 startY+deltaY、
    /// alpha 1→0 渐隐后自毁。字体取 feedFeedbackText.font，为空退 bathFeedbackText.font，仍为空静默返回。
    /// </summary>
    private void SpawnFloatingText(string content, Color color, float startX, float startY, float deltaY, float duration)
    {
        Font font = feedFeedbackText != null ? feedFeedbackText.font : null;
        if (font == null && bathFeedbackText != null) font = bathFeedbackText.font;
        if (font == null) return; // 无可用字体：放弃飘字，不报错
        var go = new GameObject("FloatingText");
        go.transform.SetParent(transform, false); // UIManager 挂在 Canvas 上 → 飘字即 Canvas 子物体
        var text = go.AddComponent<Text>();        // Graphic 依赖自动补 RectTransform + CanvasRenderer
        text.raycastTarget = false;               // 任务15F：飘字不拦截射线，存续期不挡面板按钮点击
        var rt = text.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f); // 锚点居中
        rt.anchoredPosition = new Vector2(startX, startY);
        rt.sizeDelta = new Vector2(400f, 80f);
        text.font = font;
        text.fontSize = 48;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = color;
        text.text = content;
        StartCoroutine(FloatTextRoutine(text, startY, deltaY, duration)); // 协程驱动动画（本单专属授权，仅此飘字使用）
    }

    /// <summary>飘字动画协程（任务15E，仅 SpawnFloatingText 调用）：y 线性位移 + alpha 1→0，结束后 Destroy</summary>
    private System.Collections.IEnumerator FloatTextRoutine(Text text, float startY, float deltaY, float duration)
    {
        var rt = (RectTransform)text.transform;
        var c = text.color;
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / duration);
            rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, startY + deltaY * k);
            c.a = 1f - k;
            text.color = c;
            yield return null;
        }
        Destroy(rt.gameObject);
    }

    // ---------------- 碎碎念面板（阶段四任务16B） ----------------

    /// <summary>打开碎碎念面板：关全部功能面板与抽屉、刷新列表、激活面板</summary>
    public void OpenDiaryPanel()
    {
        if (GameData.Instance != null && GameData.Instance.isDead) return; // 任务17E：死亡期间禁止互动
        CloseAllPanelsAndDrawer();
        RefreshDiaryPanel();
        if (diaryPanel != null) diaryPanel.SetActive(true);
    }

    /// <summary>关闭碎碎念面板</summary>
    public void CloseDiaryPanel()
    {
        if (diaryPanel != null) diaryPanel.SetActive(false);
    }

    /// <summary>刷新碎碎念列表：按 ";" 拆分、倒序（最新在最上），每行"日期 内容"，空记录显示提示语</summary>
    public void RefreshDiaryPanel()
    {
        if (diaryListText == null) return;
        var gd = GameData.Instance;
        if (gd == null) return;
        string raw = gd.GetDiaryEntries();
        if (raw == "")
        {
            diaryListText.text = "还没有碎碎念，写一条吧";
            return;
        }
        string[] parts = raw.Split(';');
        string text = "";
        for (int i = parts.Length - 1; i >= 0; i--)
        {
            if (text.Length > 0) text += "\n";
            text += parts[i].Replace("|", " ");
        }
        diaryListText.text = text;
    }

    /// <summary>发布碎碎念：空白输入直接返回；否则入账（每天第一条附带签到奖励）、清空输入框、刷新列表；reward>0 飘金币、reward==0 飘"已记录"</summary>
    public void OnClickPostDiary()
    {
        var gd = GameData.Instance;
        if (gd == null || diaryInputField == null) return;
        string content = diaryInputField.text;
        if (string.IsNullOrWhiteSpace(content)) return;
        long reward = gd.AddDiaryEntry(content);
        diaryInputField.text = "";
        RefreshDiaryPanel();
        if (reward > 0)
            SpawnFloatingText("+" + reward + " 金币", new Color(1f, 0.85f, 0.3f), 0f, 200f, 120f, 1.5f);
        else if (reward == 0)
            SpawnFloatingText("已记录", Color.white, 0f, 200f, 120f, 1.5f);
    }
}
