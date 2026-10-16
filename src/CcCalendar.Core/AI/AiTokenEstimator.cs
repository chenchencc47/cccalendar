using System.Text.Json;
using System.Text.Json.Serialization;

namespace CcCalendar.Core.AI;

/// <summary>
/// 上下文 token 估算器（P74-02）：分段条数据源。启发式：CJK 字符 ×0.6 token、
/// 其余字符 ÷4 token（对齐 DeepSeek/通用分词器的量级），向上取整——只求量级
/// 正确，不求逐 token 精确。
/// </summary>
public static class AiTokenEstimator
{
    private static readonly JsonSerializerOptions SchemaOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static int EstimateTokens(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return 0;
        }

        int cjk = 0;
        int other = 0;
        foreach (char c in text)
        {
            if (c >= 0x4E00 && c <= 0x9FFF
                || c >= 0x3400 && c <= 0x4DBF
                || c >= 0xF900 && c <= 0xFAFF)
            {
                cjk++;
            }
            else
            {
                other++;
            }
        }

        return (int)Math.Ceiling(cjk * 0.6 + other / 4d);
    }

    /// <summary>
    /// 按请求上下文估算四段构成（系统指令/工具定义/用户消息/助手回复）。
    /// 用户段含历史用户轮与当前消息；助手段为历史回复轮（工具结果不进
    /// 会话历史，不单列）。
    /// </summary>
    public static AiContextCompositionUpdate EstimateComposition(AiRequestContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        int userTokens = EstimateTokens(context.UserMessage);
        int assistantTokens = 0;
        foreach (AiAssistantTurn turn in context.Conversation)
        {
            if (turn.IsUser)
            {
                userTokens += EstimateTokens(turn.Text);
            }
            else
            {
                assistantTokens += EstimateTokens(turn.Text);
            }
        }

        return new AiContextCompositionUpdate(
            EstimateTokens(context.SystemInstruction),
            EstimateToolsSchemaTokens(context.Tools),
            userTokens,
            assistantTokens);
    }

    private static int EstimateToolsSchemaTokens(IReadOnlyList<AiToolDefinition> tools)
    {
        if (tools.Count == 0)
        {
            return 0;
        }

        string schema = JsonSerializer.Serialize(tools, SchemaOptions);
        return EstimateTokens(schema);
    }
}
