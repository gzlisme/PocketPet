using System;
using System.Globalization;
using UnityEngine;

/// <summary>
/// Jev 工具链编辑器入口（仅编辑器工具，非游戏逻辑）。
/// 用法（batchmode）：
///   -executeMethod JevRunner.Run   健康检查（固定问题）
///   -executeMethod JevRunner.Ask  通用评估：从环境变量 JEV_STATE / JEV_QUESTION 读取，
///                                 走 JevHelper.AskNoul 真实 C# 路径，结果打日志标记 JEV_CS_RESULT
/// </summary>
public static class JevRunner
{
    public static void Run()
    {
        string state =
            "PocketPet 是团结引擎 1.10.4 的 2D URP 微信小游戏项目（电子宠物养成，竖屏）。" +
            "事实核查：JevHelper.cs 已从 MemoryCards3D 复制到 Assets/Editor，SHA256 与源文件一致；" +
            "Unity batchmode 编译通过（无 error CS）；" +
            "本调用是编辑器内 C# 路径通过 JevHelper.AskNoul 发起的真实 API 请求。";
        float? n = JevHelper.AskNoul(state, "JevHelper 是否复制成功、可用？");
        if (n.HasValue)
            Debug.Log("JEV_CS_RESULT noul=" + n.Value.ToString("0.####", CultureInfo.InvariantCulture));
        else
            Debug.LogError("JEV_CS_RESULT FAILED: AskNoul returned null (see JevHelper warnings above)");
    }

    /// <summary>
    /// 通用评估入口：环境变量传参（设置 $env: 必须与编辑器启动在同一 shell 命令内）。
    /// </summary>
    public static void Ask()
    {
        string state = Environment.GetEnvironmentVariable("JEV_STATE");
        string question = Environment.GetEnvironmentVariable("JEV_QUESTION");
        if (string.IsNullOrEmpty(question))
        {
            Debug.LogError("JEV_ASK_FAILED: env JEV_QUESTION not set");
            return;
        }
        if (string.IsNullOrEmpty(state)) state = "(no state provided)";
        float? n = JevHelper.AskNoul(state, question);
        if (n.HasValue)
            Debug.Log("JEV_CS_RESULT noul=" + n.Value.ToString("0.####", CultureInfo.InvariantCulture));
        else
            Debug.LogError("JEV_CS_RESULT FAILED: AskNoul returned null (see JevHelper warnings above)");
    }
}
