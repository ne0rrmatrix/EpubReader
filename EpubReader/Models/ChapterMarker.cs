namespace EpubReader.Models;

/// <summary>
/// Describes a chapter boundary inside a combined HTML document.
/// </summary>
/// <param name="Index">Zero-based chapter index in reading order.</param>
/// <param name="Title">Human-readable chapter title.</param>
/// <param name="FileName">Original EPUB filename (e.g. <c>chapter03.xhtml</c>).</param>
/// <param name="CharOffset">Character position of the opening <c>&lt;section&gt;</c> tag in the combined string.</param>
public record ChapterMarker(int Index, string Title, string FileName, long CharOffset);