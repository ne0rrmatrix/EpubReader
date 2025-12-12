using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
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
	static readonly JsonSerializerOptions mediaOverlayThemeSerializerOptions = new()
	{
		Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
	};

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

		await ApplyMediaOverlayThemeAsync(settings).ConfigureAwait(false);
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
		var result = await dispatcher.DispatchAsync(() => webView.EvaluateJavaScriptAsync("getWidth()"));
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

	async Task ApplyMediaOverlayThemeAsync(Settings settings)
	{
		var payload = TryBuildMediaOverlayThemePayload(settings, out var theme)
			? JsonSerializer.Serialize(theme, mediaOverlayThemeSerializerOptions)
			: "null";

		await dispatcher.DispatchAsync(() => webView.EvaluateJavaScriptAsync($"setMediaOverlayTheme({payload});")).ConfigureAwait(false);
	}

	static bool TryBuildMediaOverlayThemePayload(Settings settings, out MediaOverlayThemePayload payload)
	{
		payload = null!;
		if (!TryParseColor(settings.BackgroundColor, out var background) || !TryParseColor(settings.TextColor, out var text))
		{
			return false;
		}

		var isDarkBackground = GetRelativeLuminance(background) < 0.5;
		var highlightBase = Mix(background, text, isDarkBackground ? 0.62 : 0.38);
		var outlineBase = Mix(text, background, isDarkBackground ? 0.25 : 0.75);
		var highlightBackground = ToRgba(highlightBase, isDarkBackground ? 0.58 : 0.42);
		var highlightOutline = ToRgba(outlineBase, 0.8);
		var highlightText = GetHigherContrastColor(highlightBase, text, background);

		payload = new MediaOverlayThemePayload
		{
			HighlightBackground = highlightBackground,
			HighlightText = ToHex(highlightText),
			HighlightOutline = highlightOutline
		};
		return true;
	}

	static bool TryParseColor(string? value, out Color color)
	{
		color = Colors.Transparent;
		if (string.IsNullOrWhiteSpace(value))
		{
			return false;
		}

		try
		{
			color = Color.FromArgb(value);
			return true;
		}
		catch (Exception)
		{
			return false;
		}
	}

	static Color Mix(Color start, Color end, double amount)
	{
		var t = Math.Clamp(amount, 0d, 1d);
		return new Color(
			(float)(start.Red + (end.Red - start.Red) * t),
			(float)(start.Green + (end.Green - start.Green) * t),
			(float)(start.Blue + (end.Blue - start.Blue) * t),
			1f);
	}

	static string ToHex(Color color)
	{
		return $"#{ToByte(color.Red):X2}{ToByte(color.Green):X2}{ToByte(color.Blue):X2}";
	}

	static string ToRgba(Color color, double alpha)
	{
		var clampedAlpha = Math.Clamp(alpha, 0d, 1d);
		return $"rgba({ToByte(color.Red)},{ToByte(color.Green)},{ToByte(color.Blue)},{clampedAlpha.ToString(CultureInfo.InvariantCulture)})";
	}

	static byte ToByte(double channel)
	{
		return (byte)Math.Clamp(Math.Round(channel * 255d), 0d, 255d);
	}

	static Color GetHigherContrastColor(Color reference, Color optionA, Color optionB)
	{
		var contrastA = CalculateContrastRatio(reference, optionA);
		var contrastB = CalculateContrastRatio(reference, optionB);
		return contrastA >= contrastB ? optionA : optionB;
	}

	static double CalculateContrastRatio(Color first, Color second)
	{
		var luminanceFirst = GetRelativeLuminance(first);
		var luminanceSecond = GetRelativeLuminance(second);
		var (brighter, darker) = luminanceFirst >= luminanceSecond
			? (luminanceFirst, luminanceSecond)
			: (luminanceSecond, luminanceFirst);
		return (brighter + 0.05) / (darker + 0.05);
	}

	static double GetRelativeLuminance(Color color)
	{
		static double NormalizeChannel(double channel)
		{
			return channel <= 0.03928
				? channel / 12.92
				: Math.Pow((channel + 0.055) / 1.055, 2.4);
		}

		var r = NormalizeChannel(color.Red);
		var g = NormalizeChannel(color.Green);
		var b = NormalizeChannel(color.Blue);
		return 0.2126 * r + 0.7152 * g + 0.0722 * b;
	}

	sealed record MediaOverlayThemePayload
	{
		public string? HighlightBackground { get; init; }
		public string? HighlightText { get; init; }
		public string? HighlightOutline { get; init; }
	}
}