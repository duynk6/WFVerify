using System.Text;
using ModelContextProtocol.Protocol;
using Xunit;

namespace WinFormsVerifier.UnitTests;

/// <summary>
/// Regression cho lỗi wf_screenshot trả "Invalid Base64 string".
/// ImageContentBlock.Data trong MCP SDK 2.2.0 là "base64-encoded UTF-8 bytes",
/// KHÔNG phải bytes ảnh gốc. Gán thẳng bytes PNG/JPEG vào Data khiến client
/// nhận được chuỗi không giải mã base64 được.
/// </summary>
public class ImageContentBlockTests
{
    // Header PNG hợp lệ tối thiểu, đủ để đại diện cho bytes ảnh nhị phân thô.
    private static readonly byte[] PngBytes = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0xFF, 0x7F, 0x80 };

    private static bool IsValidBase64(ReadOnlyMemory<byte> data)
    {
        var asText = Encoding.UTF8.GetString(data.Span);
        var buffer = new byte[asText.Length];
        return Convert.TryFromBase64String(asText, buffer, out _);
    }

    [Fact]
    public void FromBytes_ProducesValidBase64_ThatDecodesBackToOriginalImage()
    {
        var block = ImageContentBlock.FromBytes(PngBytes, "image/png");

        Assert.True(IsValidBase64(block.Data), "Data phải là chuỗi base64 hợp lệ để MCP client giải mã được.");
        Assert.Equal(PngBytes, block.DecodedData.ToArray());
        Assert.Equal("image/png", block.MimeType);
        Assert.Equal(Convert.ToBase64String(PngBytes), Encoding.UTF8.GetString(block.Data.Span));
    }

    [Fact]
    public void AssigningRawBytesToData_IsNotValidBase64_TheOriginalBug()
    {
        // Đây chính là cách VisualTools làm trước khi sửa.
        var buggy = new ImageContentBlock { Data = PngBytes, MimeType = "image/png" };

        Assert.False(IsValidBase64(buggy.Data), "Bytes ảnh thô không phải base64 — đây là nguyên nhân lỗi 'Invalid Base64 string'.");
    }

    [Fact]
    public void FromBytes_WorksForJpegToo()
    {
        var jpegBytes = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46 };
        var block = ImageContentBlock.FromBytes(jpegBytes, "image/jpeg");

        Assert.True(IsValidBase64(block.Data));
        Assert.Equal(jpegBytes, block.DecodedData.ToArray());
        Assert.Equal("image/jpeg", block.MimeType);
    }
}
