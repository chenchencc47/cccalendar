using CcCalendar.Core.Configuration;

namespace CcCalendar.Core.AI;

/// <summary>
/// P74-04 视觉桥：把图片文件交给视觉模型转写为结构化文字。实现方负责
/// 读取文件并编码（如 data URI）；调用方负责选模型（AiProviderSettings
/// 来自用户在设置页指定的视觉转写模型）与错误呈现。
/// </summary>
public interface IVisionTranscriber
{
    /// <summary>转写单张图片，返回模型产出的文字描述。</summary>
    Task<string> TranscribeAsync(
        AiProviderSettings settings,
        string imageFilePath,
        CancellationToken cancellationToken);
}
