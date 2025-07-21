using CommunityToolkit.Mvvm.ComponentModel;
using EpubReader.Interfaces;
using EpubReader.Models;
using MetroLog;
using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;

#if ANDROID
using CommunityToolkit.Maui.Core.Platform;
#endif

namespace EpubReader.ViewModels;

/// <summary>
/// Serves as a base class for view models, providing common functionality such as access to the application's
/// dispatcher and database service, as well as handling theme changes on Android.
/// </summary>
/// <remarks>This class implements <see cref="IDisposable"/> to manage resources, particularly for handling theme
/// change events on Android. It provides properties for accessing the application's dispatcher and database service,
/// and includes a property for managing the current book instance.</remarks>
public partial class BaseViewModel : ObservableObject, IDisposable
{
	/// <summary>
	/// Gets the logger instance associated with the <see cref="BaseViewModel"/>.
	/// </summary>
	/// <remarks>This logger is used for logging messages related to the operations and state of the <see
	/// cref="BaseViewModel"/>. It is initialized using the <see cref="LoggerFactory"/> with the name of the view
	/// model.</remarks>
	public readonly ILogger Logger = LoggerFactory.GetLogger(nameof(BaseViewModel));
	/// <summary>
	/// Gets the dispatcher associated with the current application.
	/// </summary>
	public readonly IDispatcher Dispatcher = Application.Current?.Dispatcher ?? throw new InvalidOperationException();
	bool disposedValue;

	/// <summary>
	/// Gets or sets the database service used by the application.
	/// </summary>
	public IDb db { get; set; } = Application.Current?.Handler.MauiContext?.Services.GetRequiredService<IDb>() ?? throw new InvalidOperationException();

	/// <summary>
	/// Gets or sets the current book instance.
	/// </summary>
	[ObservableProperty]
	public partial Book Book { get; set; }

	public BaseViewModel()
	{
		Book = new();
#if ANDROID
		ArgumentNullException.ThrowIfNull(Application.Current);
		StatusBar.SetStyle(StatusBarStyle.LightContent);
		Current_RequestedThemeChanged(Application.Current, new AppThemeChangedEventArgs(Application.Current.RequestedTheme));
		Application.Current.RequestedThemeChanged += Current_RequestedThemeChanged;
#endif
	}

#if ANDROID
	static void Current_RequestedThemeChanged(object? sender, AppThemeChangedEventArgs e)
	{
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

	#region Toast Helper Methods

	/// <summary>
	/// Shows an informational toast message.
	/// </summary>
	/// <param name="message">The message to display.</param>
	/// <param name="cancellationToken">Cancellation token for the operation.</param>
	/// <returns>A task representing the asynchronous operation.</returns>
	public async Task ShowInfoToastAsync(string message, CancellationToken cancellationToken = default)
	{
		await Dispatcher.DispatchAsync(async () =>
			await Toast.Make(message, ToastDuration.Short, 12).Show(cancellationToken));
		Logger.Info(message);
	}

	/// <summary>
	/// Shows an error toast message.
	/// </summary>
	/// <param name="message">The message to display.</param>
	/// <param name="cancellationToken">Cancellation token for the operation.</param>
	/// <returns>A task representing the asynchronous operation.</returns>
	public async Task ShowErrorToastAsync(string message, CancellationToken cancellationToken = default)
	{
		await Dispatcher.DispatchAsync(async () =>
			await Toast.Make(message, ToastDuration.Short, 12).Show(cancellationToken));
		Logger.Error(message);
	}

	#endregion

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
