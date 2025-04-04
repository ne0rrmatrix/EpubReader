using CommunityToolkit.Maui.Views;
using CommunityToolkit.Mvvm.Messaging;
using EpubReader.Interfaces;
using EpubReader.Messages;
using EpubReader.Models;
using EpubReader.ViewModels;
using MetroLog;

namespace EpubReader.Views;

public partial class SettingsPage : Popup
{
	static readonly ILogger logger = LoggerFactory.GetLogger(nameof(SettingsPage));
	readonly IDb db;
	Settings settings;
	public SettingsPage(SettingsPageViewModel viewModel, IDb db)
	{
        InitializeComponent();
		BindingContext = viewModel;
		this.db = db;
		settings = db.GetSettings() ?? new();
		FontSizeSlider.Value = settings.FontSize;
		FontPicker.SelectedItem = ((SettingsPageViewModel)BindingContext).Fonts.Find(x => x.FontFamily == settings.FontFamily);
		ThemePicker.SelectedItem = ((SettingsPageViewModel)BindingContext).ColorSchemes.Find(x => x.Name == settings.ColorScheme);
	}

	void OnFontSizeSliderChanged(object sender, ValueChangedEventArgs e)
	{
		if((int)e.NewValue == 0)
		{
			return;
		}
		settings.FontSize = (int)e.NewValue;
		db.SaveSettings(settings);
		WeakReferenceMessenger.Default.Send(new SettingsMessage(true));
	}

	void RemoveAllSettings(object sender, EventArgs e)
	{
		db.RemoveAllSettings();
		settings = new Settings();
		db.SaveSettings(settings);
		ThemePicker.SelectedItem = ((SettingsPageViewModel)BindingContext).ColorSchemes.Find(x => x.Name == settings.ColorScheme);
		FontPicker.SelectedItem = ((SettingsPageViewModel)BindingContext).Fonts.Find(x => x.FontFamily == settings.FontFamily);
		FontSizeSlider.Value = settings.FontSize;
		WeakReferenceMessenger.Default.Send(new SettingsMessage(true));
		logger.Info("Settings removed");
	}

	void ThemePicker_SelectedIndexChanged(object sender, EventArgs e)
	{
		var selectedTheme = ThemePicker.SelectedItem;
		if (selectedTheme is not ColorScheme scheme || settings.ColorScheme == scheme.Name)
		{
			return;
		}
		
		settings.BackgroundColor = scheme.BackgroundColor;
		settings.TextColor = scheme.TextColor;
		settings.ColorScheme = scheme.Name;
		logger.Info($"Changing color scheme to: {scheme.Name}");
		db.SaveSettings(settings);
		WeakReferenceMessenger.Default.Send(new SettingsMessage(true));
	}

	void FontPicker_SelectedIndexChanged(object sender, EventArgs e)
	{
		var selectedTheme = FontPicker.SelectedItem;
		if (selectedTheme is not EpubFonts font || settings.FontFamily == font.FontFamily)
		{
			return;
		}

		settings.FontFamily = font.FontFamily;
		logger.Info($"Chaging Font to: {font.FontFamily}");
		db.SaveSettings(settings);
		WeakReferenceMessenger.Default.Send(new SettingsMessage(true));
	}
}
