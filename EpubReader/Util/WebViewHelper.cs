using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
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
	const int defaultReaderFontSize = 16;
	const int minimumReaderFontSize = 8;
	const int maximumReaderFontSize = 36;
	const int minimumReaderFontPercent = 50;
	const int maximumReaderFontPercent = 225;
	const int androidReaderFontPercentPerStep = 10;
	const int androidMinimumReaderFontPercent = 80;
	const int androidMaximumReaderFontPercent = 360;
	const string defaultReaderLineSpacing = "1.5";
	const string readerTextAlignmentPublisherDefault = "";
	const string readerParagraphSpacingPublisherDefault = "";
	const string readerHyphenationPublisherDefault = "";
	const string readerLetterSpacingPublisherDefault = "";

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
	static readonly JsonSerializerOptions combinedPaginationSerializerOptions = new()
	{
		PropertyNameCaseInsensitive = true
	};

	// Events to notify the UI layer about page load lifecycle
	public event Action? PageLoadStarted;
	public event Action? SettingsApplied;

	// Tracks whether combined.html has been loaded into the iframe for the current book session.
	bool combinedHtmlLoaded = false;
	bool combinedHtmlLoadPending = false;

	/// <summary>
	/// Asynchronously sets the color scheme and font data for the web view based on user settings.
	/// </summary>
	/// <returns></returns>
	public async Task OnSettingsClickedAsync()
	{
		var settings = await database.GetSettings() ?? new();
		await SetColorSchemeAsync(settings);
		await SetFontDataAsync(settings);
		await SetLayoutDataAsync(settings);
		var colCount = settings.SupportMultipleColumns ? "2" : "1";
		dispatcher.Dispatch(() => webView.EvaluateJavaScriptAsync($"setReadiumProperty('--USER__colCount','{colCount}')"));
		if (OperatingSystem.IsWindows())
		{
			var width = await GetWidthAsync(settings);
			dispatcher.Dispatch(() => webView.EvaluateJavaScriptAsync($"setReadiumProperty('--USER__lineLength', '{width}px');"));
		}

		await dispatcher.DispatchAsync(() => webView.EvaluateJavaScriptAsync("refreshReaderLayout('settings');"));

		dispatcher.Dispatch(() =>
		{
			SettingsApplied?.Invoke();
		});
    }

	/// <summary>
	/// Asynchronously loads the combined HTML document into the iframe or reveals the active chapter section.
	/// </summary>
	/// <param name="label">Label to update with the current page information.</param>
	/// <param name="book">The book whose current chapter should be displayed.</param>
	/// <returns>
	/// <see langword="true"/> when the iframe <c>src</c> was changed and the caller must wait for the
	/// <c>pageload</c> JS event before applying settings or positioning the page.
	/// <see langword="false"/> when <c>showSection()</c> was used and the caller must apply settings
	/// and position the page immediately after this method returns.
	/// </returns>
	public async Task<bool> LoadPageAsync(Label label, Book book)
	{
		if (book is null || book.Chapters.Count == 0 || book.CurrentChapter < 0 || book.CurrentChapter >= book.Chapters.Count)
		{
			logger.Error($"Invalid book state: book is null, chapters empty, or CurrentChapter ({book?.CurrentChapter}) is out of bounds (count: {book?.Chapters.Count ?? 0})");
			return false;
		}

		EnsureCombinedHtml(book);

		if (!combinedHtmlLoaded)
		{
           if (combinedHtmlLoadPending)
			{
				UpdatePageLabel(label, book);
				return true;
			}

			// First load: put combined.html into the iframe. The pageload JS event will fire next.
			dispatcher.Dispatch(() => PageLoadStarted?.Invoke());
			var loadSucceeded = await EvaluateBooleanScriptAsync("loadCombinedPage();");
			if (!loadSucceeded)
			{
				const string message = "Failed to load combined.html into the iframe.";
				logger.Error(message);
				throw new InvalidOperationException(message);
			}

           combinedHtmlLoadPending = true;
			UpdatePageLabel(label, book);
			return true;
		}

		// Combined HTML already in iframe: reveal target section without a full reload.
		await dispatcher.DispatchAsync(() => webView.EvaluateJavaScriptAsync($"showSection({book.CurrentChapter});"));
		UpdatePageLabel(label, book);
		return false;
	}

	async Task<bool> EvaluateBooleanScriptAsync(string script)
	{
		var result = await dispatcher.DispatchAsync(() => webView.EvaluateJavaScriptAsync(script));
     var normalized = NormalizeJavaScriptResult(result);
		return bool.TryParse(normalized, out var parsed) && parsed;
	}

	static string NormalizeJavaScriptResult(string? result)
	{
		if (string.IsNullOrWhiteSpace(result))
		{
			return string.Empty;
		}

        var normalized = result.Trim();
		for (var attempt = 0; attempt < 3; attempt++)
		{
         if (normalized.Length >= 2 && normalized[0] == '"' && normalized[^1] == '"')
			{
             try
				{
					normalized = JsonSerializer.Deserialize<string>(normalized) ?? string.Empty;
					continue;
				}
				catch (JsonException)
				{
					normalized = normalized.Trim('"');
				}
			}

			if ((normalized.StartsWith('{') || normalized.StartsWith('[')) && normalized.Contains("\\\"", StringComparison.Ordinal))
			{
               normalized = Regex.Unescape(normalized);
				continue;
			}

			break;
		}

     return normalized;
	}

	static void EnsureCombinedHtml(Book book)
	{
		ArgumentNullException.ThrowIfNull(book);
		if (string.IsNullOrWhiteSpace(book.CombinedHtml))
		{
			throw new InvalidOperationException("CombinedHtml is required for reader navigation.");
		}
	}

	/// <summary>
	/// Resets the combined HTML load state so the next <see cref="LoadPageAsync"/> call reloads the iframe.
	/// Call this when the book session ends (page disappears) so a fresh load happens on re-entry.
	/// </summary>
	public void ResetCombinedState()
	{
		combinedHtmlLoaded = false;
     combinedHtmlLoadPending = false;
	}

	/// <summary>
	/// Marks the combined iframe document as ready after the initial <c>pageload</c> signal arrives from JavaScript.
	/// </summary>
	public void MarkCombinedHtmlLoaded()
	{
		combinedHtmlLoaded = true;
		combinedHtmlLoadPending = false;
	}

	/// <summary>
	/// Returns <see langword="true"/> when <c>combined.html</c> has fully loaded and
	/// its sections are available for pagination queries.
	/// </summary>
	public bool CombinedHtmlIsLoaded => combinedHtmlLoaded;

	/// <summary>
	/// Gets the current pagination state for the rendered <c>combined.html</c> document.
	/// </summary>
	/// <param name="token">The cancellation token.</param>
	/// <returns>The current combined pagination information, or <see langword="null"/> when it is unavailable.</returns>
	public async Task<CombinedPaginationInfo?> GetCombinedPaginationInfoAsync(CancellationToken token = default)
	{
		token.ThrowIfCancellationRequested();

		var result = await dispatcher.DispatchAsync(() => webView.EvaluateJavaScriptAsync("getCombinedPaginationInfo(true);"));
		var normalized = NormalizeJavaScriptResult(result);
		if (string.IsNullOrWhiteSpace(normalized) || string.Equals(normalized, "null", StringComparison.OrdinalIgnoreCase))
		{
			return null;
		}

		try
		{
			return JsonSerializer.Deserialize<CombinedPaginationInfo>(normalized, combinedPaginationSerializerOptions);
		}
		catch (JsonException ex)
		{
			logger.Error($"Failed to parse combined pagination info: {ex.Message}");
			return null;
		}
	}

	/// <summary>
	/// Asynchronously navigates to the next chapter of the book in the web view.
	/// </summary>
	/// <param name="label">Label to update with the current page information.</param>
	/// <param name="book">The book to navigate.</param>
	/// <returns>
	/// The same value as <see cref="LoadPageAsync"/>: <see langword="true"/> when the iframe reloaded,
	/// <see langword="false"/> when only a section swap occurred.
	/// </returns>
	public async Task<bool> Next(Label label, Book book)
	{
		if (book.CurrentChapter < book.Chapters.Count - 1)
		{
			book.CurrentChapter++;
			book.CurrentPage = 0;
			await SaveProgressAsync(book);
			return await LoadPageAsync(label, book);
		}
		return false;
	}

	/// <summary>
	/// Asynchronously navigates to the previous chapter of the book in the web view.
	/// </summary>
	/// <param name="label">Label to update with the current page information.</param>
	/// <param name="book">The book to navigate.</param>
	/// <returns>
	/// The same value as <see cref="LoadPageAsync"/>: <see langword="true"/> when the iframe reloaded,
	/// <see langword="false"/> when only a section swap occurred.
	/// </returns>
	public async Task<bool> Prev(Label label, Book book)
	{
		if (book.CurrentChapter > 0)
		{
			book.CurrentChapter--;
			book.CurrentPage = 0;
			await SaveProgressAsync(book);
			return await LoadPageAsync(label, book);
		}
		return false;
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
          dispatcher.Dispatch(() => label.Text = GetChapterTitle(book));
		}
		catch (Exception ex)
		{
			logger.Error($"Error updating page label: {ex.Message}");
           dispatcher.Dispatch(() => label.Text = GetChapterTitle(book));
		}
	}

	/// <summary>
 /// Formats the current page information for the active chapter.
	/// </summary>
    /// <param name="book">The active book.</param>
	/// <param name="currentPageNumber">The current global page number.</param>
	/// <param name="totalPages">The total rendered page count.</param>
	/// <returns>A formatted string with page information.</returns>
	public static string FormatPageLabel(Book book, int currentPageNumber, int totalPages)
	{
		try
		{
         var chapterTitle = GetChapterTitle(book);
			if (totalPages <= 0)
			{
				return chapterTitle;
			}

			var normalizedPage = Math.Clamp(currentPageNumber, 1, Math.Max(1, totalPages));
			return $"{chapterTitle} (Page {normalizedPage} of {totalPages})";
		}
		catch (Exception ex)
		{
           logger.Error($"Error formatting page info: {ex.Message}");
			return GetChapterTitle(book);
		}
	}

	static string GetChapterTitle(Book book)
	{
		ArgumentNullException.ThrowIfNull(book);
		if (book.CurrentChapter < 0 || book.CurrentChapter >= book.Chapters.Count)
		{
			return string.Empty;
		}

		return book.Chapters[book.CurrentChapter]?.Title ?? string.Empty;
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
			var fontFamilyValue = JsonSerializer.Serialize(ToReaderFontFamilyValue(settings.FontFamily));
			await dispatcher.DispatchAsync(() => webView.EvaluateJavaScriptAsync("setReadiumProperty('--USER__fontOverride', 'readium-font-on')"));
			await dispatcher.DispatchAsync(() => webView.EvaluateJavaScriptAsync($"setReadiumProperty('--USER__fontFamily', {fontFamilyValue})"));
		}
		else
		{
			await dispatcher.DispatchAsync(() => webView.EvaluateJavaScriptAsync("unsetReadiumProperty('--USER__fontOverride')"));
			await dispatcher.DispatchAsync(() => webView.EvaluateJavaScriptAsync("unsetReadiumProperty('--USER__fontFamily')"));
		}

		var normalizedFontSize = GetNormalizedReaderFontSize(settings.FontSize);
		var fontPercent = ToReaderFontPercent(normalizedFontSize, OperatingSystem.IsAndroid());
		await dispatcher.DispatchAsync(() => webView.EvaluateJavaScriptAsync($"setReadiumProperty('--USER__fontSize','{fontPercent}')"));
	}

	async Task SetLayoutDataAsync(Settings settings)
	{
		var normalizedLineSpacing = NormalizeReaderLineSpacing(settings.LineSpacing);
		await dispatcher.DispatchAsync(() => webView.EvaluateJavaScriptAsync($"setReadiumProperty('--USER__lineHeight','{normalizedLineSpacing}')"));

		var normalizedTextAlignment = NormalizeReaderTextAlignment(settings.TextAlignment);
		if (string.IsNullOrEmpty(normalizedTextAlignment))
		{
			await dispatcher.DispatchAsync(() => webView.EvaluateJavaScriptAsync("unsetReadiumProperty('--USER__textAlign')"));
		}
		else
		{
			await dispatcher.DispatchAsync(() => webView.EvaluateJavaScriptAsync($"setReadiumProperty('--USER__textAlign','{normalizedTextAlignment}')"));
		}

		var normalizedParagraphSpacing = NormalizeReaderParagraphSpacing(settings.ParagraphSpacing);
		if (string.IsNullOrEmpty(normalizedParagraphSpacing))
		{
			await dispatcher.DispatchAsync(() => webView.EvaluateJavaScriptAsync("unsetReadiumProperty('--USER__paraSpacing')"));
		}
		else
		{
			await dispatcher.DispatchAsync(() => webView.EvaluateJavaScriptAsync($"setReadiumProperty('--USER__paraSpacing','{normalizedParagraphSpacing}')"));
		}

		var normalizedHyphenation = NormalizeReaderBodyHyphens(settings.BodyHyphens);
		if (string.IsNullOrEmpty(normalizedHyphenation))
		{
			await dispatcher.DispatchAsync(() => webView.EvaluateJavaScriptAsync("unsetReadiumProperty('--USER__bodyHyphens')"));
		}
		else
		{
			await dispatcher.DispatchAsync(() => webView.EvaluateJavaScriptAsync($"setReadiumProperty('--USER__bodyHyphens','{normalizedHyphenation}')"));
		}

		var normalizedLetterSpacing = NormalizeReaderLetterSpacing(settings.LetterSpacing);
		if (string.IsNullOrEmpty(normalizedLetterSpacing))
		{
			await dispatcher.DispatchAsync(() => webView.EvaluateJavaScriptAsync("unsetReadiumProperty('--USER__letterSpacing')"));
			return;
		}

		await dispatcher.DispatchAsync(() => webView.EvaluateJavaScriptAsync($"setReadiumProperty('--USER__letterSpacing','{normalizedLetterSpacing}')"));
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
		var fontSize = GetNormalizedReaderFontSize(settings.FontSize);

		if (settings.SupportMultipleColumns)
		{
			return (Convert.ToInt32(result) / 3 - fontSize);
		}
		return (Convert.ToInt32(result) - fontSize);
	}

	static int GetNormalizedReaderFontSize(int fontSize)
	{
		if (fontSize <= 0)
		{
			return defaultReaderFontSize;
		}

		return Math.Clamp(fontSize, minimumReaderFontSize, maximumReaderFontSize);
	}

	static string ToReaderFontFamilyValue(string fontFamily)
	{
		var sanitized = fontFamily.Trim();
		if (string.IsNullOrEmpty(sanitized))
		{
			return string.Empty;
		}

		var escapedFamily = sanitized.Replace("\\", "\\\\", StringComparison.Ordinal)
			.Replace("\"", "\\\"", StringComparison.Ordinal);

		return sanitized switch
		{
			"Arial" or "Verdana" or "Tahoma" or "Trebuchet MS" or "Comic Sans MS" or "Helvetica" => $"\"{escapedFamily}\", sans-serif",
			"Courier New" => $"\"{escapedFamily}\", monospace",
			_ => $"\"{escapedFamily}\", serif"
		};
	}

	static string NormalizeReaderLineSpacing(string? lineSpacing)
	{
		if (string.IsNullOrWhiteSpace(lineSpacing))
		{
			return defaultReaderLineSpacing;
		}

		if (!double.TryParse(lineSpacing, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedValue))
		{
			return defaultReaderLineSpacing;
		}

		var allowedValues = new[] { 1.25d, 1.5d, 1.75d, 2d };
		var nearest = allowedValues.OrderBy(value => Math.Abs(value - parsedValue)).First();
		return nearest.ToString("0.##", CultureInfo.InvariantCulture);
	}

	static string NormalizeReaderTextAlignment(string? textAlignment)
	{
		if (string.IsNullOrWhiteSpace(textAlignment))
		{
			return readerTextAlignmentPublisherDefault;
		}

		return textAlignment.Trim().ToLowerInvariant() switch
		{
			"left" => "left",
			"justify" => "justify",
			_ => readerTextAlignmentPublisherDefault
		};
	}

	static string NormalizeReaderParagraphSpacing(string? paragraphSpacing)
	{
		if (string.IsNullOrWhiteSpace(paragraphSpacing))
		{
			return readerParagraphSpacingPublisherDefault;
		}

		return paragraphSpacing.Trim().ToLowerInvariant() switch
		{
			"0" or "0rem" or "0.0" or "0.0rem" => "0",
			"0.5rem" => "0.5rem",
			"1rem" => "1rem",
			"1.5rem" => "1.5rem",
			_ => readerParagraphSpacingPublisherDefault
		};
	}

	static string NormalizeReaderBodyHyphens(string? bodyHyphens)
	{
		if (string.IsNullOrWhiteSpace(bodyHyphens))
		{
			return readerHyphenationPublisherDefault;
		}

		return bodyHyphens.Trim().ToLowerInvariant() switch
		{
			"auto" => "auto",
			"manual" => "manual",
			"none" => "none",
			_ => readerHyphenationPublisherDefault
		};
	}

	static string NormalizeReaderLetterSpacing(string? letterSpacing)
	{
		if (string.IsNullOrWhiteSpace(letterSpacing))
		{
			return readerLetterSpacingPublisherDefault;
		}

		return letterSpacing.Trim().ToLowerInvariant() switch
		{
			"0" or "0em" or "0.0em" => "0",
			"0.02em" => "0.02em",
			"0.04em" => "0.04em",
			"0.06em" => "0.06em",
			_ => readerLetterSpacingPublisherDefault
		};
	}

	static string ToReaderFontPercent(int fontSize, bool isAndroid)
	{
		var normalizedFontSize = GetNormalizedReaderFontSize(fontSize);
		var scalePercent = isAndroid
			? normalizedFontSize * androidReaderFontPercentPerStep
			: (int)Math.Round(normalizedFontSize / (double)defaultReaderFontSize * 100d, MidpointRounding.AwayFromZero);

		var clampedScale = isAndroid
			? Math.Clamp(scalePercent, androidMinimumReaderFontPercent, androidMaximumReaderFontPercent)
			: Math.Clamp(scalePercent, minimumReaderFontPercent, maximumReaderFontPercent);
		return $"{clampedScale}%";
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

	public sealed record CombinedPaginationInfo
	{
		public int CurrentSectionIndex { get; init; }
		public int CurrentPage { get; init; }
		public int CurrentGlobalPage { get; init; }
		public int TotalPages { get; init; }
		public List<int> ChapterPageCounts { get; init; } = [];
		public List<int> ChapterOffsets { get; init; } = [];
	}
}