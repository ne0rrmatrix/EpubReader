namespace EpubReader.Extensions;

/// <summary>
/// Extension methods for the Book class to support synthetic page number generation.
/// </summary>
public static class BookExtensions
{
	/// <summary>
	/// Generates synthetic page information for the book.
	/// </summary>
	/// <param name="book">The book to generate page information for.</param>
	/// <returns>List of synthetic page information.</returns>
	public static List<SyntheticPageInfo> GenerateSyntheticPages(this Book book)
	{
		return SyntheticPageNumberService.GenerateSyntheticPages(book);
	}

	/// <summary>
	/// Gets the total number of synthetic pages in the book.
	/// </summary>
	/// <param name="book">The book to get page count for.</param>
	/// <returns>Total number of pages.</returns>
	public static int GetTotalPageCount(this Book book)
	{
		var syntheticPages = book.GenerateSyntheticPages();
		return SyntheticPageNumberService.GetTotalPageCount(syntheticPages);
	}

	/// <summary>
	/// Gets the current global page number based on the current chapter and position.
	/// </summary>
	/// <param name="book">The book to get page number for.</param>
	/// <param name="characterPosition">Character position within the current chapter (optional).</param>
	/// <returns>Current global page number.</returns>
	public static int GetCurrentPageNumber(this Book book, int characterPosition = 0)
	{
		var syntheticPages = book.GenerateSyntheticPages();
		return SyntheticPageNumberService.GetGlobalPageNumber(syntheticPages, book.CurrentChapter, characterPosition);
	}

	/// <summary>
	/// Gets page information for a specific chapter.
	/// </summary>
	/// <param name="book">The book to get page information for.</param>
	/// <param name="chapterIndex">Zero-based chapter index.</param>
	/// <returns>Synthetic page information for the chapter, or null if not found.</returns>
	public static SyntheticPageInfo? GetChapterPageInfo(this Book book, int chapterIndex)
	{
		var syntheticPages = book.GenerateSyntheticPages();
		return chapterIndex >= 0 && chapterIndex < syntheticPages.Count
			? syntheticPages[chapterIndex]
			: null;
	}
}