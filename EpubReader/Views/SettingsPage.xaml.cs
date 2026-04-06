using System.Diagnostics;
using System.Globalization;
using System.Text.Json;

namespace EpubReader.Views;

/// <summary>
/// Represents a settings page within the application, allowing users to view and modify application settings.
/// </summary>
/// <remarks>The <see cref="SettingsPage"/> class provides a user interface for managing application settings such
/// as font size, theme, and layout preferences. It interacts with a database to retrieve and persist settings, and
/// updates the UI components to reflect the current settings. The page also sends notifications to other components
/// when settings are changed.</remarks>
public partial class SettingsPage : Popup<bool>
{
	const int defaultReaderFontSize = 16;
	const int minimumReaderFontSize = 8;
	const int maximumReaderFontSize = 36;
	const string defaultReaderLineSpacing = "1.5";
	const string defaultReaderTextAlignment = "";
	const string defaultReaderParagraphSpacing = "";
	const string defaultReaderBodyHyphens = "";
	const string defaultReaderLetterSpacing = "";
	const string publisherDefaultOptionLabel = "Publisher Default";
	static readonly IReadOnlyList<(string Label, string Value)> lineSpacingOptions =
	[
		("Tight", "1.25"),
		("Normal", "1.5"),
		("Relaxed", "1.75"),
		("Spacious", "2")
	];
	static readonly IReadOnlyList<(string Label, string Value)> textAlignmentOptions =
	[
		(publisherDefaultOptionLabel, ""),
		("Left", "left"),
		("Justified", "justify")
	];
	static readonly IReadOnlyList<(string Label, string Value)> paragraphSpacingOptions =
	[
		(publisherDefaultOptionLabel, ""),
		("Compact", "0"),
		("Normal", "0.5rem"),
		("Relaxed", "1rem"),
		("Spacious", "1.5rem")
	];
	static readonly IReadOnlyList<(string Label, string Value)> hyphenationOptions =
	[
		(publisherDefaultOptionLabel, ""),
		("Automatic", "auto"),
		("Manual Only", "manual"),
		("Off", "none")
	];
	static readonly IReadOnlyList<(string Label, string Value)> letterSpacingOptions =
	[
		(publisherDefaultOptionLabel, ""),
		("Normal", "0"),
		("Wide", "0.02em"),
		("Wider", "0.04em"),
		("Extra Wide", "0.06em")
	];

	readonly JsonSerializerOptions jsonOptions = new() { WriteIndented = true };
	static readonly ILogger logger = LoggerFactory.GetLogger(nameof(SettingsPage));
	readonly IFolderPicker folderPicker = Application.Current?.Windows[0].Page?.Handler?.MauiContext?.Services.GetService<IFolderPicker>() ?? throw new InvalidOperationException();
	readonly IDb db = Application.Current?.Windows[0].Page?.Handler?.MauiContext?.Services.GetRequiredService<IDb>() ?? throw new InvalidOperationException();
	Settings? settings;
	const string deleteLocalDataTitle = "Delete Local Data";
	const string exportDataTitle = "Export Data";


	/// <summary>
	/// Initializes a new instance of the <see cref="SettingsPage"/> class with the specified view model and database.
	/// </summary>
	/// <remarks>This constructor sets up the settings page by initializing the component, setting the data binding
	/// context, and configuring UI elements based on the current settings retrieved from the database.</remarks>
	/// <param name="viewModel">The view model that provides data binding for the settings page.</param>
	public SettingsPage(SettingsPageViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = viewModel;
		LineSpacingPicker.ItemsSource = lineSpacingOptions.Select(option => option.Label).ToList();
		TextAlignmentPicker.ItemsSource = textAlignmentOptions.Select(option => option.Label).ToList();
		ParagraphSpacingPicker.ItemsSource = paragraphSpacingOptions.Select(option => option.Label).ToList();
		HyphenationPicker.ItemsSource = hyphenationOptions.Select(option => option.Label).ToList();
		LetterSpacingPicker.ItemsSource = letterSpacingOptions.Select(option => option.Label).ToList();
	}

	/// <summary>
	/// Handles the event when the font size slider value changes.
	/// </summary>
	/// <remarks>Updates the font size setting and persists the change. Sends a message to notify other components
	/// of the update.</remarks>
	/// <param name="sender">The source of the event, typically the slider control.</param>
	/// <param name="e">The <see cref="ValueChangedEventArgs"/> containing the old and new values of the slider.</param>
	async void OnFontSizeSliderChanged(object? sender, ValueChangedEventArgs? e)
	{
		if (e is null)
		{
			logger.Warn("ValueChangedEventArgs is null, cannot change font size.");
			return;
		}
		if (settings is null)
		{
			return;
		}
		settings.FontSize = NormalizeFontSize((int)Math.Round(e.NewValue, MidpointRounding.AwayFromZero));
		await db.SaveSettings(settings);
		WeakReferenceMessenger.Default.Send(new SettingsMessage(SettingsChangeKind.FontSize));
	}

	/// <summary>
	/// Removes all user settings and resets them to default values.
	/// </summary>
	/// <remarks>This method clears all existing settings from the database and initializes new default settings. It
	/// updates the UI components to reflect the default settings and sends a notification indicating that the settings
	/// have been reset.</remarks>
	/// <param name="sender">The source of the event that triggered the method.</param>
	/// <param name="e">The <see cref="EventArgs"/> containing event data.</param>
	async void RemoveAllSettings(object? sender, EventArgs? e)
	{
		await db.RemoveAllSettings();
		settings = new Settings();
		await db.SaveSettings(settings);
		switchControl.IsToggled = settings.SupportMultipleColumns;
		ThemePicker.SelectedItem = ((SettingsPageViewModel)BindingContext).ColorSchemes.Find(x => x.Name == settings.ColorScheme);
		FontPicker.SelectedItem = ((SettingsPageViewModel)BindingContext).Fonts.Find(x => x.FontFamily == settings.FontFamily);
		FontSizeSlider.Value = settings.FontSize;
		LineSpacingPicker.SelectedIndex = GetLineSpacingOptionIndex(settings.LineSpacing);
		TextAlignmentPicker.SelectedIndex = GetTextAlignmentOptionIndex(settings.TextAlignment);
		ParagraphSpacingPicker.SelectedIndex = GetParagraphSpacingOptionIndex(settings.ParagraphSpacing);
		HyphenationPicker.SelectedIndex = GetHyphenationOptionIndex(settings.BodyHyphens);
		LetterSpacingPicker.SelectedIndex = GetLetterSpacingOptionIndex(settings.LetterSpacing);
		WeakReferenceMessenger.Default.Send(new SettingsMessage(SettingsChangeKind.Reset));
		logger.Info("Settings removed");
	}

	async void LineSpacingPicker_SelectedIndexChanged(object? sender, EventArgs? e)
	{
		if (settings is null)
		{
			logger.Warn("Settings are null, cannot change line spacing.");
			return;
		}

		if (LineSpacingPicker.SelectedIndex < 0 || LineSpacingPicker.SelectedIndex >= lineSpacingOptions.Count)
		{
			return;
		}

		var selectedValue = NormalizeLineSpacing(lineSpacingOptions[LineSpacingPicker.SelectedIndex].Value);
		if (string.Equals(settings.LineSpacing, selectedValue, StringComparison.Ordinal))
		{
			return;
		}

		settings.LineSpacing = selectedValue;
		await db.SaveSettings(settings);
		WeakReferenceMessenger.Default.Send(new SettingsMessage(SettingsChangeKind.LineSpacing));
	}

	async void TextAlignmentPicker_SelectedIndexChanged(object? sender, EventArgs? e)
	{
		if (settings is null)
		{
			logger.Warn("Settings are null, cannot change text alignment.");
			return;
		}

		if (TextAlignmentPicker.SelectedIndex < 0 || TextAlignmentPicker.SelectedIndex >= textAlignmentOptions.Count)
		{
			return;
		}

		var selectedValue = NormalizeTextAlignment(textAlignmentOptions[TextAlignmentPicker.SelectedIndex].Value);
		if (string.Equals(settings.TextAlignment, selectedValue, StringComparison.Ordinal))
		{
			return;
		}

		settings.TextAlignment = selectedValue;
		await db.SaveSettings(settings);
		WeakReferenceMessenger.Default.Send(new SettingsMessage(SettingsChangeKind.TextAlignment));
	}

	async void ParagraphSpacingPicker_SelectedIndexChanged(object? sender, EventArgs? e)
	{
		if (settings is null)
		{
			logger.Warn("Settings are null, cannot change paragraph spacing.");
			return;
		}

		if (ParagraphSpacingPicker.SelectedIndex < 0 || ParagraphSpacingPicker.SelectedIndex >= paragraphSpacingOptions.Count)
		{
			return;
		}

		var selectedValue = NormalizeParagraphSpacing(paragraphSpacingOptions[ParagraphSpacingPicker.SelectedIndex].Value);
		if (string.Equals(settings.ParagraphSpacing, selectedValue, StringComparison.Ordinal))
		{
			return;
		}

		settings.ParagraphSpacing = selectedValue;
		await db.SaveSettings(settings);
		WeakReferenceMessenger.Default.Send(new SettingsMessage(SettingsChangeKind.ParagraphSpacing));
	}

	async void HyphenationPicker_SelectedIndexChanged(object? sender, EventArgs? e)
	{
		if (settings is null)
		{
			logger.Warn("Settings are null, cannot change hyphenation.");
			return;
		}

		if (HyphenationPicker.SelectedIndex < 0 || HyphenationPicker.SelectedIndex >= hyphenationOptions.Count)
		{
			return;
		}

		var selectedValue = NormalizeBodyHyphens(hyphenationOptions[HyphenationPicker.SelectedIndex].Value);
		if (string.Equals(settings.BodyHyphens, selectedValue, StringComparison.Ordinal))
		{
			return;
		}

		settings.BodyHyphens = selectedValue;
		await db.SaveSettings(settings);
		WeakReferenceMessenger.Default.Send(new SettingsMessage(SettingsChangeKind.Hyphenation));
	}

	async void LetterSpacingPicker_SelectedIndexChanged(object? sender, EventArgs? e)
	{
		if (settings is null)
		{
			logger.Warn("Settings are null, cannot change letter spacing.");
			return;
		}

		if (LetterSpacingPicker.SelectedIndex < 0 || LetterSpacingPicker.SelectedIndex >= letterSpacingOptions.Count)
		{
			return;
		}

		var selectedValue = NormalizeLetterSpacing(letterSpacingOptions[LetterSpacingPicker.SelectedIndex].Value);
		if (string.Equals(settings.LetterSpacing, selectedValue, StringComparison.Ordinal))
		{
			return;
		}

		settings.LetterSpacing = selectedValue;
		await db.SaveSettings(settings);
		WeakReferenceMessenger.Default.Send(new SettingsMessage(SettingsChangeKind.LetterSpacing));
	}

	/// <summary>
	/// Handles the event when the selected index of the theme picker changes.
	/// </summary>
	/// <remarks>Updates the application's color scheme based on the selected theme. If the selected theme is
	/// different from the current one, it updates the background and text colors, logs the change, saves the new settings
	/// to the database, and notifies other components of the change.</remarks>
	/// <param name="sender">The source of the event.</param>
	/// <param name="e">The event data.</param>
	async void ThemePicker_SelectedIndexChanged(object? sender, EventArgs? e)
	{
		if (settings is null)
		{
			logger.Warn("Settings are null, cannot change theme.");
			return;
		}
		var selectedTheme = ThemePicker.SelectedItem;
		if (selectedTheme is not ColorScheme scheme || settings.ColorScheme == scheme.Name)
		{
			return;
		}

		settings.BackgroundColor = scheme.BackgroundColor;
		settings.TextColor = scheme.TextColor;
		settings.ColorScheme = scheme.Name;
		logger.Info($"Changing color scheme to: {scheme.Name}");
		await db.SaveSettings(settings);
		WeakReferenceMessenger.Default.Send(new SettingsMessage(SettingsChangeKind.Theme));
	}

	async void ThemePreview_SelectionChanged(object? sender, SelectionChangedEventArgs? e)
	{
		if (settings is null)
		{
			logger.Warn("Settings are null, cannot change theme from preview.");
			return;
		}

		if (e?.CurrentSelection is null || e.CurrentSelection.Count == 0)
		{
			return;
		}
		if (e.CurrentSelection[0] is not ColorScheme selected || settings.ColorScheme == selected.Name)
		{
			return;
		}

		// Mirror selection to the picker and reuse existing handler
		ThemePicker.SelectedItem = selected;
		ThemePicker_SelectedIndexChanged(ThemePicker, EventArgs.Empty);
	}

	/// <summary>
	/// Handles the event when the selected font changes in the font picker.
	/// </summary>
	/// <remarks>Updates the application's font settings to the newly selected font, logs the change, saves the
	/// updated settings to the database, and notifies other components of the change.</remarks>
	/// <param name="sender">The source of the event, typically the font picker control.</param>
	/// <param name="e">The event data associated with the selection change.</param>
	async void FontPicker_SelectedIndexChanged(object? sender, EventArgs? e)
	{
		if (settings is null)
		{
			logger.Warn("Settings are null, cannot change font.");
			return;
		}
		var selectedTheme = FontPicker.SelectedItem;
		if (selectedTheme is not EpubFonts font || settings.FontFamily == font.FontFamily)
		{
			return;
		}

		var family = SanitizeFontFamily(font.FontFamily);
		settings.FontFamily = family;
		logger.Info($"Chaging Font to: {family}");
		await db.SaveSettings(settings);
		WeakReferenceMessenger.Default.Send(new SettingsMessage(SettingsChangeKind.FontFamily));

		if (!string.IsNullOrEmpty(family) && FontPreview is not null)
		{
			FontPreview.FontFamily = family;
		}
		else
		{
			System.Diagnostics.Trace.TraceWarning("Font family is null or empty, cannot update font preview.");
		}
	}

#pragma warning disable S2325 // Suppress "Methods that don't access instance data should be static" for event handlers
	void CurrentPage_Unloaded(object? sender, EventArgs e)
	{
		stackLayout.Remove(switchControl);
	}
#pragma warning restore S2325 // Restore "Methods that don't access instance data should be static" for event handlers

	/// <summary>
	/// Handles the toggle event for the switch control to update the settings for supporting multiple columns.
	/// </summary>
	/// <remarks>This method updates the <see cref="settings"/> to reflect the current toggle state of the switch
	/// control. It logs a warning if the settings or switch control is null and does not proceed with the update. After
	/// updating the settings, it saves the changes to the database and sends a message indicating the update.</remarks>
	/// <param name="sender">The source of the event, typically the switch control.</param>
	/// <param name="e">The event data containing the toggle state.</param>
	async void switchControl_Toggled(object? sender, ToggledEventArgs? e)
	{
		if (sender is null)
		{
			logger.Warn("Sender is null, cannot toggle multiple columns.");
			return;
		}
		if (settings is null)
		{
			logger.Warn("Settings are null, cannot toggle multiple columns.");
			return;
		}
		if (switchControl is null)
		{
			logger.Warn("Switch control is null, cannot toggle multiple columns.");
			return;
		}
		settings.SupportMultipleColumns = switchControl.IsToggled;
		await db.SaveSettings(settings);
		WeakReferenceMessenger.Default.Send(new SettingsMessage(SettingsChangeKind.Layout));
	}

	async void CurrentPage_Loaded(object? sender, EventArgs? e)
	{
		settings = await db.GetSettings() ?? new();
		var normalizedFontSize = NormalizeFontSize(settings.FontSize);
		var normalizedLineSpacing = NormalizeLineSpacing(settings.LineSpacing);
		var normalizedTextAlignment = NormalizeTextAlignment(settings.TextAlignment);
		var normalizedParagraphSpacing = NormalizeParagraphSpacing(settings.ParagraphSpacing);
		var normalizedBodyHyphens = NormalizeBodyHyphens(settings.BodyHyphens);
		var normalizedLetterSpacing = NormalizeLetterSpacing(settings.LetterSpacing);
		if (settings.FontSize != normalizedFontSize
			|| !string.Equals(settings.LineSpacing, normalizedLineSpacing, StringComparison.Ordinal)
			|| !string.Equals(settings.TextAlignment, normalizedTextAlignment, StringComparison.Ordinal)
			|| !string.Equals(settings.ParagraphSpacing, normalizedParagraphSpacing, StringComparison.Ordinal)
			|| !string.Equals(settings.BodyHyphens, normalizedBodyHyphens, StringComparison.Ordinal)
			|| !string.Equals(settings.LetterSpacing, normalizedLetterSpacing, StringComparison.Ordinal))
		{
			settings.FontSize = normalizedFontSize;
			settings.LineSpacing = normalizedLineSpacing;
			settings.TextAlignment = normalizedTextAlignment;
			settings.ParagraphSpacing = normalizedParagraphSpacing;
			settings.BodyHyphens = normalizedBodyHyphens;
			settings.LetterSpacing = normalizedLetterSpacing;
			await db.SaveSettings(settings);
		}

		FontSizeSlider.Value = settings.FontSize;
		LineSpacingPicker.SelectedIndex = GetLineSpacingOptionIndex(settings.LineSpacing);
		TextAlignmentPicker.SelectedIndex = GetTextAlignmentOptionIndex(settings.TextAlignment);
		ParagraphSpacingPicker.SelectedIndex = GetParagraphSpacingOptionIndex(settings.ParagraphSpacing);
		HyphenationPicker.SelectedIndex = GetHyphenationOptionIndex(settings.BodyHyphens);
		LetterSpacingPicker.SelectedIndex = GetLetterSpacingOptionIndex(settings.LetterSpacing);
		switchControl.IsToggled = settings.SupportMultipleColumns;
		FontPicker.SelectedItem = ((SettingsPageViewModel)BindingContext).Fonts.Find(x => x.FontFamily == settings.FontFamily);
		var scheme = ((SettingsPageViewModel)BindingContext).ColorSchemes.Find(x => x.Name == settings.ColorScheme);
		ThemePicker.SelectedItem = scheme;
		if (ThemePreview is not null && scheme is not null)
		{
			ThemePreview.SelectedItem = scheme;
		}

		if (FontPreview is not null && FontPicker.SelectedItem is EpubFonts selectedFont)
		{
			FontPreview.FontFamily = SanitizeFontFamily(selectedFont.FontFamily);
		}

		if (BindingContext is SettingsPageViewModel viewModel)
		{
			await viewModel.LoadAuthStatusAsync();
		}
	}

	static string SanitizeFontFamily(string? family)
	{
		if (string.IsNullOrEmpty(family))
		{
			return string.Empty;
		}

		var name = family;
		if (name.Contains('/') || name.Contains('\\'))
		{
			name = Path.GetFileName(name);
		}
		if (name.EndsWith(".ttf", StringComparison.InvariantCultureIgnoreCase) || name.EndsWith(".otf", StringComparison.InvariantCultureIgnoreCase))
		{
			name = Path.GetFileNameWithoutExtension(name);
		}
		return name;
	}

	static int NormalizeFontSize(int fontSize)
	{
		if (fontSize <= 0)
		{
			return defaultReaderFontSize;
		}

		return Math.Clamp(fontSize, minimumReaderFontSize, maximumReaderFontSize);
	}

	static string NormalizeLineSpacing(string? lineSpacing)
	{
		if (string.IsNullOrWhiteSpace(lineSpacing))
		{
			return defaultReaderLineSpacing;
		}

		if (!double.TryParse(lineSpacing, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedValue))
		{
			return defaultReaderLineSpacing;
		}

		var nearest = lineSpacingOptions
			.Select(option => option.Value)
			.Select(value => double.Parse(value, CultureInfo.InvariantCulture))
			.OrderBy(value => Math.Abs(value - parsedValue))
			.FirstOrDefault();

		return nearest.ToString("0.##", CultureInfo.InvariantCulture);
	}

	static string NormalizeTextAlignment(string? textAlignment)
	{
		if (string.IsNullOrWhiteSpace(textAlignment))
		{
			return defaultReaderTextAlignment;
		}

		return textAlignment.Trim().ToLowerInvariant() switch
		{
			"left" => "left",
			"justify" => "justify",
			_ => defaultReaderTextAlignment
		};
	}

	static string NormalizeParagraphSpacing(string? paragraphSpacing)
	{
		if (string.IsNullOrWhiteSpace(paragraphSpacing))
		{
			return defaultReaderParagraphSpacing;
		}

		return paragraphSpacing.Trim().ToLowerInvariant() switch
		{
			"0" or "0rem" or "0.0" or "0.0rem" => "0",
			"0.5rem" => "0.5rem",
			"1rem" => "1rem",
			"1.5rem" => "1.5rem",
			_ => defaultReaderParagraphSpacing
		};
	}

	static string NormalizeBodyHyphens(string? bodyHyphens)
	{
		if (string.IsNullOrWhiteSpace(bodyHyphens))
		{
			return defaultReaderBodyHyphens;
		}

		return bodyHyphens.Trim().ToLowerInvariant() switch
		{
			"auto" => "auto",
			"manual" => "manual",
			"none" => "none",
			_ => defaultReaderBodyHyphens
		};
	}

	static string NormalizeLetterSpacing(string? letterSpacing)
	{
		if (string.IsNullOrWhiteSpace(letterSpacing))
		{
			return defaultReaderLetterSpacing;
		}

		return letterSpacing.Trim().ToLowerInvariant() switch
		{
			"0" or "0em" or "0.0em" => "0",
			"0.02em" => "0.02em",
			"0.04em" => "0.04em",
			"0.06em" => "0.06em",
			_ => defaultReaderLetterSpacing
		};
	}

	static int GetLineSpacingOptionIndex(string? lineSpacing)
	{
		var normalizedValue = NormalizeLineSpacing(lineSpacing);
		for (var index = 0; index < lineSpacingOptions.Count; index++)
		{
			if (string.Equals(lineSpacingOptions[index].Value, normalizedValue, StringComparison.Ordinal))
			{
				return index;
			}
		}

		return 1;
	}

	static int GetTextAlignmentOptionIndex(string? textAlignment)
	{
		var normalizedValue = NormalizeTextAlignment(textAlignment);
		for (var index = 0; index < textAlignmentOptions.Count; index++)
		{
			if (string.Equals(textAlignmentOptions[index].Value, normalizedValue, StringComparison.Ordinal))
			{
				return index;
			}
		}

		return 0;
	}

	static int GetParagraphSpacingOptionIndex(string? paragraphSpacing)
	{
		var normalizedValue = NormalizeParagraphSpacing(paragraphSpacing);
		for (var index = 0; index < paragraphSpacingOptions.Count; index++)
		{
			if (string.Equals(paragraphSpacingOptions[index].Value, normalizedValue, StringComparison.Ordinal))
			{
				return index;
			}
		}

		return 0;
	}

	static int GetHyphenationOptionIndex(string? bodyHyphens)
	{
		var normalizedValue = NormalizeBodyHyphens(bodyHyphens);
		for (var index = 0; index < hyphenationOptions.Count; index++)
		{
			if (string.Equals(hyphenationOptions[index].Value, normalizedValue, StringComparison.Ordinal))
			{
				return index;
			}
		}

		return 0;
	}

	static int GetLetterSpacingOptionIndex(string? letterSpacing)
	{
		var normalizedValue = NormalizeLetterSpacing(letterSpacing);
		for (var index = 0; index < letterSpacingOptions.Count; index++)
		{
			if (string.Equals(letterSpacingOptions[index].Value, normalizedValue, StringComparison.Ordinal))
			{
				return index;
			}
		}

		return 0;
	}

	async void OnCloseClicked(object? sender, EventArgs? e)
	{
		Trace.TraceInformation("SettingsPage.OnCloseClicked: Close button pressed");
		await this.CloseAsync();
	}

	async void OnPrivacyClicked(object? sender, EventArgs? e)
	{
		// Close settings popup then navigate to the in-app privacy page
		try
		{
			await this.CloseAsync();
			await Shell.Current.GoToAsync("privacy");
		}
		catch (Exception ex)
		{
			Trace.TraceWarning($"SettingsPage.OnPrivacyClicked: navigation failed: {ex}");
		}
	}

	async void OnExportDataClicked(object? sender, EventArgs? e)
	{
		try
		{
			await this.CloseAsync();
			if (db is null)
			{
				await Shell.Current.DisplayAlertAsync(exportDataTitle, "Database not available.", "OK");
				return;
			}
			var export = new
			{
				Settings = await db.GetSettings(),
				Books = await db.GetAllBooks()
			};
			var json = System.Text.Json.JsonSerializer.Serialize(export, jsonOptions);

			// Prompt user to choose a folder to save the export

			string path;
			if (folderPicker is null)
			{
				await Shell.Current.DisplayAlertAsync(exportDataTitle, "Export Failed.", "OK");
				return;
			}

			var folder = await folderPicker.PickFolderAsync();
			if (string.IsNullOrEmpty(folder))
			{
				// User cancelled folder selection
				return;
			}
			path = Path.Combine(folder, "epubreader_export.json");
			await File.WriteAllTextAsync(path, json);
			await Shell.Current.DisplayAlertAsync(exportDataTitle, $"Export saved to {path}", "OK");
		}
		catch (Exception ex)
		{
			Trace.TraceError($"Export failed: {ex}");
			await Shell.Current.DisplayAlertAsync(exportDataTitle, "Export failed", "OK");
		}
	}

	async void OnDeleteLocalDataClicked(object? sender, EventArgs? e)
	{
		var ok = await Shell.Current.DisplayAlertAsync(deleteLocalDataTitle, "This will remove local books, settings and progress from this device. This cannot be undone. Continue?", "Delete", "Cancel");
		if (!ok)
		{
			return;
		}
		try
		{
			if (db is null)
			{
				await Shell.Current.DisplayAlertAsync(deleteLocalDataTitle, "Database not available.", "OK");
				return;
			}
			await db.RemoveAllBooks();
			await db.RemoveAllSettings();
			await Shell.Current.DisplayAlertAsync(deleteLocalDataTitle, "Local data deleted.", "OK");
		}
		catch (Exception ex)
		{
			Trace.TraceError($"Delete local data failed: {ex}");
			await Shell.Current.DisplayAlertAsync(deleteLocalDataTitle, "Delete failed", "OK");
		}
	}
}