using System.Diagnostics;
using EpubReader.Extensions;

namespace EpubReader.Util;

/// <summary>
/// A utility class that provides methods to interact with a <see cref="WebView"/> handler.
/// </summary>
/// <param name="handler"></param>
/// <param name="db"></param>
/// <param name="syncService"></param>
public partial class WebViewHelper(WebView handler, IDb db, ISyncService syncService)
{
	readonly IDispatcher dispatcher = Microsoft.Maui.Controls.Application.Current?.Dispatcher ?? throw new InvalidOperationException();
	readonly IDb database = db;
	readonly ISyncService syncServiceInstance = syncService;
	readonly WebView webView = handler;
	static readonly ILogger logger = LoggerFactory.GetLogger(nameof(WebViewHelper));

	// Events to notify the UI layer about page load lifecycle
	public event Action? PageLoadStarted;
	public event Action? SettingsApplied;

	/// <summary>
	/// Asynchronously sets the color scheme and font data for the web view based on user settings.
	/// </summary>
	/// <returns></returns>
	public async Task OnSettingsClickedAsync()
	{
		var settings = await database.GetSettings() ?? new();
		await SetColorSchemeAsync(settings);
		await SetFontDataAsync(settings);
		var colCount = settings.SupportMultipleColumns ? "2" : "1";
		dispatcher.Dispatch(() => webView.EvaluateJavaScriptAsync($"setReadiumProperty('--USER__colCount','{colCount}')"));
		if (OperatingSystem.IsWindows())
		{
			var width = await GetWidthAsync(settings);
			dispatcher.Dispatch(() => webView.EvaluateJavaScriptAsync($"setReadiumProperty('--USER__lineLength', '{width}px');"));
		}

		dispatcher.Dispatch(() => webView.EvaluateJavaScriptAsync("gotoEnd();"));
		dispatcher.Dispatch(() => webView.EvaluateJavaScriptAsync("getPageCount()"));
		dispatcher.Dispatch(() =>
		{
			SettingsApplied?.Invoke();
		});
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
		dispatcher.Dispatch(() => PageLoadStarted?.Invoke());
		await dispatcher.DispatchAsync(() => webView.EvaluateJavaScriptAsync($"loadPage('{pageToLoad}');"));
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
			book.CurrentPage = 0;
			await SaveProgressAsync(book);
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
			book.CurrentPage = 0;
			await SaveProgressAsync(book);
			await dispatcher.DispatchAsync(() => webView.EvaluateJavaScriptAsync("setPreviousPage()"));
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

			dispatcher.Dispatch(() => label.Text = $"{chapterTitle} (Page {currentPage} of {totalPages})");
		}
		catch (Exception ex)
		{
			logger.Error($"Error updating page label: {ex.Message}");
			// Fallback to chapter title only if synthetic page calculation fails
			dispatcher.Dispatch(() => label.Text = book.Chapters[book.CurrentChapter]?.Title ?? string.Empty);
		}
	}

	/// <summary>
	/// Gets synthetic page information for the current book state.
	/// </summary>
	/// <param name="book">The book to get page information for.</param>
	/// <param name="characterPosition">Optional character position within the current chapter.</param>
	/// <returns>A formatted string with synthetic page information.</returns>
	public static string GetSyntheticPageInfo(Book book, int characterPosition = 0)
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

	/// <summary>
	/// Asynchronously sets the color scheme for the web view based on the specified settings.
	/// </summary>
	/// <remarks>This method updates the web view's color scheme by applying the specified background and text
	/// colors. If both colors in the <paramref name="settings"/> are null or empty, the method resets the color scheme to
	/// its default state.</remarks>
	/// <param name="settings">The settings containing the background and text colors to apply. If both colors are null or empty, the color scheme
	/// will be reset to default.</param>
	/// <returns>A task that represents the asynchronous operation.</returns>
	async Task SetColorSchemeAsync(Settings settings)
	{
		if (string.IsNullOrEmpty(settings.BackgroundColor) && string.IsNullOrEmpty(settings.TextColor))
		{
			await dispatcher.DispatchAsync(() => webView.EvaluateJavaScriptAsync("unsetReadiumProperty('--USER__backgroundColor')"));
			await dispatcher.DispatchAsync(() => webView.EvaluateJavaScriptAsync($"setBackgroundColor('{string.Empty}')"));
			await dispatcher.DispatchAsync(() => webView.EvaluateJavaScriptAsync("unsetReadiumProperty('--USER__textColor')"));
		}
		else
		{
			await dispatcher.DispatchAsync(() => webView.EvaluateJavaScriptAsync($"setReadiumProperty('--USER__backgroundColor', '{settings.BackgroundColor}')"));
			await dispatcher.DispatchAsync(() => webView.EvaluateJavaScriptAsync($"setBackgroundColor('{settings.BackgroundColor}')"));
			await dispatcher.DispatchAsync(() => webView.EvaluateJavaScriptAsync($"setReadiumProperty('--USER__textColor', '{settings.TextColor}')"));
		}
	}

	/// <summary>
	/// Asynchronously sets the font properties for the web view based on the specified settings.
	/// </summary>
	/// <remarks>This method updates the font settings in the web view by executing JavaScript commands. If the font
	/// family is specified, it enables the font override and sets the font family. If the font size is greater than zero,
	/// it sets the font size as a percentage. Otherwise, it unsets the respective properties.</remarks>
	/// <param name="settings">The settings containing font properties such as font family and font size. The <see cref="Settings.FontFamily"/>
	/// property cannot be null or empty to apply a font family, and <see cref="Settings.FontSize"/> must be greater than
	/// zero to apply a font size.</param>
	/// <returns>A task that represents the asynchronous operation.</returns>
	async Task SetFontDataAsync(Settings settings)
	{
		if (!string.IsNullOrEmpty(settings.FontFamily))
		{
			await dispatcher.DispatchAsync(() => webView.EvaluateJavaScriptAsync("setReadiumProperty('--USER__fontOverride', 'readium-font-on')"));
			await dispatcher.DispatchAsync(() => webView.EvaluateJavaScriptAsync($"setReadiumProperty('--USER__fontFamily', '{settings.FontFamily}')"));
		}
		else
		{
			await dispatcher.DispatchAsync(() => webView.EvaluateJavaScriptAsync("unsetReadiumProperty('--USER__fontOverride')"));
			await dispatcher.DispatchAsync(() => webView.EvaluateJavaScriptAsync("unsetReadiumProperty('--USER__fontFamily')"));
		}
		if (settings.FontSize > 0)
		{
			await dispatcher.DispatchAsync(() => webView.EvaluateJavaScriptAsync($"setReadiumProperty('--USER__fontSize','{settings.FontSize * 10}%')"));
		}
		else
		{
			await dispatcher.DispatchAsync(() => webView.EvaluateJavaScriptAsync("unsetReadiumProperty('--USER__fontSize')"));
		}
	}

	/// <summary>
	/// Asynchronously retrieves the width of the content and adjusts it based on the specified settings.
	/// </summary>
	/// <remarks>The method evaluates a JavaScript function to obtain the initial width and then adjusts it based on
	/// the font size and whether multiple columns are supported. If multiple columns are supported, the width is divided
	/// by three before adjusting for font size.</remarks>
	/// <param name="settings">The settings used to determine the font size and column support for the width calculation. Must not be null.</param>
	/// <returns>A task that represents the asynchronous operation. The task result contains the calculated width as an integer.</returns>
	async Task<int> GetWidthAsync(Settings settings)
	{
		var result = await dispatcher.DispatchAsync	(() => webView.EvaluateJavaScriptAsync("getWidth()"));
		var fontSize = settings.FontSize > 0 ? settings.FontSize * 10 : 30;

		if (settings.SupportMultipleColumns)
		{
			return (Convert.ToInt32(result) / 3 - fontSize);
		}
		return (Convert.ToInt32(result) - fontSize);
	}

	async Task SaveProgressAsync(Book book)
	{
		var syncId = await BookIdentityService.ComputeSyncIdAsync(book, CancellationToken.None);
		var progress = new ReadingProgress
		{
			BookId = syncId,
			CurrentChapter = book.CurrentChapter,
			CurrentPage = book.CurrentPage,
			LastUpdated = DateTimeOffset.UtcNow.ToString("o"),
			DeviceId = string.Empty,
			DeviceName = string.Empty,
			IsSynced = false
		};

		// Persist the local book position to the local DB so local and cloud positions can be distinguished.
		try
		{
			// Update only progress fields so we don't overwrite cover/image/title accidentally.
			await database.UpdateBookProgress(book.Id, book.CurrentChapter, book.CurrentPage, CancellationToken.None);
		}
		catch (Exception ex)
		{
			logger.Error($"Failed to persist local book position: {ex.Message}");
		}

		// SaveProgressAsync now handles local storage and debouncing internally via Rx
		await syncServiceInstance.SaveProgressAsync(progress, CancellationToken.None);
	}
}
