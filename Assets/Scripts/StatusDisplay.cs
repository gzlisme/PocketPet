using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 抽屉状态显示（阶段二任务5 + 阶段三任务11）：
/// 每帧把 GameData 的实时数值刷到抽屉里的 4 个基础文本 + 3 个进化文本。
/// 仅做显示，不含任何交互逻辑；文本内容一律由本脚本动态更新，不写死。
/// </summary>
public class StatusDisplay : MonoBehaviour
{
    [SerializeField] private Text textHunger;  // Text_Hunger
    [SerializeField] private Text textHealth;  // Text_Health
    [SerializeField] private Text textMood;    // Text_Mood
    [SerializeField] private Text textCoins;   // Text_Coins
    [SerializeField] private Text textDay;    // Text_Day（任务11：第 X 天）
    [SerializeField] private Text textStage;  // Text_Stage（任务11：成长阶段）
    [SerializeField] private Text textCareScore; // Text_CareScore（任务11：照顾质量分）

    private void Update()
    {
        var gd = GameData.Instance;
        if (gd == null) return;
        if (textHunger != null) textHunger.text = "食物：" + Mathf.FloorToInt(gd.hunger);
        if (textHealth != null) textHealth.text = "健康：" + Mathf.FloorToInt(gd.health);
        if (textMood != null) textMood.text = "心情：" + Mathf.FloorToInt(gd.mood);
        if (textCoins != null) textCoins.text = "金币：" + gd.coins;
        if (textDay != null) textDay.text = "第 " + gd.GetCurrentDay() + " 天";
        if (textStage != null) textStage.text = "阶段：" + gd.GetCurrentStage();
        if (textCareScore != null) textCareScore.text = "照顾质量：" + gd.careScore;
    }
}
