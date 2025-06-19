using Application = Microsoft.Maui.Controls.Application;

namespace EpubReader;

public partial class App : Application
{
    readonly AppShell appShell;
	Window? appWindow;
	public readonly TitleBar TitleBar;
	public App(AppShell appShell)
    {
        InitializeComponent();
		var backgroundColor = Colors.White;
		var foregroundColor = Colors.Black;
		if (PlatformAppTheme == AppTheme.Dark)
		{
			backgroundColor = Colors.Black;
			foregroundColor = Colors.White;
		}
		TitleBar = new TitleBar
		{
			BackgroundColor = backgroundColor,
			ForegroundColor = foregroundColor,
			Title = "EpubReader",
		};
		this.appShell = appShell;
	}
	protected override Window CreateWindow(IActivationState? activationState)
	{
		RequestedThemeChanged += Current_RequestedThemeChanged;
		appWindow = new Window(appShell)
		{
			TitleBar = TitleBar,
		};
		return appWindow;
	}

	void Current_RequestedThemeChanged(object? sender, AppThemeChangedEventArgs e)
	{
		if (appWindow?.TitleBar is null)
		{
			return;
		}
		if (e.RequestedTheme == AppTheme.Dark)
		{
			
			TitleBar.BackgroundColor = Colors.Black;
			TitleBar.ForegroundColor = Colors.White;
		}
		else
		{
			TitleBar.BackgroundColor = Colors.White;
			TitleBar.ForegroundColor = Colors.Black;
		}
	}
}
