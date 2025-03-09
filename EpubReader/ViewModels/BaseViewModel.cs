using CommunityToolkit.Maui.Core;
using CommunityToolkit.Maui.Core.Platform;
using CommunityToolkit.Mvvm.ComponentModel;
using EpubReader.Interfaces;
using EpubReader.Models;

namespace EpubReader.ViewModels;

public partial class BaseViewModel : ObservableObject, IDisposable
{
	public readonly IDispatcher Dispatcher = Application.Current?.Dispatcher ?? throw new InvalidOperationException();
	bool disposedValue;

	public IDb db { get; set; } = Application.Current?.Handler.MauiContext?.Services.GetRequiredService<IDb>() ?? throw new InvalidOperationException();
	[ObservableProperty]
	public partial Book Book { get; set; }

	public BaseViewModel()
	{
		Book = new();
#if ANDROID
		ArgumentNullException.ThrowIfNull(Application.Current);
		StatusBar.SetStyle(StatusBarStyle.LightContent);
		if (Application.Current.RequestedTheme == AppTheme.Dark)
		{
			StatusBar.SetColor(Color.FromArgb("#121212"));
		}
		else
		{
			StatusBar.SetColor(Color.FromArgb("#3E8EED"));

		}
		Application.Current.RequestedThemeChanged += Current_RequestedThemeChanged;
#endif
	}

#if ANDROID
	static void Current_RequestedThemeChanged(object? sender, AppThemeChangedEventArgs e)
	{
		StatusBar.SetStyle(StatusBarStyle.LightContent);
		if (e.RequestedTheme == AppTheme.Dark)
		{
			StatusBar.SetColor(Color.FromArgb("#121212"));
		}
		else
		{
			StatusBar.SetColor(Color.FromArgb("#3E8EED"));
		}
	}
#endif
	protected virtual void Dispose(bool disposing)
	{
		if (!disposedValue)
		{
			if (disposing)
			{
#if ANDROID
#pragma warning disable S1066 // Mergeable "if" statements should be combined
				if (Application.Current is not null)
				{
					Application.Current.RequestedThemeChanged -= Current_RequestedThemeChanged;
				}
#pragma warning restore S1066 // Mergeable "if" statements should be combined
#endif
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
