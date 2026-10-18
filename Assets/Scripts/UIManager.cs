using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UI 交互逻辑（阶段二 + 阶段三）：
/// - ToggleDrawer/CloseDrawer：☰ 开关抽屉 + 抽屉外透明点击区 / 抽屉内 X 按钮关闭（任务6.1）
/// - SwitchRoom：任务4假切换方法原样保留（四房间现均绑定各自功能面板，无按钮再调用）
/// - OpenFeedPanel/FeedNormal/FeedPremium/CloseFeedPanel：餐厅喂食（任务6.2，扣金币加状态并立即存档）
/// - OpenBathPanel/BathNormal/BathPremium/CloseBathPanel：浴室洗澡（任务7+17D，扣金币加清洁度（递减）并立即存档，健康不再直接加）
/// - OpenYardPanel/PlayFree/PlayPremium/CloseYardPanel：院子逗鸡（任务8，扣金币加心情并立即存档）
/// - OpenBedroomPanel/CloseBedroomPanel：卧室面板（任务9+17D+17F：睡觉改全局被动机制，两睡觉按钮已删）
/// - OpenShopPanel/CloseShopPanel/BuyOrToggleItem/RefreshChickDecoration：商店（任务10，买装饰、装备/卸下、小鸡色块显示）
/// - RefreshSignInButton：抽屉内碎碎念入口按钮刷新（任务16B：恒显"碎碎念"可点，签到并入发布流程）
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

    // ---- 玩具系统（wb 分支：豪华玩具下线，玩具改由商店买断）----
    private Button btnYardToy;           // 院子里的玩具按钮（复用场景原 Btn_PlayPremium，不删对象）
    private Text txtYardToy;             // 该按钮上的文字
    private readonly List<Button> runtimeShopButtons = new List<Button>(); // 商店按钮运行时列表（含按需动态补的）

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

        // 豪华玩具下线（玩具改由商店买断）：原按钮不删、改为「玩具入口」复用。
        // 没买玩具时点它提示去商店，买了之后点它陪玩。场景结构零改动。
        if (yardPanel != null)
        {
            // 递归查找而非 Transform.Find 固定路径：按钮已被搬进 YardButtons 自动布局容器，
            // 固定路径 "Btn_PlayPremium" 会失效（Find 只找直接子对象，不递归）。
            // 用 GetComponentsInChildren 按名字找，对任何层级变动免疫。
            foreach (var b in yardPanel.GetComponentsInChildren<Button>(true))
            {
                if (b.name != "Btn_PlayPremium") continue;
                btnYardToy = b;
                txtYardToy = b.GetComponentInChildren<Text>(true);
                break;
            }
        }
        RefreshYardToyButton();

        // 商店按钮按商品数量动态补足并统一重排：以后再加商品不用再动场景布局
        BuildShopButtons();
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
        {
            gd.AddMemory("长大了，变成" + stage + "了"); // wb：回忆录（随下一次存档落盘）
            ShowEvolutionPopup(lastStage, stage); // 任务12：阶段升高弹「进化了！」（回退/相同不弹）
        }
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

    /// <summary>普通饲料：花费 10 金币，饥饿 +15</summary>
    public void FeedNormal() { TryFeed(10L, 15f, 0f); }

    /// <summary>豪华零食：花费 60 金币，饥饿 +30，心情 +10（任务17：+40→+30）</summary>
    public void FeedPremium() { TryFeed(60L, 30f, 10f); }

    /// <summary>喂食核心：金币不足→提示不扣钱；足够→扣钱、加食物（不封顶）与心情（钳 100）、立即存档、提示成功</summary>
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
        float moodBefore = gd.mood; // 2026-10-18：记加值前心情，用于飘"实际到账量"
        gd.hunger = Mathf.Max(0f, gd.hunger + hungerGain);
        if (moodGain > 0f) gd.mood = Mathf.Clamp(gd.mood + moodGain, 0f, 100f); // 任务17G：心情封顶 100
        gd.Save(); // 立即存档
        gd.AddFeedCount(); // 任务15B：互动成功计数（成就系统）
        if (feedFeedbackText != null)
        {
            feedFeedbackText.text = "喂食成功"; // 2026-10-18：去掉成就进度后缀（老板反馈：每次都挂 49/100 太吵）
            string msg = gd.ConsumeAchievementMessage(); // 任务15D：本次解锁的成就提示追加（取走即清空）
            if (!string.IsNullOrEmpty(msg)) feedFeedbackText.text += " · " + msg;
        }
        // 任务15E：互动成功飘字——加值向上飘；cost>0 再飘一条扣金币向下（金币不足已在上方 return，到不了这里）
        string gainText = "+" + hungerGain + " 食物";
        float moodActual = gd.mood - moodBefore; // 2026-10-18：心情满时不再虚报（原来照飘 +10 实际 +0）
        if (moodGain > 0f && moodActual >= 0.5f) gainText += " +" + Mathf.RoundToInt(moodActual) + " 心情";
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

    /// <summary>普通淋浴：花费 20 金币（任务17D：加清洁度，健康不再直接加）</summary>
    public void BathNormal() { TryBath(20L, 20f, 0f); }

    /// <summary>泡泡浴：花费 120 金币，心情 +5（任务17D：加清洁度，健康不再直接加）</summary>
    public void BathPremium() { TryBath(120L, 50f, 5f); }

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
        // 2026-10-18：清洁已满（或可加量不足 1 点）→ 不扣钱（花金币买 +0 太伤感情），只提示。
        // 老板反馈：清洁满了还照扣 120。递减档提前算，用"真实可加量"判定，顺带避免飘 +0。
        float cleanGain = gd.clean <= 50f ? 30f : (gd.clean <= 80f ? 15f : 5f);
        float cleanActual = Mathf.Min(100f, gd.clean + cleanGain) - gd.clean;
        if (cleanActual < 1f)
        {
            if (bathFeedbackText != null) bathFeedbackText.text = "它已经干干净净了";
            SpawnFloatingText("它香喷喷的", Color.white, 0f, 200f, 120f, 1.5f);
            return;
        }
        gd.coins -= cost;
        // 洗澡加清洁度，递减：越接近满加越少（任务17D；healthGain 参数仅保留签名兼容，健康不再直接加）
        float moodBefore = gd.mood;
        gd.clean = Mathf.Min(100f, gd.clean + cleanGain);
        if (moodGain > 0f) gd.mood = Mathf.Clamp(gd.mood + moodGain, 0f, 100f); // 任务17G：心情封顶 100
        gd.Save(); // 立即存档
        gd.AddBathCount(); // 任务15B：互动成功计数（成就系统）
        if (bathFeedbackText != null)
        {
            bathFeedbackText.text = "洗澡成功"; // 2026-10-18：去掉成就进度后缀
            string msg = gd.ConsumeAchievementMessage(); // 任务15D：本次解锁的成就提示追加（取走即清空）
            if (!string.IsNullOrEmpty(msg)) bathFeedbackText.text += " · " + msg;
        }
        // 2026-10-18：飘字改"实际到账量"（原来飘理论值，清洁 98 时实际 +2 却写 +5）；cleanActual 在上方已算好
        float moodActual = gd.mood - moodBefore;
        string gainText = "+" + Mathf.RoundToInt(cleanActual) + " 清洁";
        if (moodGain > 0f && moodActual >= 0.5f) gainText += " +" + Mathf.RoundToInt(moodActual) + " 心情";
        SpawnFloatingText(gainText, FloatGainColor, 0f, 200f, 120f, 1.5f);
        if (cost > 0L) SpawnFloatingText("-" + cost + " 金币", FloatCostColor, 0f, 100f, -120f, 1.5f);
    }

    // ---------------- 院子逗鸡（任务8） ----------------

    /// <summary>打开逗鸡面板（清空上次提示；2026-10-19 夜班：提示行改为显示今日剩余散步次数）</summary>
    public void OpenYardPanel()
    {
        if (GameData.Instance != null && GameData.Instance.isDead) return; // 任务17E：死亡期间禁止互动
        CloseAllPanelsAndDrawer();
        if (yardPanel == null) return;
        if (yardFeedbackText != null)
        {
            var gdYard = GameData.Instance;
            int left = gdYard != null ? gdYard.GetYardRemainingToday() : 0;
            yardFeedbackText.text = left > 0 ? "今天还能陪它走 " + left + " 次" : "今天陪它玩够了，明天再来";
        }
        yardPanel.SetActive(true);
    }

    /// <summary>关闭逗鸡面板，恢复正常 UI</summary>
    public void CloseYardPanel()
    {
        if (yardPanel != null) yardPanel.SetActive(false);
    }

    /// <summary>陪它散步：免费，心情 +5</summary>
    public void PlayFree() { TryYard(0L, 5f); }

    /// <summary>
    /// 院子里的玩具按钮（复用原 Btn_PlayPremium，不删场景对象）。两种状态：
    /// 没买玩具 → 提示去商店；买了 → 陪玩当前选中的玩具（每日 5 次，心情 +玩具加成）。
    /// 玩具不受心情软天花板（80）限制，是花钱突破免费项上限的途径。
    /// </summary>
    public void PlayPremium()
    {
        var gd = GameData.Instance;
        if (gd == null || gd.isDead) return; // 死亡期间禁止互动
        gd.NotifyInteraction(); // 任务17F：夜间互动唤醒 5 分钟

        if (!gd.HasAnyToy())
        {
            if (yardFeedbackText != null) yardFeedbackText.text = "玩具在商店里，去买一个吧";
            SpawnFloatingText("先去商店买玩具", Color.white, 0f, 200f, 120f, 1.5f);
            return;
        }

        var toy = gd.PlayToy();
        if (toy == null)
        {
            if (yardFeedbackText != null) yardFeedbackText.text = "它今天玩够了，明天再来";
            SpawnFloatingText("它玩累啦", Color.white, 0f, 200f, 120f, 1.5f);
            RefreshYardToyButton();
            return;
        }

        if (yardFeedbackText != null) yardFeedbackText.text = "玩" + toy.displayName + "，它开心坏了";
        SpawnFloatingText(ToyMoodText(gd.mood), FloatGainColor, 0f, 200f, 120f, 1.5f);
        RefreshYardToyButton();
    }

    /// <summary>逗鸡核心：收费项金币不足→提示不扣钱；A+ 起加两道闸（心情软天花板、每日次数）</summary>
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

        // A+ 上限①：心情软天花板。必须先判这个、再扣次数 —— 心情已满时点一下等于白烧一次日额度。
        // 这里不记成就、不存档：什么都没发生，不该留下痕迹。
        if (gd.mood >= GameData.MoodSoftCap)
        {
            if (yardFeedbackText != null) yardFeedbackText.text = "它现在开心得不得了";
            SpawnFloatingText("小鸡乐开花", Color.white, 0f, 200f, 120f, 1.5f);
            return;
        }

        // A+ 上限②：每日散步次数（跨天自动清零）
        if (!gd.TryConsumeYardToday())
        {
            if (yardFeedbackText != null) yardFeedbackText.text = "今天陪它玩够了，明天再来";
            SpawnFloatingText("小鸡跑累啦", Color.white, 0f, 200f, 120f, 1.5f);
            return;
        }

        gd.coins -= cost; // 免费项为 0，等效不扣
        gd.mood = Mathf.Clamp(gd.mood + moodGain, 0f, 100f); // 任务17G：心情封顶 100
        gd.Save(); // 立即存档
        gd.AddYardCount(); // 任务15B：互动成功计数（成就系统）
        if (yardFeedbackText != null)
        {
            yardFeedbackText.text = "逗鸡成功，小鸡很开心！"; // 2026-10-18：去掉成就进度后缀（21/10 越界显示太闹心）
            string msg = gd.ConsumeAchievementMessage(); // 任务15D：本次解锁的成就提示追加（取走即清空）
            if (!string.IsNullOrEmpty(msg)) yardFeedbackText.text += " · " + msg;
        }
        // 任务15E：互动成功飘字——加值向上飘；免费项（cost==0）只飘加值，不飘扣金币
        SpawnFloatingText(WalkMoodText(gd.mood), FloatGainColor, 0f, 200f, 120f, 1.5f);
        if (cost > 0L) SpawnFloatingText("-" + cost + " 金币", FloatCostColor, 0f, 100f, -120f, 1.5f);
    }

    // ---------------- 卧室睡觉（任务9；任务17F：睡觉改全局被动机制，场景两睡觉按钮已删） ----------------

    /// <summary>打开睡觉面板（2026-10-19 夜班：原本只清空提示行，现改为显示今日剩余抚摸次数——睡觉按钮删掉后这行一直闲置）</summary>
    public void OpenBedroomPanel()
    {
        if (GameData.Instance != null && GameData.Instance.isDead) return; // 任务17E：死亡期间禁止互动
        CloseAllPanelsAndDrawer();
        if (bedroomPanel == null) return;
        if (bedroomFeedbackText != null)
        {
            var gdBed = GameData.Instance;
            int left = gdBed != null ? gdBed.GetPatRemainingToday() : 0;
            bedroomFeedbackText.text = left > 0 ? "今天还能摸摸它 " + left + " 次" : "它今天被摸得心满意足啦";
        }
        bedroomPanel.SetActive(true);
    }

    /// <summary>关闭睡觉面板，恢复正常 UI</summary>
    public void CloseBedroomPanel()
    {
        if (bedroomPanel != null) bedroomPanel.SetActive(false);
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

        // 玩具（wb 分支）：买断后不"装备"到小鸡身上，而是选为「当前玩的玩具」，去院子里陪玩
        if (item.IsToy)
        {
            if (!owned)
            {
                if (gd.coins < item.price)
                {
                    if (shopFeedbackText != null) shopFeedbackText.text = "金币不足";
                    return; // 不扣钱
                }
                gd.coins -= item.price;
                AddOwned(gd, itemId);
                gd.equippedToy = itemId; // 买下即选中，省得再点一次
                if (shopFeedbackText != null) shopFeedbackText.text = "买到了！去院子陪它玩";
            }
            else if (gd.equippedToy == itemId)
            {
                if (shopFeedbackText != null) shopFeedbackText.text = "正在玩这个";
            }
            else
            {
                gd.equippedToy = itemId;
                if (shopFeedbackText != null) shopFeedbackText.text = "换成" + item.displayName + "了";
            }
            gd.Save();
            RefreshShopCoins();
            RefreshShopButtons();
            RefreshYardToyButton();
            return;
        }

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
            shopCoinsText.text = "当前金币：" + GameData.FormatCoins(gd.coins);
    }

    /// <summary>任务17H：刷新商店按钮文字（装饰品三态 / 玩具三态，各自文案不同）</summary>
    private void RefreshShopButtons()
    {
        if (runtimeShopButtons.Count == 0) return; // 尚未构建
        var gd = GameData.Instance;
        if (gd == null) return;

        for (int i = 0; i < runtimeShopButtons.Count && i < ShopItem.AllItems.Count; i++)
        {
            var item = ShopItem.AllItems[i];
            var btn = runtimeShopButtons[i];
            if (btn == null) continue;
            var txt = btn.GetComponentInChildren<Text>();
            if (txt == null) continue;

            bool owned = IsOwned(gd, item.id);
            string priceText = GameData.FormatCoins(item.price);

            // 玩具：没有"卸下"概念，只有 未购买 / 正在玩 / 已拥有待换上
            // 2026-10-19 夜班：未购买与正在玩两态补上心情加成——4 个玩具只显示价格，玩家没法横向比较，只能盲买
            if (item.IsToy)
            {
                string moodText = "心情+" + Mathf.RoundToInt(item.toyMoodGain);
                if (!owned)
                    txt.text = item.displayName + "（" + priceText + " · " + moodText + "）";
                else if (gd.equippedToy == item.id)
                    txt.text = item.displayName + "（正在玩 · " + moodText + "）";
                else
                    txt.text = item.displayName + "（已拥有 · 点击换上）";
                continue;
            }

            string equippedId = GetEquipped(gd, item.slot);

            if (!owned)
                txt.text = item.displayName + "（" + priceText + "）";
            else if (equippedId == item.id)
                txt.text = item.displayName + "（使用中 · 点击卸下）";
            else
                txt.text = item.displayName + "（已拥有 · 点击装备）";
        }
    }

    // ---- 商店按钮动态排布（wb 分支：商品数量不再被场景布局锁死） ----

    /// <summary>
    /// 构建商店按钮列表：先用场景里已绑定的按钮，商品数超出时以最后一个为模板动态补足，
    /// 再统一重排位置并重新绑定点击事件。此后加商品只需改 ShopItem.AllItems，不必再动场景。
    /// </summary>
    private void BuildShopButtons()
    {
        runtimeShopButtons.Clear();
        if (shopItemButtons != null)
        {
            foreach (var b in shopItemButtons)
                if (b != null) runtimeShopButtons.Add(b);
        }
        if (runtimeShopButtons.Count == 0) return; // 场景没绑按钮，什么都不做

        int need = ShopItem.AllItems.Count;

        // 场景按钮比商品多：多余的藏起来，避免留下无商品的空按钮
        if (runtimeShopButtons.Count > need)
        {
            for (int i = need; i < runtimeShopButtons.Count; i++)
                if (runtimeShopButtons[i] != null) runtimeShopButtons[i].gameObject.SetActive(false);
            runtimeShopButtons.RemoveRange(need, runtimeShopButtons.Count - need);
        }

        // 商品比场景按钮多：复制最后一个按钮补足
        var template = runtimeShopButtons[runtimeShopButtons.Count - 1];
        while (runtimeShopButtons.Count < need)
        {
            var clone = Instantiate(template, template.transform.parent);
            clone.name = "Btn_Shop_Auto" + runtimeShopButtons.Count;
            runtimeShopButtons.Add(clone);
        }

        LayoutShopButtons();
        BindShopButtons();
        RefreshShopButtons();
    }

    /// <summary>
    /// 把按钮按数量平均铺在面板中部：数量越多单个越矮，间距恒定。
    /// 5 个商品时算出的位置与原手工布局完全一致（h=110、步长 130），不会破坏现有观感。
    /// </summary>
    private void LayoutShopButtons()
    {
        int n = Mathf.Min(runtimeShopButtons.Count, ShopItem.AllItems.Count);
        if (n == 0) return;
        const float areaTop = 375f;     // 标题下方起点
        const float areaBottom = -290f; // 关闭按钮上方终点
        const float gap = 20f;          // 间距，与原布局一致
        float area = areaTop - areaBottom;
        float h = Mathf.Clamp((area - (n - 1) * gap) / n, 56f, 110f);
        float y = areaTop - h / 2f;
        for (int i = 0; i < n; i++)
        {
            var rt = runtimeShopButtons[i].GetComponent<RectTransform>();
            if (rt == null) continue;
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(700f, h);
            rt.anchoredPosition = new Vector2(0f, y);
            y -= (h + gap);
        }
    }

    /// <summary>按顺序把按钮重新绑到对应商品，不依赖场景里 Inspector 的手工绑定</summary>
    private void BindShopButtons()
    {
        for (int i = 0; i < runtimeShopButtons.Count && i < ShopItem.AllItems.Count; i++)
        {
            int idx = i; // 必须用局部变量捕获：直接用 i 会让所有按钮都绑到最后一个商品
            var btn = runtimeShopButtons[i];
            if (btn == null) continue;
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() => BuyOrToggleItem(ShopItem.AllItems[idx].id));
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
            SpawnFloatingText(PatMoodText(gd.mood), FloatGainColor, 0f, 200f, 120f, 1.5f);
        else
            SpawnFloatingText("它舒服得眯起眼", Color.white, 0f, 200f, 120f, 1.5f); // 3 次限的是"加心情"不是"摸"，文案只描述状态、不提次数
    }

    /// <summary>抚摸成功文案（按加完后的心情值分档；飘字 Text 宽 400 / 字号 48，文案必须 ≤8 汉字，防截断）</summary>
    private static string PatMoodText(float m)
    {
        if (m >= 96f) return "宝宝爱你";
        if (m >= 90f) return "宝宝稀罕你";
        if (m >= 80f) return "宝宝喜欢你";
        if (m >= 65f) return "好舒服呀";
        if (m >= 45f) return "再摸摸嘛";
        if (m >= 25f) return "宝宝闷闷的";
        return "宝宝不开心";
    }

    /// <summary>散步成功文案（按加完后的心情值分档；与抚摸区分语气；文案必须 ≤8 汉字）</summary>
    private static string WalkMoodText(float m)
    {
        if (m >= 96f) return "飞起来啦";
        if (m >= 90f) return "撒欢儿跑两圈";
        if (m >= 80f) return "追着影子跑";
        if (m >= 65f) return "蹦蹦跳跳";
        if (m >= 45f) return "歪头看看你";
        if (m >= 25f) return "提不起劲";
        return "蹲在角落";
    }

    /// <summary>陪玩玩具文案（按玩完后的心情值分档；比散步更闹腾；文案必须 ≤8 汉字防截断）</summary>
    private static string ToyMoodText(float m)
    {
        if (m >= 96f) return "它追着玩具跑";
        if (m >= 90f) return "它玩疯了";
        if (m >= 80f) return "它扑过来抢";
        if (m >= 65f) return "它玩得正欢";
        if (m >= 45f) return "它歪头看着";
        if (m >= 25f) return "它蹭了蹭玩具";
        return "它慢慢凑过来";
    }

    /// <summary>刷新院子玩具按钮文字：没买玩具 / 有玩具(带剩余次数) / 今日用完，三态</summary>
    private void RefreshYardToyButton()
    {
        if (txtYardToy == null) return;
        var gd = GameData.Instance;
        if (gd == null) return;

        if (!gd.HasAnyToy())
        {
            txtYardToy.text = "玩具去商店买";
            return;
        }

        var toy = ShopItem.FindById(gd.equippedToy);
        if (toy == null)
        {
            txtYardToy.text = "去商店挑个玩具"; // 有玩具但没选中（兜底）
            return;
        }

        int left = gd.GetToyRemainingToday();
        txtYardToy.text = left > 0
            ? "玩" + toy.displayName + "（心情 +" + Mathf.RoundToInt(toy.toyMoodGain) + "，今日还剩 " + left + " 次）"
            : toy.displayName + "（今天玩够了）";
    }

    // ---------------- 签到（阶段三任务14） ----------------

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

    /// <summary>
    /// 刷新回忆录/碎碎念面板（wb 起二合一）：
    /// 上半部分是小鸡回忆录（系统自动记录，只显示最近 8 条），下半部分是玩家手写的碎碎念（最近 10 条）。
    /// 两者分开存储、互不覆盖；显示条数受限只是为了排版，存档本身不设上限。
    /// </summary>
    public void RefreshDiaryPanel()
    {
        if (diaryListText == null) return;
        var gd = GameData.Instance;
        if (gd == null) return;

        // 2026-10-19 夜班：这个 Text 是固定 700×520 的静态文本（场景无 ScrollRect），垂直溢出为截断，
        // 内容一多下方（正是最新碎碎念）就被裁掉。改 BestFit 自动缩字号：内容少时仍是最初的 40 号。
        diaryListText.resizeTextForBestFit = true;
        diaryListText.resizeTextMaxSize = 40;
        diaryListText.resizeTextMinSize = 16;

        string text = "";

        // 签到提示：签到藏在"发碎碎念"流程里（每天第一条附带金币奖励），玩家不知道 → 面板顶部亮明状态
        if (gd.HasSignedInToday())
            text += "今天已签到，金币已入账\n\n";
        else
            text += "今天发碎碎念可领 " + gd.GetTodayReward() + " 金币\n\n";

        // ---- 回忆录（自动记录）----
        string mem = gd.GetMemories();
        if (!string.IsNullOrEmpty(mem))
        {
            string[] mp = mem.Split(';');
            int fromMem = Mathf.Max(0, mp.Length - 8); // 只展示最近 8 条
            text += "— 小鸡回忆录 —\n";
            for (int i = mp.Length - 1; i >= fromMem; i--)
                text += mp[i].Replace("|", " ") + "\n";
            text += "\n";
        }

        // ---- 碎碎念（玩家手写）----
        string raw = gd.GetDiaryEntries();
        if (string.IsNullOrEmpty(raw))
        {
            text += "— 碎碎念 —\n还没有，写一条吧";
        }
        else
        {
            string[] parts = raw.Split(';');
            int fromDiary = Mathf.Max(0, parts.Length - 10); // 只展示最近 10 条
            text += "— 碎碎念 —\n";
            for (int i = parts.Length - 1; i >= fromDiary; i--)
            {
                text += parts[i].Replace("|", " ");
                if (i > fromDiary) text += "\n";
            }
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
            SpawnFloatingText("+" + GameData.FormatCoins(reward) + " 金币", new Color(1f, 0.85f, 0.3f), 0f, 200f, 120f, 1.5f); // 单A：改走分段格式化
        else if (reward == 0)
            SpawnFloatingText("已记录", Color.white, 0f, 200f, 120f, 1.5f);
    }
}
