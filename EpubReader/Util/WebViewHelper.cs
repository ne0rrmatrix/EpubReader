using EpubReader.Extensions;
using EpubReader.Interfaces;
using EpubReader.Models;
using MetroLog;

namespace EpubReader.Util;

/// <summary>
/// A utility class that provides methods to interact with a <see cref="WebView"/> handler.
/// </summary>
/// <param name="handler"></param>
public partial class WebViewHelper(WebView handler)
{
	readonly IDb db = Application.Current?.Windows[0].Page?.Handler?.MauiContext?.Services.GetRequiredService<IDb>() ?? throw new InvalidOperationException();
	readonly WebView webView = handler;
	static readonly ILogger logger = LoggerFactory.GetLogger(nameof(WebViewHelper));

	/// <summary>
	/// Asynchronously sets the color scheme and font data for the web view based on user settings.
	/// </summary>
	/// <returns></returns>
	public async Task OnSettingsClickedAsync()
	{
		var settings = db.GetSettings() ?? new();
		await SetColorSchemeAsync(settings);
		await SetFontDataAsync(settings);
		var colCount = settings.SupportMultipleColumns ? "2" : "1";

		await webView.EvaluateJavaScriptAsync($"setReadiumProperty('--USER__colCount','{colCount}')");
		if (OperatingSystem.IsWindows())
		{
			var width = await GetWidthAsync(settings);
			await webView.EvaluateJavaScriptAsync($"setReadiumProperty('--USER__lineLength', '{width}px');");
		}

		await webView.EvaluateJavaScriptAsync("gotoEnd();");
		await webView.EvaluateJavaScriptAsync("getPageCount()");
	}

	/// <summary>
	/// Asynchronously loads a specific page of the book into the web view.
	/// </summary>
	/// <param name="label"></param>
	/// <param name="book"></param>
	/// <returns></returns>
	public async Task LoadPageAsync(Label label, Book book)
	{
#if ANDROID || WINDOWS
		var pageToLoad = $"https://demo/" + Path.GetFileName(book.Chapters[book.CurrentChapter].FileName);
#elif IOS || MACCATALYST
		var pageToLoad = $"app://demo/" + Path.GetFileName(book.Chapters[book.CurrentChapter].FileName);
#endif
		await webView.EvaluateJavaScriptAsync($"loadPage('{pageToLoad}');");
		UpdatePageLabel(label, book);
	}

	/// <summary>
	/// Asynchronously navigates to the next chapter of the book in the web view.
	/// </summary>
	/// <param name="label"></param>
	/// <param name="book"></param>
	/// <returns></returns>
	public async Task Next(Label label, Book book)
	{
		if (book.CurrentChapter < book.Chapters.Count - 1)
		{
			book.CurrentChapter++;
			db.UpdateBookMark(book);
			await LoadPageAsync(label, book);
		}
	}

	/// <summary>
	/// Asynchronously navigates to the previous chapter of the book in the web view.
	/// </summary>
	/// <param name="label"></param>
	/// <param name="book"></param>
	/// <returns></returns>
	public async Task Prev(Label label, Book book)
	{
		if (book.CurrentChapter > 0)
		{
			book.CurrentChapter--;
			db.UpdateBookMark(book);
			await webView.EvaluateJavaScriptAsync("setPreviousPage()");
			await LoadPageAsync(label, book);
		}
	}

	/// <summary>
	/// Updates the page label with synthetic page numbers.
	/// </summary>
	/// <param name="label">The label to update.</param>
	/// <param name="book">The book to get page information for.</param>
	public void UpdatePageLabel(Label label, Book book)
	{
		try
		{
			var currentPage = book.GetCurrentPageNumber();
			var totalPages = book.GetTotalPageCount();
			var chapterTitle = book.Chapters[book.CurrentChapter]?.Title ?? string.Empty;

			label.Text = $"{chapterTitle} (Page {currentPage} of {totalPages})";
		}
		catch (Exception ex)
		{
			logger.Error($"Error updating page label: {ex.Message}");
			// Fallback to chapter title only if synthetic page calculation fails
			label.Text = book.Chapters[book.CurrentChapter]?.Title ?? string.Empty;
		}
	}

	/// <summary>
	/// Gets synthetic page information for the current book state.
	/// </summary>
	/// <param name="book">The book to get page information for.</param>
	/// <param name="characterPosition">Optional character position within the current chapter.</param>
	/// <returns>A formatted string with synthetic page information.</returns>
	public string GetSyntheticPageInfo(Book book, int characterPosition = 0)
	{
		try
		{
			var currentPage = book.GetCurrentPageNumber(characterPosition);
			var totalPages = book.GetTotalPageCount();
			var chapterTitle = book.Chapters[book.CurrentChapter]?.Title ?? string.Empty;

			return $"{chapterTitle} (Page {currentPage} of {totalPages})";
		}
		catch (Exception ex)
		{
			logger.Error($"Error getting synthetic page info: {ex.Message}");
			// Fallback to chapter title only if synthetic page calculation fails
			return book.Chapters[book.CurrentChapter]?.Title ?? string.Empty;
		}
	}

	async Task SetColorSchemeAsync(Settings settings)
	{
		if (string.IsNullOrEmpty(settings.BackgroundColor) && string.IsNullOrEmpty(settings.TextColor))
		{
			await webView.EvaluateJavaScriptAsync("unsetReadiumProperty('--USER__backgroundColor')");
			await webView.EvaluateJavaScriptAsync($"setBackgroundColor('{string.Empty}')");
			await webView.EvaluateJavaScriptAsync("unsetReadiumProperty('--USER__textColor')");
		}
		else
		{
			await webView.EvaluateJavaScriptAsync($"setReadiumProperty('--USER__backgroundColor', '{settings.BackgroundColor}')");
			await webView.EvaluateJavaScriptAsync($"setBackgroundColor('{settings.BackgroundColor}')");
			await webView.EvaluateJavaScriptAsync($"setReadiumProperty('--USER__textColor', '{settings.TextColor}')");
		}
	}

	async Task SetFontDataAsync(Settings settings)
	{
		if (!string.IsNullOrEmpty(settings.FontFamily))
		{
			await webView.EvaluateJavaScriptAsync("setReadiumProperty('--USER__fontOverride', 'readium-font-on')");
			await webView.EvaluateJavaScriptAsync($"setReadiumProperty('--USER__fontFamily', '{settings.FontFamily}')");
		}
		else
		{
			await webView.EvaluateJavaScriptAsync("unsetReadiumProperty('--USER__fontOverride')");
			await webView.EvaluateJavaScriptAsync("unsetReadiumProperty('--USER__fontFamily')");
		}
		if (settings.FontSize > 0)
		{
			await webView.EvaluateJavaScriptAsync($"setReadiumProperty('--USER__fontSize','{settings.FontSize * 10}%')");
		}
		else
		{
			await webView.EvaluateJavaScriptAsync("unsetReadiumProperty('--USER__fontSize')");
		}
	}

	async Task<int> GetWidthAsync(Settings settings)
	{
		var result = await webView.EvaluateJavaScriptAsync("getWidth()");
		var fontSize = settings.FontSize > 0 ? settings.FontSize * 10 : 30;

		if (settings.SupportMultipleColumns)
		{
			return (Convert.ToInt32(result) / 3 - fontSize);
		}
		return (Convert.ToInt32(result) - fontSize);
	}
}
