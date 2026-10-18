using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 抽屉状态显示（阶段二任务5 + 阶段三任务11 + 任务17D补丁 + 任务17G + wb 进度条改造）：
/// 每帧把 GameData 的实时数值刷到抽屉里。
///
/// wb 起改版（老板拍板）：健康 / 心情 / 清洁 改为进度条，不再显示数字
/// —— 数值一直挂着会给人压力，条只表达"够不够"，不逼人盯着小数点。
/// 食物保留数字：它是唯一允许超量（>100）的指标，进度条封顶 100 表达不了 140 这种事。
///
/// 进度条用 RectTransform 宽度实现，不用 Image.filled：无 sprite 时 filled 类型不生效。
/// </summary>
public class StatusDisplay : MonoBehaviour
{
    [SerializeField] private Text textHunger;  // Text_Hunger
    [SerializeField] private Text textHealth;  // Text_Health
    [SerializeField] private Text textMood;    // Text_Mood
    [SerializeField] private Text textCoins;   // Text_Coins
    [SerializeField] private Text textDay;     // Text_Day（任务11：第 X 天）
    [SerializeField] private Text cleanText;   // Text_Clean（任务17D补丁：清洁度）

    // ---- 进度条参数（wb 分支）----
    private const float BarWidth = 420f;    // 条宽：Text 宽 500，两侧各留 40 边距
    private const float BarHeight = 10f;    // 条高
    private const float BarOffsetY = -46f;  // 相对 Text 中心下移的量（Text 高 70→底边 -35，再留 11px 间隙）

    private RectTransform fillHealth, fillMood, fillClean;
    private Image imgHealth, imgMood, imgClean;

    private static readonly Color BarBg = new Color(0f, 0f, 0f, 0.55f);                                  // 槽底深色
    private static readonly Color ColHealth = new Color(76f / 255f, 175f / 255f, 80f / 255f);            // 绿 #4CAF50
    private static readonly Color ColMood = new Color(1f, 193f / 255f, 7f / 255f);                       // 金 #FFC107
    private static readonly Color ColClean = new Color(0f, 188f / 255f, 212f / 255f);                    // 青 #00BCD4
    private static readonly Color ColLow = new Color(230f / 255f, 60f / 255f, 60f / 255f);               // 低于 30% 一律转红警示

    private void Start()
    {
        // 标签只留文字、去掉数字（老板：那三个数字改成进度条，不给用户数值压力）
        if (textHealth != null) textHealth.text = "健康";
        if (textMood != null) textMood.text = "心情";
        if (cleanText != null) cleanText.text = "清洁";

        var parent = textMood != null ? textMood.transform.parent : transform;
        fillHealth = CreateBar(parent, "Bar_Health", textHealth, ColHealth, out imgHealth);
        fillMood = CreateBar(parent, "Bar_Mood", textMood, ColMood, out imgMood);
        fillClean = CreateBar(parent, "Bar_Clean", cleanText, ColClean, out imgClean);
    }

    private void Update()
    {
        var gd = GameData.Instance;
        if (gd == null) return;

        // 仍用数字的两项
        if (textHunger != null) textHunger.text = "食物：" + Mathf.FloorToInt(gd.hunger);
        if (textCoins != null) textCoins.text = "金币：" + GameData.FormatCoins(gd.coins);
        if (textDay != null) textDay.text = "第 " + gd.GetCurrentDay() + " 天";

        // 改成进度条的三项
        SetBar(fillHealth, imgHealth, gd.health / 100f, ColHealth);
        SetBar(fillMood, imgMood, gd.mood / 100f, ColMood);
        SetBar(fillClean, imgClean, gd.clean / 100f, ColClean);
    }

    /// <summary>按比例设条宽；低于 30% 转红，让"快不行了"一眼可见</summary>
    private static void SetBar(RectTransform fill, Image img, float ratio, Color normal)
    {
        if (fill == null) return;
        ratio = Mathf.Clamp01(ratio);
        fill.sizeDelta = new Vector2(BarWidth * ratio, 0f);
        if (img != null) img.color = ratio < 0.3f ? ColLow : normal;
    }

    /// <summary>
    /// 在状态栏根节点下创建一条进度条（背景槽 + 前景填充），位置以对应 Text 为锚点向下偏移。
    /// 前景条用左对齐 + 改宽度表示进度，避免 Image.filled 在无 sprite 时失效。
    /// </summary>
    private RectTransform CreateBar(Transform parent, string name, Text anchorText, Color color, out Image fillImg)
    {
        fillImg = null;
        if (parent == null) return null;

        float y = 0f;
        if (anchorText != null)
        {
            var art = anchorText.GetComponent<RectTransform>();
            if (art != null) y = art.anchoredPosition.y + BarOffsetY;
        }

        // 背景槽
        var bg = new GameObject(name, typeof(RectTransform), typeof(Image));
        bg.transform.SetParent(parent, false);
        var bgRt = bg.GetComponent<RectTransform>();
        bgRt.anchorMin = new Vector2(0.5f, 0.5f);
        bgRt.anchorMax = new Vector2(0.5f, 0.5f);
        bgRt.pivot = new Vector2(0.5f, 0.5f);
        bgRt.sizeDelta = new Vector2(BarWidth, BarHeight);
        bgRt.anchoredPosition = new Vector2(0f, y);
        var bgImg = bg.GetComponent<Image>();
        bgImg.color = BarBg;
        bgImg.raycastTarget = false; // 别挡住抽屉里的按钮

        // 前景填充（左对齐，宽度随进度变）
        var fg = new GameObject(name + "_Fill", typeof(RectTransform), typeof(Image));
        fg.transform.SetParent(bg.transform, false);
        var fgRt = fg.GetComponent<RectTransform>();
        fgRt.anchorMin = new Vector2(0f, 0f);
        fgRt.anchorMax = new Vector2(0f, 1f);   // 高度跟随父级，宽度由 sizeDelta.x 决定
        fgRt.pivot = new Vector2(0f, 0.5f);
        fgRt.sizeDelta = new Vector2(BarWidth, 0f);
        fgRt.anchoredPosition = Vector2.zero;
        fillImg = fg.GetComponent<Image>();
        fillImg.color = color;
        fillImg.raycastTarget = false;

        return fgRt;
    }
}
