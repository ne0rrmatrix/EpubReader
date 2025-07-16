namespace EpubReader.Models;

/// <summary>
/// Represents synthetic page information for a resource in an EPUB document.
/// </summary>
public class SyntheticPageInfo
{
    /// <summary>
    /// Gets or sets the resource file name.
    /// </summary>
    public string ResourceFileName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the total number of pages in this resource.
    /// </summary>
    public int PageCount { get; set; }

    /// <summary>
    /// Gets or sets the number of Unicode characters per page.
    /// </summary>
    public int CharactersPerPage { get; set; }

    /// <summary>
    /// Gets or sets the total number of Unicode characters in the resource.
    /// </summary>
    public int TotalCharacters { get; set; }

    /// <summary>
    /// Gets or sets the compressed byte length of the resource.
    /// </summary>
    public int CompressedByteLength { get; set; }

    /// <summary>
    /// Gets or sets the character positions where page breaks occur.
    /// </summary>
    public List<int> PageBreakPositions { get; set; } = [];
}