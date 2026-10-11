using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 抽屉状态显示（阶段二任务5）：每帧把 GameData 的实时数值刷到抽屉里的 4 个文本。
/// 仅做显示，不含任何交互逻辑；文本内容一律由本脚本动态更新，不写死。
/// </summary>
public class StatusDisplay : MonoBehaviour
{
    [SerializeField] private Text textHunger;  // Text_Hunger
    [SerializeField] private Text textHealth;  // Text_Health
    [SerializeField] private Text textMood;    // Text_Mood
    [SerializeField] private Text textCoins;   // Text_Coins

    private void Update()
    {
        if (GameData.Instance == null) return;
        if (textHunger != null) textHunger.text = "饥饿：" + Mathf.FloorToInt(GameData.Instance.hunger) + "/100";
        if (textHealth != null) textHealth.text = "健康：" + Mathf.FloorToInt(GameData.Instance.health) + "/100";
        if (textMood != null) textMood.text = "心情：" + Mathf.FloorToInt(GameData.Instance.mood) + "/100";
        if (textCoins != null) textCoins.text = "金币：" + GameData.Instance.coins;
    }
}
