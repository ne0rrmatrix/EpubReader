using CommunityToolkit.Maui.Views;
using CommunityToolkit.Mvvm.Messaging;
using EpubReader.Interfaces;
using EpubReader.Messages;
using EpubReader.Models;
using EpubReader.ViewModels;
using ExCSS;
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

	async void OnApplyChanges(object sender, EventArgs e)
	{
		var settings = await db.GetSettings(CancellationToken.None);
		var selectedTheme = ThemePicker.SelectedItem;
		if (selectedTheme is ColorScheme scheme)
		{
			settings.BackgroundColor = scheme.BackgroundColor;
			settings.TextColor = scheme.TextColor;
			settings.ColorScheme = scheme.Name;
			logger.Info($"Changing color scheme to: {scheme.Name}");
		}

		selectedTheme = FontPicker.SelectedItem;
		if (selectedTheme is EbookFonts font)
		{
			settings.FontFamily = font.FontFamily;
			logger.Info($"Chaging Font to: {font.FontFamily}");
		}
		await db.SaveSettings(settings, CancellationToken.None);
		WeakReferenceMessenger.Default.Send(new SettingsMessage(true));
	}

	async void RemoveAllSettings(object sender, EventArgs e)
	{
		await db.RemoveAllSettings(CancellationToken.None);
		var settings = new Models.Settings
		{
			FontSize = 16,
			FontFamily = "Times New Roman",
			BackgroundColor = "#FFFFFF",
			TextColor = "#000000"
		};
		await db.SaveSettings(settings, CancellationToken.None);
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
}
