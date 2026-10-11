using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UI 交互逻辑（阶段二 + 阶段三）：
/// - ToggleDrawer/CloseDrawer：☰ 开关抽屉 + 抽屉外透明点击区 / 抽屉内 X 按钮关闭（任务6.1）
/// - SwitchRoom：任务4假切换方法原样保留（四房间现均绑定各自功能面板，无按钮再调用）
/// - OpenFeedPanel/FeedNormal/FeedPremium/CloseFeedPanel：餐厅喂食（任务6.2，扣金币加状态并立即存档）
/// - OpenBathPanel/BathNormal/BathPremium/CloseBathPanel：浴室洗澡（任务7，扣金币加健康并立即存档）
/// - OpenYardPanel/PlayFree/PlayPremium/CloseYardPanel：院子逗鸡（任务8，扣金币加心情并立即存档）
/// - OpenBedroomPanel/SleepNormal/SleepDeep/CloseBedroomPanel：卧室睡觉（任务9，扣金币加健康/心情并立即存档）
/// - OpenShopPanel/CloseShopPanel/BuyOrToggleItem/RefreshChickDecoration：商店（任务10，买装饰、装备/卸下、小鸡色块显示）
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
    [SerializeField] private GameObject shopPanel;       // 商店面板（任务10）
    [SerializeField] private Text shopCoinsText;        // 商店内金币显示
    [SerializeField] private Text shopFeedbackText;     // 商店结果提示文本
    [SerializeField] private GameObject slotHat;         // 小鸡帽子槽位（Chick 子对象）
    [SerializeField] private GameObject slotScarf;       // 小鸡围巾槽位
    [SerializeField] private GameObject slotGlasses;    // 小鸡墨镜槽位

    private Camera cam;
    private string currentRoom;
    private float hintUntil = -999f;

    // ---- 进化（阶段三任务11）----
    private Transform chickTr;      // Chick 缓存（位置不动，只改 scale/color）
    private SpriteRenderer chickSr;
    private string lastStage;       // 上次阶段（阶段变化 Console 提示 + 避免每帧重设）
    private string lastForm;        // 上次成年形态（careScore 跨档变色用）

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

    private void Update()
    {
        // 任务11：阶段或成年形态变化时才重设（避免每帧重设）
        var gd = GameData.Instance;
        if (gd == null || chickTr == null) return;
        string stage = gd.GetCurrentStage();
        string form = stage == "成年鸡" ? gd.GetFinalForm() : "";
        if (stage == lastStage && form == lastForm) return;
        ApplyEvolutionVisual();
    }

    /// <summary>☰ 按钮：开/关抽屉；打开时先关全部功能面板再开抽屉并激活外部点击区，关闭时隐藏点击区（任务6.1 + 互斥单）</summary>
    public void ToggleDrawer()
    {
        if (drawerPanel == null) return;
        bool open = !drawerPanel.activeSelf;
        if (open) CloseAllPanels(); // 抽屉与面板互斥：开抽屉先关五个功能面板；关抽屉不动面板
        drawerPanel.SetActive(open);
        if (closeDrawerArea != null) closeDrawerArea.SetActive(open);
    }

    /// <summary>关闭抽屉（外部透明点击区 / 抽屉内 X 按钮共用，任务6.1）</summary>
    public void CloseDrawer()
    {
        if (drawerPanel != null) drawerPanel.SetActive(false);
        if (closeDrawerArea != null) closeDrawerArea.SetActive(false);
    }

    /// <summary>关闭全部五个功能面板（不关抽屉）——抽屉打开分支与 CloseAllPanelsAndDrawer 共用</summary>
    private void CloseAllPanels()
    {
        CloseFeedPanel();
        CloseBathPanel();
        CloseYardPanel();
        CloseBedroomPanel();
        CloseShopPanel();
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
        GameData.Instance.careScore += 10; // 任务11：互动成功加分（金币不足不加分）
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
        GameData.Instance.careScore += 10; // 任务11：互动成功加分（金币不足不加分）
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
        if (gd == null) return;
        if (cost > 0L && gd.coins < cost)
        {
            if (yardFeedbackText != null) yardFeedbackText.text = "金币不足";
            return; // 不扣钱
        }
        GameData.Instance.careScore += 10; // 任务11：互动成功加分（免费项也加，金币不足不加）
        gd.coins -= cost; // 免费项为 0，等效不扣
        gd.mood = Mathf.Min(100f, gd.mood + moodGain);
        gd.Save(); // 立即存档
        if (yardFeedbackText != null) yardFeedbackText.text = "逗鸡成功，小鸡很开心！";
    }

    // ---------------- 卧室睡觉（任务9） ----------------

    /// <summary>打开睡觉面板（清空上次提示）</summary>
    public void OpenBedroomPanel()
    {
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
        GameData.Instance.careScore += 10; // 任务11：互动成功加分（免费项也加，金币不足不加）
        gd.coins -= cost; // 免费项为 0，等效不扣
        gd.health = Mathf.Min(100f, gd.health + healthGain);
        gd.mood = Mathf.Min(100f, gd.mood + moodGain);
        gd.Save(); // 立即存档
        if (bedroomFeedbackText != null) bedroomFeedbackText.text = "小鸡睡得好香！";
    }

    // ---------------- 商店（阶段三任务10） ----------------

    /// <summary>打开商店面板：关其余面板与抽屉、清提示、刷新金币显示</summary>
    public void OpenShopPanel()
    {
        CloseAllPanelsAndDrawer();
        if (shopPanel == null) return;
        if (shopFeedbackText != null) shopFeedbackText.text = "";
        RefreshShopCoins();
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
    /// 按 GameData 阶段设置小鸡大小与颜色（任务11.4）：
    /// 蛋=0.6 白+隐藏装饰 / 幼鸡=0.8 / 少年鸡=1.0 / 成年鸡=1.2 且按 careScore 分化三色。
    /// 只改 scale 与 color，不改位置 (0,0,0)，不改槽位局部参数。
    /// </summary>
    public void ApplyEvolutionVisual()
    {
        var gd = GameData.Instance;
        if (gd == null || chickTr == null || chickSr == null) return;
        string stage = gd.GetCurrentStage();
        string form = stage == "成年鸡" ? gd.GetFinalForm() : "";

        // 任务11.6：阶段变化 Console 提示（首次初始化不算变化，不输出）
        if (lastStage != null && lastStage != stage)
            Debug.Log("[PocketPet] 进化：" + lastStage + " → " + stage);
        lastStage = stage;
        lastForm = form;

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
                if (form == "漂亮鸡") chickSr.color = new Color(1f, 215f / 255f, 0f);          // #FFD700
                else if (form == "普通鸡") chickSr.color = new Color(240f / 255f, 192f / 255f, 32f / 255f); // #F0C020
                else chickSr.color = new Color(176f / 255f, 176f / 255f, 176f / 255f);        // #B0B0B0
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
}
