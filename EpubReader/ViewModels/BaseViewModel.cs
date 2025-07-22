using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;
using CommunityToolkit.Mvvm.ComponentModel;
using EpubReader.Interfaces;
using EpubReader.Models;
using MetroLog;

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
	/// Gets or sets the <see cref="CancellationTokenSource"/> used to manage cancellation tokens.
	/// </summary>

	[ObservableProperty]
	public partial CancellationTokenSource CancellationTokenSource { get; set; } = new CancellationTokenSource();


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

	/// <summary>
	/// Sorts a list of books by their author names.
	/// </summary>
	/// <remarks>The sorting is case-insensitive and uses ordinal comparison.</remarks>
	/// <param name="books">The list of books to be sorted. Cannot be null.</param>
	/// <param name="ascending">A boolean value indicating the sort order.  true to sort in ascending order (A-Z);  false to sort in descending
	/// order (Z-A).</param>
	/// <returns>A new list of books sorted by author names in the specified order.</returns>
	public List<Book> SortByAuthor(List<Book> books, bool ascending = true)
	{
		if (ascending)
		{
			Logger.Info("Sorting books by author (A-Z)");
			return [.. books.OrderBy(b => b.Author, StringComparer.OrdinalIgnoreCase)];
		}
		Logger.Info("Sorting books by author (Z-A)");
		return [.. books.OrderByDescending(b => b.Author, StringComparer.OrdinalIgnoreCase)];
	}

	/// <summary>
	/// Sorts a list of books by their titles in either ascending or descending order.
	/// </summary>
	/// <param name="books">The list of books to be sorted. Cannot be null.</param>
	/// <param name="ascending">A boolean value indicating the sort order.  <see langword="true"/> to sort titles in ascending order (A-Z);  <see
	/// langword="false"/> to sort in descending order (Z-A).</param>
	/// <returns>A new list of books sorted by title according to the specified order.</returns>
	public List<Book> SortByTitle(List<Book> books, bool ascending)
	{
		if (ascending)
		{
			Logger.Info("Sorting books by title (A-Z)");
			return [.. books.OrderBy(b => b.Title, StringComparer.OrdinalIgnoreCase)];
		}
		Logger.Info("Sorting books by title (Z-A)");
		return [.. books.OrderByDescending(b => b.Title, StringComparer.OrdinalIgnoreCase)];
	}

	

	#region Toast Helper Methods

	/// <summary>
	/// Shows an informational toast message.
	/// </summary>
	/// <param name="message">The message to display.</param>
	/// <returns>A task representing the asynchronous operation.</returns>
	public async Task ShowInfoToastAsync(string message)
	{
		await Dispatcher.DispatchAsync(async () =>
			await Toast.Make(message, ToastDuration.Short, 12).Show());
		Logger.Info(message);
	}

	/// <summary>
	/// Shows an error toast message.
	/// </summary>
	/// <param name="message">The message to display.</param>
	/// <returns>A task representing the asynchronous operation.</returns>
	public async Task ShowErrorToastAsync(string message)
	{
		await Dispatcher.DispatchAsync(async () =>
			await Toast.Make(message, ToastDuration.Short, 12).Show());
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
