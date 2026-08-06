namespace HomeMind.Business.IServices.Expert;

/// <summary>PPTX 文件构建器，将标题与逐页要点生成标准 .pptx 字节流。</summary>
public interface IPptxBuilder
{
    /// <summary>生成 PPTX 字节流：首页为标题页，后续每页一张内容页。</summary>
    /// <param name="title">演示标题。</param>
    /// <param name="subtitle">副标题，可为空。</param>
    /// <param name="slides">内容页列表。</param>
    /// <returns>PPTX 文件字节流。</returns>
    byte[] Build(string title, string subtitle, IReadOnlyList<PptSlide> slides);
}

/// <summary>PPT 单页内容。</summary>
/// <param name="Title">页面标题。</param>
/// <param name="Bullets">页面要点，最多展示 6 条。</param>
public sealed record PptSlide(string Title, IReadOnlyList<string> Bullets);
