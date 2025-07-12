using EpubReader.Models;
using System.Text;
using System.Text.RegularExpressions;

namespace EpubReader.Service;

/// <summary>
/// Service for generating synthetic page numbers based on Adobe Digital Editions specification.
/// </summary>
public partial class SyntheticPageNumberService
{
    const int bytesPerPage = 1024;
    //const int ivSizeOverhead = 16; // Common IV size for AES encryption

    /// <summary>
    /// Generates synthetic page information for all resources in a book.
    /// </summary>
    /// <param name="book">The book containing resources to process.</param>
    /// <returns>A list of synthetic page information for each resource.</returns>
    public static List<SyntheticPageInfo> GenerateSyntheticPages(Book book)
    {
        var syntheticPages = new List<SyntheticPageInfo>();

        // Process each chapter/resource in the spine
        foreach (var chapter in book.Chapters)
        {
			var pageInfo = GeneratePageInfoForResource(chapter);
			syntheticPages.Add(pageInfo);
		}

        return syntheticPages;
    }

	/// <summary>
	/// Generates synthetic page information for a single resource.
	/// </summary>
	/// <param name="resource">The resource to process.</param>
	/// <returns>Synthetic page information for the resource.</returns>
	static SyntheticPageInfo GeneratePageInfoForResource(Chapter resource)
    {
        var pageInfo = new SyntheticPageInfo
        {
            ResourceFileName = resource.FileName
        };

        // Step 1: Determine compressed byte length (subtracting encryption overhead)
        int compressedByteLength;
        if (!string.IsNullOrEmpty(resource.HtmlFile))
        {
            compressedByteLength = Encoding.UTF8.GetByteCount(resource.HtmlFile);
        }
        else
        {
            compressedByteLength = resource.HtmlFile.Length;
        }

        pageInfo.CompressedByteLength = compressedByteLength;

        // Step 2: Calculate number of pages (1024 bytes per page, rounded up)
        pageInfo.PageCount = (int)Math.Ceiling((double)compressedByteLength / bytesPerPage);
        
        // Ensure at least 1 page
        if (pageInfo.PageCount == 0)
		{
			pageInfo.PageCount = 1;
		}

		// Step 3: Count Unicode characters and distribute page breaks
		if (!string.IsNullOrEmpty(resource.HtmlFile))
        {
            var textContent = ExtractTextFromHtml(resource.HtmlFile);
            pageInfo.TotalCharacters = textContent.Length;

            if (pageInfo.TotalCharacters > 0)
            {
                // Calculate characters per page (rounded up)
                pageInfo.CharactersPerPage = (int)Math.Ceiling((double)pageInfo.TotalCharacters / pageInfo.PageCount);
                
                // Generate page break positions
                pageInfo.PageBreakPositions = GeneratePageBreakPositions(pageInfo.TotalCharacters, pageInfo.PageCount);
            }
        }

        return pageInfo;
    }

	/// <summary>
	/// Extracts text content from HTML, removing tags and getting only readable text.
	/// </summary>
	/// <param name="htmlContent">The HTML content to process.</param>
	/// <returns>Plain text content with Unicode characters.</returns>
	static string ExtractTextFromHtml(string htmlContent)
    {
        if (string.IsNullOrEmpty(htmlContent))
		{
			return string.Empty;
		}

		// Remove HTML tags using regex
		var htmlTagPattern = @"<[^>]*>";
        var textContent = Regex.Replace(htmlContent, htmlTagPattern, string.Empty, RegexOptions.None, TimeSpan.FromSeconds(20));

        // Decode HTML entities
        textContent = System.Net.WebUtility.HtmlDecode(textContent);

        // Normalize whitespace while preserving paragraph breaks
        textContent = TextContent().Replace(textContent, " ");
        textContent = textContent.Trim();

        return textContent;
    }

	/// <summary>
	/// Generates page break positions evenly distributed across the text.
	/// </summary>
	/// <param name="totalCharacters">Total number of characters in the resource.</param>
	/// <param name="pageCount">Number of pages to distribute characters across.</param>
	/// <returns>List of character positions where page breaks should occur.</returns>
	static List<int> GeneratePageBreakPositions(int totalCharacters, int pageCount)
    {
        var pageBreakPositions = new List<int>();

        if (pageCount <= 1 || totalCharacters <= 0)
		{
			return pageBreakPositions;
		}

		// Calculate characters per page (rounded up)
		int charactersPerPage = (int)Math.Ceiling((double)totalCharacters / pageCount);

        // Generate page break positions
        for (int page = 1; page < pageCount; page++)
        {
            int breakPosition = page * charactersPerPage;
            
            // Ensure we don't exceed total characters
            if (breakPosition < totalCharacters)
            {
                pageBreakPositions.Add(breakPosition);
            }
        }

        return pageBreakPositions;
    }

    /// <summary>
    /// Gets the page number for a given character position within a resource.
    /// </summary>
    /// <param name="pageInfo">The synthetic page information for the resource.</param>
    /// <param name="characterPosition">The character position to find the page for.</param>
    /// <returns>The page number (1-based) for the given character position.</returns>
    public static int GetPageNumberForPosition(SyntheticPageInfo pageInfo, int characterPosition)
    {
        if (pageInfo.PageBreakPositions.Count == 0 || characterPosition <= 0)
		{
			return 1;
		}

		// Find which page this position falls into
		for (int i = 0; i < pageInfo.PageBreakPositions.Count; i++)
        {
            if (characterPosition < pageInfo.PageBreakPositions[i])
            {
                return i + 1;
            }
        }

        // Position is in the last page
        return pageInfo.PageCount;
    }

    /// <summary>
    /// Gets the total number of pages across all resources in the book.
    /// </summary>
    /// <param name="syntheticPages">List of synthetic page information for all resources.</param>
    /// <returns>Total number of pages in the book.</returns>
    public static int GetTotalPageCount(List<SyntheticPageInfo> syntheticPages)
    {
        return syntheticPages.Sum(p => p.PageCount);
    }

    /// <summary>
    /// Gets the global page number for a specific chapter and character position.
    /// </summary>
    /// <param name="syntheticPages">List of synthetic page information for all resources.</param>
    /// <param name="chapterIndex">Zero-based index of the chapter.</param>
    /// <param name="characterPosition">Character position within the chapter.</param>
    /// <returns>Global page number (1-based) across the entire book.</returns>
    public static int GetGlobalPageNumber(List<SyntheticPageInfo> syntheticPages, int chapterIndex, int characterPosition)
    {
        if (chapterIndex < 0 || chapterIndex >= syntheticPages.Count)
		{
			return 1;
		}

		// Calculate page offset from previous chapters
		int pageOffset = 0;
        for (int i = 0; i < chapterIndex; i++)
        {
            pageOffset += syntheticPages[i].PageCount;
        }

        // Get page number within current chapter
        int localPageNumber = GetPageNumberForPosition(syntheticPages[chapterIndex], characterPosition);

        return pageOffset + localPageNumber;
    }

	[GeneratedRegex(@"\s+", RegexOptions.None, 2000)]
	private static partial Regex TextContent();
}