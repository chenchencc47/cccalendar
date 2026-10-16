using System.IO;
using CcCalendar.Desktop.AI;

namespace CcCalendar.Desktop.Tests.AI;

/// <summary>
/// P74-04 视觉桥：附件读取器接受图片扩展名（不解码文本），图片单文件 4MB、
/// 全体附件总量 8MB（旧文本附件 1MB/2MB 上限保留给文本类）。
/// </summary>
public sealed class AiTextAttachmentReaderTests
{
    [Fact]
    public async Task ReadsImageAttachmentsWithoutDecodingText()
    {
        string imagePath = await CreateTemporaryFileAsync(".png", 1024);
        try
        {
            IReadOnlyList<AiTextAttachment> attachments = await AiTextAttachmentReader.ReadAsync(
                [imagePath],
                existingFileCount: 0,
                existingBytes: 0,
                CancellationToken.None);

            AiTextAttachment attachment = attachments.Single();
            Assert.True(attachment.IsImage);
            Assert.Equal(string.Empty, attachment.Content);
            Assert.Equal(Path.GetFileName(imagePath), attachment.FileName);
        }
        finally
        {
            File.Delete(imagePath);
        }
    }

    [Fact]
    public async Task TextAttachmentsRemainNonImageWithContent()
    {
        string textPath = await CreateTemporaryFileAsync(".txt", 64, "纯文本内容");
        try
        {
            IReadOnlyList<AiTextAttachment> attachments = await AiTextAttachmentReader.ReadAsync(
                [textPath],
                existingFileCount: 0,
                existingBytes: 0,
                CancellationToken.None);

            AiTextAttachment attachment = attachments.Single();
            Assert.False(attachment.IsImage);
            Assert.Equal("纯文本内容", attachment.Content);
        }
        finally
        {
            File.Delete(textPath);
        }
    }

    [Fact]
    public async Task ImageSingleFileLimitIsFourMegabytes()
    {
        string oversized = await CreateTemporaryFileAsync(".png", 4 * 1024 * 1024 + 1);
        string withinLimit = await CreateTemporaryFileAsync(".png", 4 * 1024 * 1024);
        try
        {
            await Assert.ThrowsAsync<InvalidDataException>(
                () => AiTextAttachmentReader.ReadAsync(
                    [oversized],
                    existingFileCount: 0,
                    existingBytes: 0,
                    CancellationToken.None));

            IReadOnlyList<AiTextAttachment> attachments = await AiTextAttachmentReader.ReadAsync(
                [withinLimit],
                existingFileCount: 0,
                existingBytes: 0,
                CancellationToken.None);
            Assert.True(attachments.Single().IsImage);
        }
        finally
        {
            File.Delete(oversized);
            File.Delete(withinLimit);
        }
    }

    [Fact]
    public async Task TotalBudgetAllowsUpToEightMegabytesWithImages()
    {
        string first = await CreateTemporaryFileAsync(".png", 3 * 1024 * 1024);
        string second = await CreateTemporaryFileAsync(".jpg", 3 * 1024 * 1024);
        string overflow = await CreateTemporaryFileAsync(".png", 3 * 1024 * 1024);
        try
        {
            // 两张 3MB 图片共 6MB，仍在 8MB 总量内。
            IReadOnlyList<AiTextAttachment> attachments = await AiTextAttachmentReader.ReadAsync(
                [first, second],
                existingFileCount: 0,
                existingBytes: 0,
                CancellationToken.None);
            Assert.Equal(2, attachments.Count);
            Assert.All(attachments, attachment => Assert.True(attachment.IsImage));

            // 再读一张 3MB 会超 8MB 总量。
            await Assert.ThrowsAsync<InvalidDataException>(
                () => AiTextAttachmentReader.ReadAsync(
                    [overflow],
                    existingFileCount: 2,
                    existingBytes: 6 * 1024 * 1024,
                    CancellationToken.None));
        }
        finally
        {
            File.Delete(first);
            File.Delete(second);
            File.Delete(overflow);
        }
    }

    [Fact]
    public async Task RejectsUnsupportedExtensions()
    {
        string executable = await CreateTemporaryFileAsync(".exe", 128);
        try
        {
            var exception = await Assert.ThrowsAsync<InvalidDataException>(
                () => AiTextAttachmentReader.ReadAsync(
                    [executable],
                    existingFileCount: 0,
                    existingBytes: 0,
                    CancellationToken.None));

            Assert.Contains(".exe", exception.Message, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(executable);
        }
    }

    private static async Task<string> CreateTemporaryFileAsync(
        string extension,
        int sizeBytes,
        string? text = null)
    {
        string path = Path.Combine(
            Path.GetTempPath(),
            $"cccalendar-attachment-{Guid.NewGuid():N}{extension}");
        if (text is null)
        {
            await File.WriteAllBytesAsync(path, new byte[sizeBytes], CancellationToken.None);
        }
        else
        {
            await File.WriteAllTextAsync(path, text, CancellationToken.None);
        }

        return path;
    }
}
