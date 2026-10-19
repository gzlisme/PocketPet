using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 图鉴面板（2026-10-19 孵化系统 · Loop 9）。
///
/// 产品逻辑：16 个物种此前只在抽中那一刻可见，错过就是黑箱。
/// 图鉴的目的不是收集完成度（一家三口凑不齐 16 只），是让玩家知道自己错过了什么——
/// 「传说那格还是？？？，下一颗蛋会不会就是它」，这就是继续孵蛋的理由。
///
/// 可见性规则：只有孵出的才算「遇见过」。抽牌时那 10 个候选背面朝上，
/// 牌没翻开就不算遇见——所以 undiscovered 永远比玩家想象的多。
/// 未遇见的显示「？？？」但保留稀有度与系别：稀有度是钩子（差的那格有多贵重），
/// 系别是线索（配合以后「半夜孵出影系」之类的条件孵化传闻）。
///
/// 纯代码创建，场景零改动（同 UIFactory/HatchPanel 约定）。
/// </summary>
public class CodexPanel : MonoBehaviour
{
    private Text titleText;          // 标题 + 进度
    private Text infoText;           // 统计行：累计孵化 / 保底进度
    private readonly List<Text> cellTexts = new List<Text>(); // 16 格，每次 Show 重填

    public static CodexPanel Create(Transform canvas)
    {
        var go = UIFactory.NewNode("CodexPanel", canvas);
        UIFactory.FillParent(go);
        var panel = go.AddComponent<CodexPanel>();
        panel.Build();
        panel.Hide();
        return panel;
    }

    private void Build()
    {
        // 全屏遮罩：点遮罩本身不关闭（误触会打断阅读），走右下关闭按钮
        UIFactory.CreateFillPanel(transform, "Dim", new Color(0f, 0f, 0f, 0.80f));

        var board = UIFactory.CreatePanel(transform, "Board",
            new Vector2(920f, 1500f), Vector2.zero,
            new Color(0.16f, 0.13f, 0.11f, 0.97f));
        var p = board.transform;

        titleText = UIFactory.CreateText(p, "Title", "图鉴",
            new Vector2(820f, 80f), new Vector2(0f, 660f), 56, new Color(1f, 0.92f, 0.76f));

        infoText = UIFactory.CreateText(p, "Info", "",
            new Vector2(820f, 50f), new Vector2(0f, 590f), 28, new Color(0.72f, 0.68f, 0.62f));

        // 4×4 网格：格子 205×250，间距 220×270，整网格高 1080（board 半高 750，标题占上部 200）
        var all = PetSpecies.All;
        for (int i = 0; i < all.Length && i < 16; i++)
        {
            int col = i % 4;
            int row = i / 4;
            float x = -330f + col * 220f;
            float y = 440f - row * 270f;

            var cell = UIFactory.CreatePanel(p, "Cell" + i,
                new Vector2(205f, 250f), new Vector2(x, y),
                new Color(0f, 0f, 0f, 0.35f));

            // 色带：格子上缘一条稀有度色带，一眼看出这格有多贵重（？？？也一样给色）
            UIFactory.CreatePanel(cell.transform, "RarityBand",
                new Vector2(205f, 14f), new Vector2(0f, 118f),
                PetSpecies.GetRarityColor(all[i].rarity));

            cellTexts.Add(UIFactory.CreateText(cell.transform, "CellText", "",
                new Vector2(190f, 200f), new Vector2(0f, -15f), 30, Color.white));
        }

        UIFactory.CreateButton(p, "BtnClose", "合上图鉴",
            new Vector2(500f, 120f), new Vector2(0f, -680f),
            new Color(0.45f, 0.36f, 0.28f, 1f), Hide, 40, new Color(1f, 0.98f, 0.94f));
    }

    // ================= 显示 / 隐藏 =================

    public void Show()
    {
        if (GameData.Instance != null && GameData.Instance.isDead) return; // 死亡期间不进图鉴
        Refresh();
        gameObject.SetActive(true);
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    /// <summary>每次打开都重填：图鉴内容随孵化/领养实时变化</summary>
    private void Refresh()
    {
        var gd = GameData.Instance;
        if (gd == null) return;

        var all = PetSpecies.All;
        int discovered = 0;

        for (int i = 0; i < all.Length && i < cellTexts.Count; i++)
        {
            var sp = all[i];
            bool seen = gd.HasDiscovered(sp.id);
            if (seen) discovered++;

            if (cellTexts[i] != null)
            {
                // 已遇见：名字 + 稀有度·系别（当前陪伴的加标记）
                // 未遇见：？？？ + 稀有度·系别 —— 名字和描述都藏着
                string name = seen ? sp.displayName : "？？？";
                if (seen && sp.id == gd.speciesId) name += " ·陪伴中";
                cellTexts[i].text = name + "\n" + PetSpecies.GetRarityName(sp.rarity) + " · " + sp.family + "系";
                cellTexts[i].color = seen ? Color.white : new Color(0.55f, 0.52f, 0.48f);
            }
        }

        if (titleText != null)
            titleText.text = "图鉴 · " + discovered + " / " + all.Length;

        if (infoText != null)
        {
            int pityLeft = Mathf.Max(0, PetSpecies.PityThreshold - gd.pityCount);
            infoText.text = "累计孵化 " + gd.hatchCount + " 次"
                + (pityLeft > 0 ? " · 再孵 " + pityLeft + " 次必有稀有以上" : " · 下一次必有稀有以上");
        }
    }
}
