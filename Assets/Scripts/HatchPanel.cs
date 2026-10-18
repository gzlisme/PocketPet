using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 孵蛋面板（2026-10-19 孵化系统 · Loop 3：三段式状态机）。
///
/// 三个阶段：
///   ① 愿望（Wish）    —— 玩家对它说 50~100 字的话，这句话决定破壳第一句台词
///   ② 抽牌（Picking） —— 十张背面朝上的牌，先真的抽出 10 个候选物种，玩家挑一张
///   ③ 揭晓（Revealed）—— 抽中的那只活下来，另外九只从这一刻起就不存在了
///
/// 为什么「先定 10 个候选再让玩家挑」而不是「点了再抽」：
/// 如果牌面内容和结果无关联，抽牌就只是视觉表演，玩两天就会被看穿。
/// 现在的做法里，那 9 张没被选中的确实存在过 —— 它们是「未出生的兄弟姐妹」，
/// 后面可以做稀有事件让它们回来见一面，一分钱不花。
///
/// 全部 UI 纯代码创建，场景结构零改动（理由见 UIFactory 类注释）。
/// </summary>
public class HatchPanel : MonoBehaviour
{
    public const int WishMinLength = 10;
    public const int WishMaxLength = 100;

    private const int CardCount = 10;
    private const int CardCols = 5;
    private const float CardW = 150f;
    private const float CardH = 210f;
    private const float CardGapX = 172f;
    private const float CardGapY = 250f;

    private static readonly Color CardBackColor = new Color(0.24f, 0.20f, 0.36f, 1f);
    private static readonly Color CardBackColorAlt = new Color(0.20f, 0.17f, 0.30f, 1f);
    private static readonly Color GoldColor = new Color(1f, 0.80f, 0.32f, 1f);

    private enum Phase { Wish, Picking, Revealed }
    private Phase phase = Phase.Wish;

    // ---- 三个阶段的容器（切换时只换 SetActive）----
    private GameObject wishGroup;
    private GameObject pickGroup;
    private GameObject revealGroup;

    // ---- 愿望阶段 ----
    private InputField wishInput;
    private Text countText;
    private Text feedbackText;

    // ---- 抽牌阶段 ----
    private readonly Button[] cards = new Button[CardCount];
    private readonly Text[] cardLabels = new Text[CardCount];
    private readonly PetSpecies.Def[] candidates = new PetSpecies.Def[CardCount];
    private bool pickingLocked;      // 抽牌过程中的连点锁

    // ---- 揭晓阶段 ----
    private Text revealRarityText;
    private Text revealNameText;
    private Text revealFirstLineText;   // 破壳第一句话（Loop 4 核心）
    private Text revealDescText;
    private Text revealWishText;

    // ---- 运行数据 ----
    private string currentWish = "";
    private string cachedFirstLine = "";   // 本会话内固定的第一句话（Loop 5 改为持久化）
    private PetSpecies.Def result;

    public bool IsOpen { get { return gameObject.activeSelf; } }
    public string CurrentWish { get { return currentWish; } }

    // ================= 构建 =================

    public static HatchPanel Create(Transform canvas)
    {
        var go = UIFactory.NewNode("HatchPanel", canvas);
        UIFactory.FillParent(go);
        var panel = go.AddComponent<HatchPanel>();
        panel.Build();
        panel.Hide();
        return panel;
    }

    private void Build()
    {
        // ---- 全屏遮罩 ----
        UIFactory.CreateFillPanel(transform, "Dim", new Color(0f, 0f, 0f, 0.80f));

        // ---- 面板底板（暖褐色，呼应蛋的质感）----
        var board = UIFactory.CreatePanel(transform, "Board",
            new Vector2(900f, 1300f), Vector2.zero,
            new Color(0.16f, 0.13f, 0.11f, 0.97f));

        BuildWishGroup(board.transform);
        BuildPickGroup(board.transform);
        BuildRevealGroup(board.transform);
    }

    // ---------------- 阶段一：愿望 ----------------

    private void BuildWishGroup(Transform board)
    {
        wishGroup = UIFactory.NewNode("WishGroup", board);
        UIFactory.FillParent(wishGroup);
        var p = wishGroup.transform;

        UIFactory.CreateText(p, "Title", "一颗蛋",
            new Vector2(820f, 110f), new Vector2(0f, 520f), 72, new Color(1f, 0.93f, 0.78f));

        UIFactory.CreateText(p, "Subtitle", "它里面是什么，谁也不知道。",
            new Vector2(820f, 60f), new Vector2(0f, 446f), 34, new Color(0.85f, 0.78f, 0.68f));

        UIFactory.CreateText(p, "WishLabel", "对它说一个愿望吧",
            new Vector2(820f, 56f), new Vector2(0f, 376f), 38, new Color(1f, 0.88f, 0.7f));

        UIFactory.CreateText(p, "WishHint", "它听得懂。说得越细，它越像你想的那个样子。",
            new Vector2(820f, 48f), new Vector2(0f, 326f), 28, new Color(0.72f, 0.66f, 0.6f));

        wishInput = UIFactory.CreateInputField(p, "WishInput",
            "比如：我想要一只不太爱说话、但会一直陪着我的小家伙……",
            new Vector2(800f, 280f), new Vector2(0f, 160f), 34, WishMaxLength);
        wishInput.onValueChanged.AddListener(OnWishChanged);

        countText = UIFactory.CreateText(p, "CountText", "0 / " + WishMaxLength,
            new Vector2(400f, 44f), new Vector2(300f, -12f), 28,
            new Color(0.7f, 0.65f, 0.6f), TextAnchor.MiddleRight);

        feedbackText = UIFactory.CreateText(p, "Feedback", "",
            new Vector2(800f, 56f), new Vector2(0f, -72f), 30, new Color(1f, 0.8f, 0.5f));

        UIFactory.CreateButton(p, "BtnHatch", "说好了",
            new Vector2(620f, 130f), new Vector2(0f, -200f),
            new Color(0.85f, 0.55f, 0.22f, 1f), OnClickToPicking, 46, new Color(1f, 0.98f, 0.94f));

        UIFactory.CreateButton(p, "BtnClose", "先看看别处",
            new Vector2(420f, 92f), new Vector2(0f, -350f),
            new Color(0.32f, 0.3f, 0.28f, 1f), Hide, 34, new Color(0.85f, 0.82f, 0.78f));
    }

    // ---------------- 阶段二：抽牌 ----------------

    private void BuildPickGroup(Transform board)
    {
        pickGroup = UIFactory.NewNode("PickGroup", board);
        UIFactory.FillParent(pickGroup);
        var p = pickGroup.transform;

        UIFactory.CreateText(p, "Title", "挑一张牌",
            new Vector2(820f, 110f), new Vector2(0f, 520f), 64, new Color(1f, 0.93f, 0.78f));

        UIFactory.CreateText(p, "Subtitle", "十种可能，只有一种会活下来。",
            new Vector2(820f, 60f), new Vector2(0f, 448f), 34, new Color(0.85f, 0.78f, 0.68f));

        for (int i = 0; i < CardCount; i++)
        {
            int col = i % CardCols;
            int row = i / CardCols;
            float x = (col - (CardCols - 1) * 0.5f) * CardGapX;
            float y = row == 0 ? 180f : 180f - CardGapY;

            int captured = i; // 闭包捕获：不捕获会全部指向最后一个 i
            var btn = UIFactory.CreateButton(p, "Card" + i, "？",
                new Vector2(CardW, CardH), new Vector2(x, y),
                (i % 2 == 0) ? CardBackColor : CardBackColorAlt,
                () => OnCardClicked(captured), 56, new Color(0.9f, 0.82f, 0.62f, 0.85f));

            cards[i] = btn;
            cardLabels[i] = btn.GetComponentInChildren<Text>();
        }

        UIFactory.CreateText(p, "Hint", "别怕选错 —— 它已经在等你了。",
            new Vector2(820f, 50f), new Vector2(0f, -440f), 30, new Color(0.72f, 0.66f, 0.6f));
    }

    // ---------------- 阶段三：揭晓 ----------------

    private void BuildRevealGroup(Transform board)
    {
        revealGroup = UIFactory.NewNode("RevealGroup", board);
        UIFactory.FillParent(revealGroup);
        var p = revealGroup.transform;

        // 稀有度徽章（颜色随稀有度变化）
        revealRarityText = UIFactory.CreateText(p, "Rarity", "",
            new Vector2(820f, 50f), new Vector2(0f, 540f), 34, Color.white);

        // 物种名（大字号，稀有度色）
        revealNameText = UIFactory.CreateText(p, "Name", "",
            new Vector2(820f, 130f), new Vector2(0f, 440f), 88, Color.white);

        // 破壳第一句话 —— 整个面板最重要的一行，字号比描述大、用引号包着
        revealFirstLineText = UIFactory.CreateText(p, "FirstLine", "",
            new Vector2(780f, 160f), new Vector2(0f, 290f), 36, new Color(1f, 0.92f, 0.76f));

        revealDescText = UIFactory.CreateText(p, "Desc", "",
            new Vector2(760f, 110f), new Vector2(0f, 150f), 30, new Color(0.82f, 0.78f, 0.72f));

        UIFactory.CreateText(p, "WishHead", "你当时说的是：",
            new Vector2(760f, 40f), new Vector2(0f, 40f), 26, new Color(0.58f, 0.54f, 0.50f));

        revealWishText = UIFactory.CreateText(p, "WishEcho", "",
            new Vector2(760f, 120f), new Vector2(0f, -40f), 28, new Color(0.72f, 0.68f, 0.62f));

        UIFactory.CreateButton(p, "BtnDone", "带它回家",
            new Vector2(620f, 130f), new Vector2(0f, -320f),
            new Color(0.85f, 0.55f, 0.22f, 1f), Hide, 46, new Color(1f, 0.98f, 0.94f));
    }

    // ================= 显示 / 隐藏 =================

    public void Show()
    {
        if (GameData.Instance != null && GameData.Instance.isDead) return;

        gameObject.SetActive(true);

        // 已经孵过（有物种）就直接看结果，别让玩家重复孵
        var gd = GameData.Instance;
        if (gd != null && !string.IsNullOrEmpty(gd.speciesId))
        {
            result = gd.CurrentSpecies;
            ShowReveal();
            return;
        }

        SetPhase(Phase.Wish);
        RefreshCount();
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    private void SetPhase(Phase target)
    {
        phase = target;
        if (wishGroup != null) wishGroup.SetActive(target == Phase.Wish);
        if (pickGroup != null) pickGroup.SetActive(target == Phase.Picking);
        if (revealGroup != null) revealGroup.SetActive(target == Phase.Revealed);
    }

    // ================= 阶段一交互 =================

    private void OnWishChanged(string value)
    {
        if (feedbackText != null) feedbackText.text = "";
        RefreshCount();
    }

    private void RefreshCount()
    {
        if (countText == null || wishInput == null) return;
        int len = wishInput.text != null ? wishInput.text.Length : 0;
        countText.text = len + " / " + WishMaxLength;
        countText.color = len >= WishMinLength
            ? new Color(0.55f, 0.9f, 0.6f)
            : new Color(0.7f, 0.65f, 0.6f);
    }

    private void OnClickToPicking()
    {
        string wish = wishInput != null ? wishInput.text : "";
        if (wish != null) wish = wish.Trim();

        if (string.IsNullOrEmpty(wish) || wish.Length < WishMinLength)
        {
            if (feedbackText != null)
                feedbackText.text = "再多说一点吧，它想知道你想要什么。（至少 " + WishMinLength + " 个字）";
            return;
        }

        currentWish = wish;
        RollCandidates();
        ResetCards();
        pickingLocked = false;
        SetPhase(Phase.Picking);
    }

    /// <summary>一次性抽出十个候选物种。尽量不重复（重复了「十种可能」就名不副实）。</summary>
    private void RollCandidates()
    {
        var gd = GameData.Instance;
        int pity = gd != null ? gd.pityCount : 0;

        for (int i = 0; i < CardCount; i++)
        {
            PetSpecies.Def picked = null;
            for (int attempt = 0; attempt < 12; attempt++)
            {
                picked = PetSpecies.Roll(pity);
                if (!AlreadyInCandidates(picked, i)) break;
            }
            candidates[i] = picked != null ? picked : PetSpecies.Default;
        }
    }

    private bool AlreadyInCandidates(PetSpecies.Def def, int count)
    {
        for (int i = 0; i < count; i++)
            if (candidates[i] != null && candidates[i].id == def.id) return true;
        return false;
    }

    private void ResetCards()
    {
        for (int i = 0; i < CardCount; i++)
        {
            if (cards[i] == null) continue;
            var img = cards[i].GetComponent<Image>();
            if (img != null) img.color = (i % 2 == 0) ? CardBackColor : CardBackColorAlt;
            if (cardLabels[i] != null)
            {
                cardLabels[i].text = "？";
                cardLabels[i].fontSize = 56;
                cardLabels[i].color = new Color(0.9f, 0.82f, 0.62f, 0.85f);
            }
            cards[i].transform.localScale = Vector3.one;
        }
    }

    // ================= 阶段二交互 =================

    private void OnCardClicked(int index)
    {
        if (pickingLocked || phase != Phase.Picking) return;
        pickingLocked = true;
        StartCoroutine(PickRoutine(index));
    }

    private System.Collections.IEnumerator PickRoutine(int index)
    {
        // ---- 选中牌放大、其余变暗（0.28 秒）----
        const float dur = 0.28f;
        float t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / dur);

            for (int i = 0; i < CardCount; i++)
            {
                if (cards[i] == null) continue;
                var img = cards[i].GetComponent<Image>();
                if (i == index)
                {
                    cards[i].transform.localScale = Vector3.one * Mathf.Lerp(1f, 1.14f, k);
                    if (img != null) img.color = Color.Lerp((index % 2 == 0) ? CardBackColor : CardBackColorAlt, GoldColor, k);
                    if (cardLabels[i] != null) cardLabels[i].color = Color.Lerp(new Color(0.9f, 0.82f, 0.62f, 0.85f), new Color(0.25f, 0.15f, 0.02f, 1f), k);
                }
                else if (img != null)
                {
                    var c = img.color;
                    c.a = Mathf.Lerp(1f, 0.18f, k);
                    img.color = c;
                }
            }
            yield return null;
        }

        // ---- 翻开：落孵（写存档）----
        var gd = GameData.Instance;
        result = gd != null
            ? gd.HatchSpeciesWith(candidates[index], currentWish)
            : candidates[index];

        if (cardLabels[index] != null && result != null)
        {
            cardLabels[index].text = result.displayName;
            cardLabels[index].fontSize = 40;
        }

        yield return new WaitForSeconds(0.55f);
        ShowReveal();
    }

    // ================= 阶段三 =================

    private void ShowReveal()
    {
        if (result == null) result = PetSpecies.Default;

        // 愿望：本次会话用 currentWish，重新打开面板时回落到存档里的 lastWish
        string wish = currentWish;
        if (string.IsNullOrEmpty(wish) && GameData.Instance != null) wish = GameData.Instance.lastWish;

        if (revealRarityText != null)
        {
            revealRarityText.text = "· " + PetSpecies.GetRarityName(result.rarity) + " · " + result.family + "系 ·";
            revealRarityText.color = PetSpecies.GetRarityColor(result.rarity);
        }
        if (revealNameText != null)
        {
            revealNameText.text = result.displayName;
            revealNameText.color = PetSpecies.GetRarityColor(result.rarity);
        }

        // 第一句话：只在第一次进揭晓时生成一次，本会话内不再变（避免来回打开时台词乱跳）
        if (revealFirstLineText != null)
        {
            if (string.IsNullOrEmpty(cachedFirstLine))
                cachedFirstLine = PetVoice.FirstLine(result, wish);
            revealFirstLineText.text = "「" + cachedFirstLine + "」";
        }

        if (revealDescText != null) revealDescText.text = result.desc;
        if (revealWishText != null) revealWishText.text = string.IsNullOrEmpty(wish) ? "" : "「" + wish + "」";

        SetPhase(Phase.Revealed);
    }
}
