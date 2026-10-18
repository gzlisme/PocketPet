using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 纯代码 UGUI 构造工具（2026-10-19 孵化系统专用）。
///
/// 为什么要有这个类：
/// 项目所有面板都是场景里预先摆好的对象 + [SerializeField] 引用。但孵化系统要加大量新 UI，
/// 而改场景文件（.scene YAML）风险极高 —— 坑 #2：改层级后所有 Transform.Find / GetChild(n) 都可能失效。
/// 所以孵化系统的全部 UI 一律在运行时用代码创建，场景结构零改动。
///
/// 参照物：UIManager.SpawnFloatingText 已验证过「new GameObject + AddComponent<Text>」这条路可行。
/// 本类把同样的做法封装成可复用的建筑块。
///
/// 字体策略：绝不新建字体资源，一律借用场景里现有 Text 的 font（同一个 Unity 内置 LegacyRuntime），
/// 保证与现有 UI 的中文表现完全一致 —— 现有 UI 能显示中文，新建的就能。
/// </summary>
public static class UIFactory
{
    /// <summary>全局字体。由 UIManager 在 Start 时从现有 Text 借来注入；为 null 时回退到内置资源。</summary>
    public static Font Font;

    /// <summary>取可用字体：优先注入的，其次内置资源，最后 OS 字体。全失败返回 null（调用方须判空）。</summary>
    public static Font GetFont()
    {
        if (Font != null) return Font;
        // Unity 2022+ 内置字体资源名是 LegacyRuntime.ttf；更早版本是 Arial.ttf。两个都试。
        Font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (Font == null) Font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        return Font;
    }

    /// <summary>创建一个带 RectTransform 的空 GameObject（UGUI 节点的统一起点）。</summary>
    public static GameObject NewNode(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    /// <summary>把 RectTransform 设成居中对齐 + 指定尺寸（本项目 UI 统一用 1080×1920 参考分辨率）。</summary>
    public static void SetRect(GameObject go, Vector2 size, Vector2 pos)
    {
        var rt = go.GetComponent<RectTransform>();
        if (rt == null) rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = pos;
    }

    /// <summary>把 RectTransform 设成铺满父容器（用来装内容区、遮罩层等）。</summary>
    public static void FillParent(GameObject go, float padding = 0f)
    {
        var rt = go.GetComponent<RectTransform>();
        if (rt == null) rt = go.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(padding, padding);
        rt.offsetMax = new Vector2(-padding, -padding);
    }

    /// <summary>纯色矩形面板（同时充当按钮/输入框的底板）。</summary>
    public static GameObject CreatePanel(Transform parent, string name, Vector2 size, Vector2 pos, Color color)
    {
        var go = NewNode(name, parent);
        var img = go.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = true; // 面板要挡住下层点击
        SetRect(go, size, pos);
        return go;
    }

    /// <summary>铺满父容器的纯色面板（做遮罩 / 内容底板）。</summary>
    public static GameObject CreateFillPanel(Transform parent, string name, Color color, float padding = 0f)
    {
        var go = NewNode(name, parent);
        var img = go.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = true;
        FillParent(go, padding);
        return go;
    }

    /// <summary>文本。alignment 默认居中；wrap 默认开启（长句自动折行）。</summary>
    public static Text CreateText(Transform parent, string name, string content, Vector2 size, Vector2 pos,
                                  int fontSize = 40, Color? color = null, TextAnchor anchor = TextAnchor.MiddleCenter)
    {
        var go = NewNode(name, parent);
        var text = go.AddComponent<Text>();
        text.font = GetFont();
        text.fontSize = fontSize;
        text.alignment = anchor;
        text.color = color ?? Color.white;
        text.text = content;
        text.raycastTarget = false;   // 文本一律不拦射线（教训来自 SpawnFloatingText）
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        SetRect(go, size, pos);
        return text;
    }

    /// <summary>铺满父容器的文本（做滚动区 / 列表内容）。</summary>
    public static Text CreateFillText(Transform parent, string name, string content, int fontSize = 36,
                                      Color? color = null, TextAnchor anchor = TextAnchor.UpperLeft, float padding = 0f)
    {
        var go = NewNode(name, parent);
        var text = go.AddComponent<Text>();
        text.font = GetFont();
        text.fontSize = fontSize;
        text.alignment = anchor;
        text.color = color ?? Color.white;
        text.text = content;
        text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        FillParent(go, padding);
        return text;
    }

    /// <summary>
    /// 按钮：底板 Image + 居中文本。返回 Button，文本用 GetComponentInChildren&lt;Text&gt; 取。
    /// 注意 onClick 用代码绑定（AddListener），不走场景 Inspector 面板事件。
    /// </summary>
    public static Button CreateButton(Transform parent, string name, string label, Vector2 size, Vector2 pos,
                                      Color bgColor, System.Action onClick, int fontSize = 40,
                                      Color? textColor = null)
    {
        var go = NewNode(name, parent);
        var img = go.AddComponent<Image>();
        img.color = bgColor;
        img.raycastTarget = true;

        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        if (onClick != null) btn.onClick.AddListener(() => onClick());

        SetRect(go, size, pos);

        var textGo = NewNode("Text", go.transform);
        var text = textGo.AddComponent<Text>();
        text.font = GetFont();
        text.fontSize = fontSize;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = textColor ?? Color.white;
        text.text = label;
        text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        FillParent(textGo);

        return btn;
    }

    /// <summary>
    /// 多行输入框：底板 + 真正的输入 Text + 占位提示 Text。
    /// 关键配置顺序：先把 textComponent / placeholder 全部设好，再设置 lineType 等参数，
    /// 否则 InputField 在 OnEnable 时拿不到 textComponent 会导致输入内容不显示。
    /// </summary>
    public static InputField CreateInputField(Transform parent, string name, string placeholder,
                                              Vector2 size, Vector2 pos, int fontSize = 36,
                                              int characterLimit = 100)
    {
        var go = NewNode(name, parent);
        var bg = go.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.45f);
        bg.raycastTarget = true;
        SetRect(go, size, pos);

        // ---- 真正的输入文本 ----
        var textGo = NewNode("Text", go.transform);
        var text = textGo.AddComponent<Text>();
        text.font = GetFont();
        text.fontSize = fontSize;
        text.alignment = TextAnchor.UpperLeft;
        text.color = Color.white;
        text.supportRichText = false;   // 关富文本：玩家输入 < > 不会被当标签吞掉
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        FillParent(textGo, 20f);

        // ---- 占位提示 ----
        var phGo = NewNode("Placeholder", go.transform);
        var ph = phGo.AddComponent<Text>();
        ph.font = GetFont();
        ph.fontSize = fontSize;
        ph.alignment = TextAnchor.UpperLeft;
        ph.color = new Color(1f, 1f, 1f, 0.35f);
        ph.text = placeholder;
        ph.raycastTarget = false;
        ph.horizontalOverflow = HorizontalWrapMode.Wrap;
        ph.verticalOverflow = VerticalWrapMode.Truncate;
        FillParent(phGo, 20f);

        // ---- 组装 InputField ----
        var input = go.AddComponent<InputField>();
        input.targetGraphic = bg;
        input.textComponent = text;
        input.placeholder = ph;
        input.lineType = InputField.LineType.MultiLineNewline; // 多行：愿望 50~100 字需要
        input.characterLimit = characterLimit;
        input.caretColor = Color.white;
        input.selectionColor = new Color(0.4f, 0.7f, 1f, 0.5f);
        input.customCaretColor = true;

        return input;
    }

    /// <summary>全屏半透明遮罩（挡住所面板点击，点击自身可用于关闭）。返回遮罩 GameObject。</summary>
    public static GameObject CreateBlocker(Transform parent, string name, Color color, System.Action onClick = null)
    {
        var go = CreateFillPanel(parent, name, color);
        if (onClick != null)
        {
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = go.GetComponent<Image>();
            btn.transition = Selectable.Transition.None;
            btn.onClick.AddListener(() => onClick());
        }
        return go;
    }
}
