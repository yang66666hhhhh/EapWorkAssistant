using System.Net.Http;
using System.Text;
using System.Text.Json;
using EapWorkAssistant.Helpers;

namespace EapWorkAssistant.Services;

/// <summary>
/// OpenAI 兼容 API 客户端，支持任意兼容端点（DeepSeek、Ollama、LM Studio 等）
/// 使用非流式调用，返回完整的 assistant 回复文本
/// </summary>
public class AiService
{
    public static AiService Instance { get; } = new();

    private static readonly HttpClient _http = new()
    {
        Timeout = TimeSpan.FromSeconds(120)
    };

    /// <summary>
    /// 发送聊天完成请求到 OpenAI 兼容 API
    /// </summary>
    /// <param name="systemPrompt">系统提示词（角色定义 + 格式要求）</param>
    /// <param name="userMessage">用户消息（实际工作数据）</param>
    /// <returns>AI 回复的文本内容</returns>
    public async Task<string> SendChatAsync(string systemPrompt, string userMessage)
    {
        var settings = AiSettings.Load();
        if (!settings.IsConfigured)
            throw new InvalidOperationException("AI 服务未配置，请先在设置中填写 API 地址和密钥。");

        _http.Timeout = TimeSpan.FromSeconds(settings.TimeoutSeconds);

        var requestBody = new
        {
            model = settings.Model,
            messages = new[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userMessage }
            },
            max_tokens = settings.MaxTokens,
            temperature = 0.7
        };

        var json = JsonSerializer.Serialize(requestBody);
        var endpoint = settings.Endpoint.TrimEnd('/');
        var request = new HttpRequestMessage(HttpMethod.Post, $"{endpoint}/chat/completions")
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        request.Headers.Add("Authorization", $"Bearer {settings.ApiKey}");

        HttpResponseMessage response;
        try
        {
            response = await _http.SendAsync(request);
        }
        catch (TaskCanceledException)
        {
            throw new TaskCanceledException("AI 服务请求超时，请稍后重试或在设置中增加超时时间。");
        }
        catch (HttpRequestException ex)
        {
            throw new HttpRequestException($"网络连接失败：{ex.Message}");
        }

        var responseBody = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            var errorMsg = ExtractErrorMessage(responseBody);
            throw new HttpRequestException(
                $"API 返回错误 ({(int)response.StatusCode})：{errorMsg}");
        }

        var content = ExtractContentFromResponse(responseBody);
        if (string.IsNullOrWhiteSpace(content))
            throw new InvalidOperationException("AI 返回内容为空，请检查模型配置。");

        return content;
    }

    /// <summary>
    /// 从 API 响应 JSON 中提取 assistant 回复内容
    /// </summary>
    private static string ExtractContentFromResponse(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.TryGetProperty("choices", out var choices) &&
                choices.GetArrayLength() > 0)
            {
                var firstChoice = choices[0];
                if (firstChoice.TryGetProperty("message", out var message) &&
                    message.TryGetProperty("content", out var content))
                {
                    return content.GetString() ?? string.Empty;
                }
            }
        }
        catch (JsonException ex)
        {
            Logger.Error("解析 AI 响应 JSON 失败", ex);
        }
        return string.Empty;
    }

    /// <summary>
    /// 从 API 错误响应 JSON 中提取错误消息
    /// </summary>
    private static string ExtractErrorMessage(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.TryGetProperty("error", out var error))
            {
                if (error.TryGetProperty("message", out var msg))
                    return msg.GetString() ?? "未知错误";
                if (error.ValueKind == JsonValueKind.String)
                    return error.GetString() ?? "未知错误";
            }
        }
        catch { /* 解析失败，返回原始文本 */ }

        // 截断长文本
        return json.Length > 200 ? json[..200] + "..." : json;
    }
}
