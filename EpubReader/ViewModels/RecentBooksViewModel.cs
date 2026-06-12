using System.Collections.ObjectModel;
using CommunityToolkit.Maui.Core;
using CommunityToolkit.Maui.Extensions;

namespace EpubReader.ViewModels;

/// <summary>
/// Represents the view model for displaying recently opened books sorted by last opened date.
/// </summary>
public partial class RecentBooksViewModel : BaseViewModel
{
	const int maxRecentBooks = 20;

	/// <summary>
	/// Gets or sets the collection of recently opened books.
	/// </summary>
	[ObservableProperty]
	public partial ObservableCollection<Book> RecentBooks { get; set; }

	readonly ISyncService syncService = Application.Current?.Handler.MauiContext?.Services.GetRequiredService<ISyncService>() ?? throw new InvalidOperationException();

	/// <summary>
	/// Gets or sets a value indicating whether the recent books collection is empty.
	/// </summary>
	[ObservableProperty]
	public partial bool IsEmpty { get; set; }

	/// <summary>
	/// Initializes a new instance of the <see cref="RecentBooksViewModel"/> class.
	/// </summary>
	public RecentBooksViewModel()
	{
		RecentBooks = [];
		IsEmpty = true;
	}

	#region Commands

	/// <summary>
	/// Load the 20 most recently opened books sorted by LastOpenedDate descending.
	/// </summary>
	[RelayCommand]
	public async Task LoadRecentBooks(CancellationToken cancellationToken = default)
	{
		await LoadRecentBooksAsync(cancellationToken);
	}

	/// <summary>
	/// Navigate to the book details page for the specified book.
	/// </summary>
	[RelayCommand]
	public async Task GotoBookDetailsAsync(Book book, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		try
		{
			Dictionary<string, object> navigationParams = new()
			{
				{ "Book", book }
			};
			await Shell.Current.GoToAsync("BookDetailsPage", navigationParams);
		}
		catch (Exception ex)
		{
			Logger.Error($"Error navigating to book details: {ex.Message}");
		}
	}

	#endregion

	#region Private Methods

	async Task LoadRecentBooksAsync(CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		try
		{
			List<Book> allBooks = await db.GetAllBooks(cancellationToken);

			// Filter books with LastOpenedDate, sort by most recent descending, limit to 20
			List<Book> recentBooks = allBooks
				.Where(b => b.LastOpenedDate.HasValue)
				.OrderByDescending(b => b.LastOpenedDate)
				.Take(maxRecentBooks)
				.ToList();

			RecentBooks.Clear();
			foreach (Book book in recentBooks)
			{
				RecentBooks.Add(book);
			}

			IsEmpty = RecentBooks.Count == 0;
			Logger.Info($"Loaded {RecentBooks.Count} recent books");
		}
		catch (Exception ex)
		{
			Logger.Error($"Error loading recent books: {ex.Message}");
		}
	}

	[RelayCommand]
	async Task Settings(CancellationToken cancellation = default)
	{
		try
		{
			IServiceProvider services = Application.Current?.Handler.MauiContext?.Services ?? throw new InvalidOperationException();
			SettingsPage settingsPopup = new(new SettingsPageViewModel(syncService));
			PopupOptions settingsOptions = new()
			{
				CanBeDismissedByTappingOutsideOfPopup = true,
			};
			settingsPopup.Closed += async (s, e) =>
			{
				Logger.Info("Settings popup closed.");
				await LoadRecentBooksAsync(cancellation);
			};
			IPopupResult<bool> result = await Shell.Current.ShowPopupAsync<bool>(settingsPopup, settingsOptions, cancellation);

			if (result.WasDismissedByTappingOutsideOfPopup)
			{
				Logger.Info("Settings popup dismissed by tapping outside.");
			}
		}
		catch (Exception ex)
		{
			Logger.Error($"Error showing settings popup: {ex.Message}");
		}
	}

	#endregion
}