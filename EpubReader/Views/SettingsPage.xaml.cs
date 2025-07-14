using CommunityToolkit.Maui.Views;
using CommunityToolkit.Mvvm.Messaging;
using EpubReader.Interfaces;
using EpubReader.Messages;
using EpubReader.Models;
using EpubReader.ViewModels;
using MetroLog;

namespace EpubReader.Views;

/// <summary>
/// Represents a settings page within the application, allowing users to view and modify application settings.
/// </summary>
/// <remarks>The <see cref="SettingsPage"/> class provides a user interface for managing application settings such
/// as font size, theme, and layout preferences. It interacts with a database to retrieve and persist settings, and
/// updates the UI components to reflect the current settings. The page also sends notifications to other components
/// when settings are changed.</remarks>
public partial class SettingsPage : Popup
{
	static readonly ILogger logger = LoggerFactory.GetLogger(nameof(SettingsPage));
	readonly IDb db;
	Settings? settings;
	readonly Task settingsTask;


	/// <summary>
	/// Initializes a new instance of the <see cref="SettingsPage"/> class with the specified view model and database.
	/// </summary>
	/// <remarks>This constructor sets up the settings page by initializing the component, setting the data binding
	/// context, and configuring UI elements based on the current settings retrieved from the database.</remarks>
	/// <param name="viewModel">The view model that provides data binding for the settings page.</param>
	/// <param name="db">The database interface used to retrieve and store settings.</param>
	public SettingsPage(SettingsPageViewModel viewModel, IDb db)
	{
        InitializeComponent();
		BindingContext = viewModel;
		this.db = db;
		settingsTask = InitializeSettings();
		
	}

	async Task InitializeSettings()
	{
		settings = await db.GetSettings() ?? new();
		FontSizeSlider.Value = settings.FontSize;
		ButtonColumn.Text = settings.SupportMultipleColumns ? "Disable Multiple Columns" : "Enable Multiple Columns";
		FontPicker.SelectedItem = ((SettingsPageViewModel)BindingContext).Fonts.Find(x => x.FontFamily == settings.FontFamily);
		ThemePicker.SelectedItem = ((SettingsPageViewModel)BindingContext).ColorSchemes.Find(x => x.Name == settings.ColorScheme);
	}


	/// <summary>
	/// Handles the event when the font size slider value changes.
	/// </summary>
	/// <remarks>Updates the font size setting and persists the change. Sends a message to notify other components
	/// of the update.</remarks>
	/// <param name="sender">The source of the event, typically the slider control.</param>
	/// <param name="e">The <see cref="ValueChangedEventArgs"/> containing the old and new values of the slider.</param>
	async void OnFontSizeSliderChanged(object sender, ValueChangedEventArgs e)
	{
		if((int)e.NewValue == 0 || settings is null)
		{
			return;
		}
		settings.FontSize = (int)e.NewValue;
		await db.SaveSettings(settings);
		WeakReferenceMessenger.Default.Send(new SettingsMessage(true));
	}

	/// <summary>
	/// Removes all user settings and resets them to default values.
	/// </summary>
	/// <remarks>This method clears all existing settings from the database and initializes new default settings. It
	/// updates the UI components to reflect the default settings and sends a notification indicating that the settings
	/// have been reset.</remarks>
	/// <param name="sender">The source of the event that triggered the method.</param>
	/// <param name="e">The <see cref="EventArgs"/> containing event data.</param>
	async void RemoveAllSettings(object sender, EventArgs e)
	{
		await db.RemoveAllSettings();
		settings = new Settings();
		await db.SaveSettings(settings);
		ButtonColumn.Text = settings.SupportMultipleColumns ? "Disable Multiple Columns" : "Enable Multiple Columns";
		ThemePicker.SelectedItem = ((SettingsPageViewModel)BindingContext).ColorSchemes.Find(x => x.Name == settings.ColorScheme);
		FontPicker.SelectedItem = ((SettingsPageViewModel)BindingContext).Fonts.Find(x => x.FontFamily == settings.FontFamily);
		FontSizeSlider.Value = settings.FontSize;
		WeakReferenceMessenger.Default.Send(new SettingsMessage(true));
		logger.Info("Settings removed");
	}

	/// <summary>
	/// Handles the event when the selected index of the theme picker changes.
	/// </summary>
	/// <remarks>Updates the application's color scheme based on the selected theme. If the selected theme is
	/// different from the current one, it updates the background and text colors, logs the change, saves the new settings
	/// to the database, and notifies other components of the change.</remarks>
	/// <param name="sender">The source of the event.</param>
	/// <param name="e">The event data.</param>
	async void ThemePicker_SelectedIndexChanged(object sender, EventArgs e)
	{
		if(settings is null)
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
		WeakReferenceMessenger.Default.Send(new SettingsMessage(true));
	}

	/// <summary>
	/// Handles the event when the selected font changes in the font picker.
	/// </summary>
	/// <remarks>Updates the application's font settings to the newly selected font, logs the change, saves the
	/// updated settings to the database, and notifies other components of the change.</remarks>
	/// <param name="sender">The source of the event, typically the font picker control.</param>
	/// <param name="e">The event data associated with the selection change.</param>
	async void FontPicker_SelectedIndexChanged(object sender, EventArgs e)
	{
		if(settings is null)
		{
			logger.Warn("Settings are null, cannot change font.");
			return;
		}
		var selectedTheme = FontPicker.SelectedItem;
		if (selectedTheme is not EpubFonts font || settings.FontFamily == font.FontFamily)
		{
			return;
		}

		settings.FontFamily = font.FontFamily;
		logger.Info($"Chaging Font to: {font.FontFamily}");
		await db.SaveSettings(settings);
		WeakReferenceMessenger.Default.Send(new SettingsMessage(true));
	}

	/// <summary>
	/// Toggles the support for multiple columns in the application settings.
	/// </summary>
	/// <remarks>This method updates the application settings to enable or disable support for multiple columns,
	/// saves the updated settings to the database, and sends a notification message indicating the change. The button text
	/// is also updated to reflect the current state.</remarks>
	/// <param name="sender">The source of the event.</param>
	/// <param name="e">The event data.</param>
	async void ToggleMultipleColumns(object sender, EventArgs e)
	{
		if(settings is null)
		{
			logger.Warn("Settings are null, cannot toggle multiple columns.");
			return;
		}
		settings.SupportMultipleColumns = !settings.SupportMultipleColumns;
		await db.SaveSettings(settings);
		WeakReferenceMessenger.Default.Send(new SettingsMessage(true));
		ButtonColumn.Text = settings.SupportMultipleColumns ? "Disable Multiple Columns" : "Enable Multiple Columns";
	}
}
