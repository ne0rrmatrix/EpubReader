using CommunityToolkit.Maui.Views;
using CommunityToolkit.Mvvm.Messaging;
using EpubReader.Interfaces;
using EpubReader.Messages;
using EpubReader.Service;
using MetroLog;
using Syncfusion.Maui.Toolkit.Themes;

namespace EpubReader.Views;

public partial class SettingsPage : Popup, IDisposable
{
	int fontSize = 0;
	readonly CancellationTokenSource cancellationTokenSource;
	readonly Task loadTask;
	bool disposedValue;
	readonly List<EbookFonts> fonts = [
		new EbookFonts { FontFamily = "Arial" },
		new EbookFonts { FontFamily = "Times New Roman" },
		new EbookFonts { FontFamily = "Verdana" },
		new EbookFonts { FontFamily = "Courier New" },
		new EbookFonts { FontFamily = "Georgia" },
		new EbookFonts { FontFamily = "Tahoma" },
		new EbookFonts { FontFamily = "Trebuchet MS" },
		new EbookFonts { FontFamily = "Comic Sans MS" },
		new EbookFonts { FontFamily = "Lucida Sans Unicode" },
		new EbookFonts { FontFamily = "Helvetica" }
	];
	public List<EbookFonts> Fonts => fonts;
	readonly List<EbookColor> colors;
	public List<EbookColor> Colors => colors;
	IDb db { get; set; } = Application.Current?.Handler.MauiContext?.Services.GetRequiredService<IDb>() ?? throw new InvalidOperationException();
	static readonly ILogger logger = LoggerFactory.GetLogger(nameof(SettingsPage));
	public SettingsPage()
	{
        InitializeComponent();
		colors = [.. Enum.GetValues<EbookColor>()];
		BindingContext = this;
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
		if (Dispatcher.IsDispatchRequired)
		{
			Dispatcher.Dispatch(() => SystemThemeSwitch.IsToggled = settings.IsSystemMode);
		}
		else
		{
			SystemThemeSwitch.IsToggled = settings.IsSystemMode;
		}
	}

	async void OnApplyColorChanged(object sender, EventArgs e)
    {
		var selectedTheme = ThemePicker.SelectedItem;
		var settings = await db.GetSettings(cancellationTokenSource.Token);

		(settings.BackgroundColor, settings.TextColor, _) = selectedTheme switch
		{
			EbookColor.Dark => EbookColorScheme.GetColorSchemeString(EbookColor.Dark),
			EbookColor.Sepia => EbookColorScheme.GetColorSchemeString(EbookColor.Sepia),
			EbookColor.Forest => EbookColorScheme.GetColorSchemeString(EbookColor.Forest),
			EbookColor.Ocean => EbookColorScheme.GetColorSchemeString(EbookColor.Ocean),
			EbookColor.Sand => EbookColorScheme.GetColorSchemeString(EbookColor.Sand),
			EbookColor.Charcoal => EbookColorScheme.GetColorSchemeString(EbookColor.Charcoal),
			EbookColor.Vintage => EbookColorScheme.GetColorSchemeString(EbookColor.Vintage),
			_ => EbookColorScheme.GetColorSchemeString(EbookColor.Default),
		};
		if (string.IsNullOrEmpty(settings.BackgroundColor) || string.IsNullOrEmpty(settings.TextColor))
        {
            return;
        }

		await db.SaveSettings(settings, CancellationToken.None);
		WeakReferenceMessenger.Default.Send(new SettingsMessage(true));
	}

    async void OnFontSizeSliderChanged(object sender, ValueChangedEventArgs e)
    {
		var settings = await db.GetSettings(CancellationToken.None);
		if (fontSize == (int)e.NewValue)
        {
            return;
        }
        fontSize = (int)e.NewValue;
		settings.FontSize = fontSize;
		await db.SaveSettings(settings);
		WeakReferenceMessenger.Default.Send(new SettingsMessage(true));
	}

    async void OnFontChange(object sender, EventArgs e)
    {
		var settings = await db.GetSettings(CancellationToken.None);
		var selectedTheme = FontPicker.SelectedItem;
		if(selectedTheme is not EbookFonts font)
		{
			logger.Info("Selected font is null");
			return;
		}

		settings.FontFamily = font.FontFamily;
		logger.Info($"Chaging Font to: {font.FontFamily}");
		await db.SaveSettings(settings);
		WeakReferenceMessenger.Default.Send(new SettingsMessage(true));
	}

    async void OnSystemThemeSwitchToggled(object sender, ToggledEventArgs e)
    {
		var settings = await db.GetSettings(CancellationToken.None);
		settings.IsSystemMode = e.Value;
		await db.SaveSettings(settings);
		if (!settings.IsSystemMode)
		{
			return;
		}
		ArgumentNullException.ThrowIfNull(Application.Current);
		ICollection<ResourceDictionary> mergedDictionaries = Application.Current.Resources.MergedDictionaries;
		if (mergedDictionaries != null)
		{
			var theme = mergedDictionaries.OfType<SyncfusionThemeResourceDictionary>().FirstOrDefault();
			if (theme != null)
			{
				if (Application.Current?.RequestedTheme == AppTheme.Dark)
				{
					var (BackgroundColor, TextColor, _) = EbookColorScheme.GetColorSchemeString(EbookColor.Dark);
					settings.BackgroundColor = BackgroundColor;
					settings.TextColor = TextColor;
					theme.VisualTheme = SfVisuals.MaterialLight;
				}
				else
				{
					var (BackgroundColor, TextColor, _) = EbookColorScheme.GetColorSchemeString(EbookColor.Default);
					settings.BackgroundColor = BackgroundColor;
					settings.TextColor = TextColor;
					theme.VisualTheme = SfVisuals.MaterialDark;
				}
			}
		}
		
		await db.SaveSettings(settings);
		logger.Info("System theme changed");
		WeakReferenceMessenger.Default.Send(new SettingsMessage(true));
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

	async void RemoveAllSettings(object sender, EventArgs e)
	{
		await db.RemoveAllSettings(CancellationToken.None);
		var settings = new Models.Settings();
		await db.SaveSettings(settings, CancellationToken.None);
		WeakReferenceMessenger.Default.Send(new SettingsMessage(true));
		logger.Info("Settings removed");
	}
}
