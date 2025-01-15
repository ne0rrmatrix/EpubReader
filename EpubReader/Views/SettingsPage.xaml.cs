using CommunityToolkit.Maui.Views;
using CommunityToolkit.Mvvm.Messaging;
using EpubReader.Interfaces;
using EpubReader.Messages;

namespace EpubReader.Views;

public partial class SettingsPage : Popup, IDisposable
{
	int fontSize = 0;
	readonly CancellationTokenSource cancellationTokenSource;
	readonly Task loadTask;
	bool disposedValue;
	IDb db { get; set; } = Application.Current?.Handler.MauiContext?.Services.GetRequiredService<IDb>() ?? throw new InvalidOperationException();
	public SettingsPage()
    {
        InitializeComponent();
        BindingContext = this;
		cancellationTokenSource = new CancellationTokenSource();
		loadTask = LoadSettings(cancellationTokenSource.Token);
		if (loadTask.IsFaulted)
		{
			System.Diagnostics.Trace.TraceInformation("Error loading settings");
		}
	}
	async Task LoadSettings(CancellationToken cancellationToken = default)
	{
		var settings = await db.GetSettings(cancellationToken);
		Dispatcher.Dispatch(() => SystemThemeSwitch.IsToggled = settings.IsSystemMode);
	}
	async void OnApplyColorChanged(object sender, EventArgs e)
    {
		string backgroundColorArgb;
        string textColorArgb;
		var selectedTheme = ThemePicker.SelectedItem.ToString();
		var settings = await db.GetSettings(cancellationTokenSource.Token);

		switch (selectedTheme)
        {
            case "Dark":
				backgroundColorArgb = "#121212";
				textColorArgb = "#E1E1E1";
				break;

            case "Sepia":
                backgroundColorArgb = "#f4ecd8"; // Sepia background
                textColorArgb = "#5b4636"; // Dark brown text
				break;

            case "Night Mode":
                backgroundColorArgb = "#000000"; // Dark background
                textColorArgb = "#ffffff"; // Light text
                break;

            case "Daylight":
                backgroundColorArgb = "#ffffff"; // Bright white background
                textColorArgb = "#000000"; // Dark text
                break;

            case "Forest":
                backgroundColorArgb = "#e0f2e9"; // Greenish background
                textColorArgb = "#2e4d38"; // Dark green text
                break;

            case "Ocean":
                backgroundColorArgb = "#e0f7fa"; // Light blue background
                textColorArgb = "#01579b"; // Navy text
                break;

            case "Sand":
                backgroundColorArgb = "#f5deb3"; // Sandy background
                textColorArgb = "#000000"; // Dark text
                break;

            case "Charcoal":
                backgroundColorArgb = "#36454f"; // Dark gray background
                textColorArgb = "#dcdcdc"; // Light gray text
                break;

            case "Vintage":
                backgroundColorArgb = "#f5f5dc"; // Yellowed paper background
                textColorArgb = "#000000"; // Dark text
                break;

            default:
				// Default color scheme if none match. Is the a default light theme.
				backgroundColorArgb = "#FFFBF5"; // Light gray background
				textColorArgb = "#2B2B2B"; // Black text
				break;
        }

        if (string.IsNullOrEmpty(backgroundColorArgb) || string.IsNullOrEmpty(textColorArgb))
        {
            return;
        }
		settings.BackgroundColor = backgroundColorArgb;
		settings.TextColor = textColorArgb;
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
		var selectedTheme = FontPicker.SelectedItem.ToString();

        var font = $"'{selectedTheme}'";
		settings.FontFamily = font;
		System.Diagnostics.Trace.TraceInformation($"Font: {font}");
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
		if (Application.Current.RequestedTheme == AppTheme.Dark)
		{
			settings.BackgroundColor = "#121212";
			settings.TextColor = "#E1E1E1";
		}
		else
		{
			settings.BackgroundColor = "#FFFBF5";
			settings.TextColor = "#2B2B2B";
		}
		await db.SaveSettings(settings);
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

	async void Button_Clicked(object sender, EventArgs e)
	{
		await db.RemoveAllSettings(CancellationToken.None);
		var settings = new Models.Settings();
		await db.SaveSettings(settings, CancellationToken.None);
		WeakReferenceMessenger.Default.Send(new SettingsMessage(true));
	}
}
