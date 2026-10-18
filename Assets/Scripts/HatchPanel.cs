using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 孵蛋面板（2026-10-19 孵化系统 · 第一步：骨架）。
///
/// 产品意图：游戏开场不是"你有一只鸡"，而是"你有一颗蛋，它里面是什么？"
/// 玩家要做的第一件事是【说出愿望】—— 这句愿望会决定它长成什么、以及它破壳后说的第一句话。
///
/// 本步（Loop 1）只验证 UI 通路：面板能弹出、愿望能输入、按钮有响应。
/// 十张牌、天材地宝、破壳动画、AI 生图都在后续轮次接。
///
/// 所有 UI 纯代码创建，场景结构零改动（理由见 UIFactory 类注释）。
/// </summary>
public class HatchPanel : MonoBehaviour
{
    public const int WishMinLength = 10;   // 太短的话"愿望"没有分量，也说不出性格
    public const int WishMaxLength = 100;  // 老板拍板的上限

    private GameObject root;               // 整个面板根（含遮罩）
    private InputField wishInput;          // 愿望输入框
    private Text countText;                // 实时字数提示
    private Text feedbackText;             // 操作反馈
    private Button hatchButton;            // 开始孵化
    private Button closeButton;            // 先不孵

    private string currentWish = "";       // 已确认的愿望（后续轮次会写入存档）

    /// <summary>面板是否正在显示</summary>
    public bool IsOpen { get { return root != null && root.activeSelf; } }

    /// <summary>当前愿望文本（供后续轮次读取）</summary>
    public string CurrentWish { get { return currentWish; } }

    // ================= 构建 =================

    /// <summary>在指定 Canvas 下创建面板并构建全部 UI（默认隐藏）</summary>
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
        root = gameObject;

        // ---- 全屏遮罩（深色，压暗底层房间）----
        UIFactory.CreateFillPanel(root.transform, "Dim", new Color(0f, 0f, 0f, 0.78f));

        // ---- 面板底板（暖褐色，呼应"蛋"的质感）----
        var board = UIFactory.CreatePanel(root.transform, "Board",
            new Vector2(900f, 1300f), Vector2.zero,
            new Color(0.16f, 0.13f, 0.11f, 0.97f));

        // ---- 标题 ----
        UIFactory.CreateText(board.transform, "Title", "一颗蛋",
            new Vector2(820f, 110f), new Vector2(0f, 520f), 72, new Color(1f, 0.93f, 0.78f));

        UIFactory.CreateText(board.transform, "Subtitle", "它里面是什么，谁也不知道。",
            new Vector2(820f, 60f), new Vector2(0f, 438f), 34, new Color(0.85f, 0.78f, 0.68f));

        // ---- 愿望输入区标签 ----
        UIFactory.CreateText(board.transform, "WishLabel", "对它说一个愿望吧",
            new Vector2(820f, 56f), new Vector2(0f, 362f), 38, new Color(1f, 0.88f, 0.7f));

        UIFactory.CreateText(board.transform, "WishHint",
            "它听得懂。说得越细，它越像你想的那个样子。",
            new Vector2(820f, 48f), new Vector2(0f, 310f), 28, new Color(0.72f, 0.66f, 0.6f));

        // ---- 愿望输入框 ----
        wishInput = UIFactory.CreateInputField(board.transform, "WishInput",
            "比如：我想要一只不太爱说话、但会一直陪着我的小家伙……",
            new Vector2(800f, 280f), new Vector2(0f, 150f), 34, WishMaxLength);
        wishInput.onValueChanged.AddListener(OnWishChanged);

        // ---- 字数提示 ----
        countText = UIFactory.CreateText(board.transform, "CountText",
            "0 / " + WishMaxLength, new Vector2(400f, 44f), new Vector2(300f, -22f), 28,
            new Color(0.7f, 0.65f, 0.6f), TextAnchor.MiddleRight);

        // ---- 操作反馈 ----
        feedbackText = UIFactory.CreateText(board.transform, "Feedback", "",
            new Vector2(800f, 56f), new Vector2(0f, -82f), 32, new Color(1f, 0.8f, 0.5f));

        // ---- 主按钮：开始孵化 ----
        hatchButton = UIFactory.CreateButton(board.transform, "BtnHatch", "开始孵化",
            new Vector2(620f, 130f), new Vector2(0f, -200f),
            new Color(0.85f, 0.55f, 0.22f, 1f), OnClickHatch, 46, new Color(1f, 0.98f, 0.94f));

        // ---- 次按钮：先不孵 ----
        closeButton = UIFactory.CreateButton(board.transform, "BtnClose", "先看看别处",
            new Vector2(420f, 92f), new Vector2(0f, -350f),
            new Color(0.32f, 0.3f, 0.28f, 1f), Hide, 34, new Color(0.85f, 0.82f, 0.78f));
    }

    // ================= 显示 / 隐藏 =================

    public void Show()
    {
        if (root == null) return;
        root.SetActive(true);
        RefreshCount();
    }

    public void Hide()
    {
        if (root == null) return;
        root.SetActive(false);
    }

    // ================= 交互 =================

    private void OnWishChanged(string value)
    {
        if (feedbackText != null) feedbackText.text = ""; // 一改字就清掉上次提示
        RefreshCount();
    }

    private void RefreshCount()
    {
        if (countText == null || wishInput == null) return;
        int len = wishInput.text != null ? wishInput.text.Length : 0;
        countText.text = len + " / " + WishMaxLength;

        // 够长了给正反馈：玩家能感知"我这句话它有分量了"
        countText.color = len >= WishMinLength
            ? new Color(0.55f, 0.9f, 0.6f)
            : new Color(0.7f, 0.65f, 0.6f);
    }

    private void OnClickHatch()
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

        // Loop 1 只验证通路：把愿望记下来并给出反馈。真正的孵化在后续轮次接。
        if (feedbackText != null) feedbackText.text = "愿望记下了（" + wish.Length + " 个字）。";
        Debug.Log("[HatchPanel] 收到愿望：" + wish);
    }
}
