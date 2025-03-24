using CommunityToolkit.Maui.Views;
using CommunityToolkit.Mvvm.Messaging;
using EpubReader.Interfaces;
using EpubReader.Messages;
using EpubReader.Models;
using EpubReader.ViewModels;
using MetroLog;

namespace EpubReader.Views;

public partial class SettingsPage : Popup, IDisposable
{
	readonly CancellationTokenSource cancellationTokenSource;
	readonly Task loadTask;
	bool disposedValue;
	static readonly ILogger logger = LoggerFactory.GetLogger(nameof(SettingsPage));
	readonly IDb db;
	public SettingsPage(SettingsPageViewModel viewModel, IDb db)
	{
        InitializeComponent();
		BindingContext = viewModel;
		this.db = db;
		cancellationTokenSource = new CancellationTokenSource();
		loadTask = LoadSettings(cancellationTokenSource.Token);
		if (loadTask.IsFaulted)
		{
			logger.Info("Error loading settings");
		}
	}
	async Task LoadSettings(CancellationToken cancellationToken = default)
	{
		var settings = await db.GetSettings(cancellationToken);
		FontSizeSlider.Value = settings.FontSize;
		FontPicker.SelectedItem = ((SettingsPageViewModel)BindingContext).Fonts.Find(x => x.FontFamily == settings.FontFamily);
		ThemePicker.SelectedItem = ((SettingsPageViewModel)BindingContext).ColorSchemes.Find(x => x.Name == settings.ColorScheme);
	}
	async void OnFontSizeSliderChanged(object sender, ValueChangedEventArgs e)
	{
		if((int)e.NewValue == 0)
		{
			return;
		}
		var settings = await db.GetSettings(CancellationToken.None);
		settings.FontSize = (int)e.NewValue;
		await db.SaveSettings(settings);
		WeakReferenceMessenger.Default.Send(new SettingsMessage(true));
	}

	async void RemoveAllSettings(object sender, EventArgs e)
	{
		await db.RemoveAllSettings(CancellationToken.None);
		var settings = new Settings();
		await db.SaveSettings(settings, CancellationToken.None);
		ThemePicker.SelectedItem = ((SettingsPageViewModel)BindingContext).ColorSchemes.Find(x => x.Name == settings.ColorScheme);
		FontPicker.SelectedItem = ((SettingsPageViewModel)BindingContext).Fonts.Find(x => x.FontFamily == settings.FontFamily);
		FontSizeSlider.Value = settings.FontSize;
		WeakReferenceMessenger.Default.Send(new SettingsMessage(true));
		logger.Info("Settings removed");
	}

	protected virtual void Dispose(bool disposing)
	{
		if (!disposedValue)
		{
			if (disposing)
			{
				cancellationTokenSource?.Dispose();
				loadTask?.Dispose();
			}

			disposedValue = true;
		}
	}

	public void Dispose()
	{
		Dispose(disposing: true);
		GC.SuppressFinalize(this);
	}

	async void ThemePicker_SelectedIndexChanged(object sender, EventArgs e)
	{
		var settings = await db.GetSettings(CancellationToken.None);
		var selectedTheme = ThemePicker.SelectedItem;
		if (selectedTheme is not ColorScheme scheme || settings.ColorScheme == scheme.Name)
		{
			return;
		}
		
		settings.BackgroundColor = scheme.BackgroundColor;
		settings.TextColor = scheme.TextColor;
		settings.ColorScheme = scheme.Name;
		settings.SetTextColor = $"--USER__textColor: {settings.TextColor}";
		settings.SetBackgroundColor = $"--USER__backgroundColor: {settings.BackgroundColor}";
		logger.Info($"Changing color scheme to: {scheme.Name}");
		await db.SaveSettings(settings, CancellationToken.None);
		WeakReferenceMessenger.Default.Send(new SettingsMessage(true));
	}

	async void FontPicker_SelectedIndexChanged(object sender, EventArgs e)
	{
		var settings = await db.GetSettings(CancellationToken.None);
		var selectedTheme = FontPicker.SelectedItem;
		if (selectedTheme is not EpubFonts font || settings.FontFamily == font.FontFamily)
		{
			return;
		}

		settings.FontFamily = font.FontFamily;
		logger.Info($"Chaging Font to: {font.FontFamily}");
		await db.SaveSettings(settings, CancellationToken.None);
		WeakReferenceMessenger.Default.Send(new SettingsMessage(true));
	}
}
