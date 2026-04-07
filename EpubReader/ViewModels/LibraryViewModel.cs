using System.Collections.ObjectModel;
using CommunityToolkit.Maui.Core;
using CommunityToolkit.Maui.Extensions;

namespace EpubReader.ViewModels;

/// <summary>
/// Represents the view model for managing a library of books, providing functionality to add, remove, and navigate books.
/// </summary>
/// <remarks>
/// The <see cref="LibraryViewModel"/> class is responsible for handling operations related to a collection of books,
/// including adding books from files, removing books, and navigating to a book's page. It interacts with services
/// for file picking, database operations, and ebook processing.
/// </remarks>
/// <remarks>
/// Initializes a new instance of the <see cref="LibraryViewModel"/> class.
/// </remarks>
public partial class LibraryViewModel(ProcessEpubFiles processEpubFiles, ILibraryStateService libraryStateService, IImportStateService importStateService, ISyncService syncService) : BaseViewModel
{
	bool isAlphabeticalSorted = true;

	Popup? popup;
	readonly PopupOptions options = new()
	{
		CanBeDismissedByTappingOutsideOfPopup = false,
	};

	/// <summary>
	/// Provides a read-only instance of the <see cref="ProcessEpubFiles"/> service.
	/// </summary>
	/// <remarks>This field retrieves the <see cref="ProcessEpubFiles"/> service from the current application's
	/// service provider. If the service cannot be resolved, an <see cref="InvalidOperationException"/> is
	/// thrown.</remarks>
	readonly ProcessEpubFiles processEpubFiles = processEpubFiles;
	readonly ILibraryStateService libraryStateService = libraryStateService;
	readonly IImportStateService importStateService = importStateService;
	readonly ISyncService syncService = syncService;

	/// <summary>
	/// Gets or sets the collection of books in the library.
	/// </summary>
	[ObservableProperty]
	public partial ObservableCollection<Book> Books { get; set; } = libraryStateService.Books;

	protected override void Dispose(bool disposing)
	{
		if (disposing)
		{
			CancellationTokenSource?.Dispose();
		}
		base.Dispose(disposing);
	}

	#region Commands

	/// <summary>
	/// Navigate to the book details page for the specified book.
	/// </summary>
	[RelayCommand]
	public async Task GotoBookDetails(Book book)
	{
		try
		{
			var navigationParams = new Dictionary<string, object>
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

	/// <summary>
	/// Removes the specified book from the library.
	/// </summary>
	/// <param name="book">The book to be removed.</param>
	[RelayCommand]
	public async Task RemoveBook(Book book)
	{
		try
		{
			Logger.Info($"Attempting to remove book: {book.Title}");
			if (!await FileService.ArePermissionsGranted())
			{
				return;
			}

			Logger.Info($"Removing book: {book.Title}");

			var directory = Path.GetDirectoryName(book.FilePath);
			Logger.Info($"Book file path: {book.FilePath}");
			if (!string.IsNullOrEmpty(directory) && Directory.Exists(directory))
			{
				Directory.Delete(directory, true);
			}

			await libraryStateService.RemoveBookAsync(book, CancellationTokenSource.Token);

			Logger.Info("Book removed from library");
		}
		catch (Exception ex)
		{
			Logger.Error($"Error removing book: {ex.Message}");
		}
	}

	/// <summary>
	/// Sorts the collection of books in alphabetical order by the first author's name.
	/// </summary>
	/// <remarks>This method orders the books based on the name of the first author in each book's author list,
	/// using a case-insensitive comparison. The sorted order is applied directly to the existing collection.</remarks>
	[RelayCommand]
	public void AlphabeticalAuthorSort()
	{
		isAlphabeticalSorted = !isAlphabeticalSorted;
		ReplaceBooks(SortByAuthor([.. Books], isAlphabeticalSorted));
	}

	/// <summary>
	/// Sorts the collection of books in alphabetical order by their titles.
	/// </summary>
	/// <remarks>This method sorts the books in a case-insensitive manner using the ordinal string comparison. After
	/// sorting, the original collection is cleared and repopulated with the sorted books.</remarks>
	[RelayCommand]
	public void AlphabeticalTitleSort()
	{
		isAlphabeticalSorted = !isAlphabeticalSorted;
		ReplaceBooks(SortByTitle([.. Books], isAlphabeticalSorted));
	}

	/// <summary>
	/// Asynchronously adds EPUB files from a selected folder to the library.
	/// </summary>
	/// <returns>A task representing the asynchronous operation.</returns>
	[RelayCommand]
	public async Task AddFolder()
	{
		if (!await FileService.ArePermissionsGranted())
		{
			return;
		}

		var folderUri = await processEpubFiles.FolderPicker.PickFolderAsync().ConfigureAwait(false);
		if (string.IsNullOrEmpty(folderUri))
		{
			Logger.Info("No folder selected");
			return;
		}

		Logger.Info($"Selected folder: {folderUri}");
		importStateService.Begin();
		await Dispatcher.DispatchAsync(() =>
		{
			popup = Application.Current?.Handler?.MauiContext?.Services.GetRequiredService<FolderDialogePage>() ?? throw new InvalidOperationException();
			popup.Closed += (s, e) =>
			{
				Logger.Info("File dialog popup closed.");
				importStateService.Complete();
			};
			LoadPopup();
		}).ConfigureAwait(false);
		var importToken = importStateService.Token;

		var epubFiles = await processEpubFiles.FolderPicker.EnumerateEpubFilesInFolderAsync(folderUri, importToken).ConfigureAwait(false);

		Logger.Info($"Found {epubFiles.Count} EPUB files in the selected folder");

		if (importToken.IsCancellationRequested)
		{
			Logger.Info("Operation cancelled by user.");
			await ClosePopupAsync();
			importStateService.Complete();
			return;
		}

		await processEpubFiles.ProcessEpubFilesAsync(epubFiles, importToken).ConfigureAwait(false);
		await ClosePopupAsync();
		importStateService.Complete();
	}

	/// <summary>
	/// Loads and displays a folder dialog popup.
	/// </summary>
	/// <remarks>This method initializes a new folder dialog page and displays it as a popup using the current
	/// shell.</remarks>
	void LoadPopup()
	{
		if (popup is null)
		{
			return;
		}

		Shell.Current.ShowPopup(popup, options);
	}

	async Task ClosePopupAsync()
	{
		if (popup is null)
		{
			return;
		}

		await Dispatcher.DispatchAsync(async () =>
		{
			await popup.CloseAsync();
			popup = null;
		});
	}

	public async Task LoadBooksAsync(CancellationToken cancellationToken = default)
	{
		await libraryStateService.InitializeAsync(cancellationToken);
	}

	public void ReplaceBooks(IEnumerable<Book> books)
	{
		Books.Clear();
		foreach (var book in books)
		{
			Books.Add(book);
		}
	}

	/// <summary>
	/// Asynchronously adds a selected EPUB book to the library.
	/// </summary>
	/// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
	/// <returns>A task representing the asynchronous operation.</returns>
	[RelayCommand]
	public async Task Add(CancellationToken cancellationToken = default)
	{
		if (!await FileService.ArePermissionsGranted())
		{
			return;
		}

		if (CancellationTokenSource.IsCancellationRequested)
		{
			CancellationTokenSource = new CancellationTokenSource();
		}
		try
		{

			var result = await processEpubFiles.PickEpubFileAsync(cancellationToken).ConfigureAwait(false);
			if (result is null)
			{
				Logger.Info("No file selected");
				return;
			}

			var ebook = await EbookService.GetListingAsync(result.FullPath).ConfigureAwait(false);
			if (ebook is null)
			{
				await ShowErrorToastAsync("Error opening book. Please select a valid EPUB file.");
				return;
			}

			if (await processEpubFiles.IsBookAlreadyInLibrary(ebook))
			{
				await ShowInfoToastAsync($"Book already exists in library: {ebook.Title}");
				return;
			}

			await processEpubFiles.SaveBookToLibraryAsync(ebook, result, cancellationToken).ConfigureAwait(false);
		}
		catch (Exception ex)
		{
			Logger.Error($"Error adding book: {ex.Message}");
			await ShowErrorToastAsync("Error adding book. Please try again.");
		}
	}
	#endregion

	[RelayCommand]
	async Task Settings(CancellationToken cancellation = default)
	{
		try
		{
			var services = Application.Current?.Handler.MauiContext?.Services ?? throw new InvalidOperationException();
			var authenticationService = services.GetRequiredService<AuthenticationService>();
			var settingsPopup = new Views.SettingsPage(new SettingsPageViewModel(authenticationService, syncService));
			var settingsOptions = new PopupOptions
			{
				CanBeDismissedByTappingOutsideOfPopup = true,
			};
			settingsPopup.Closed += async (s, e) =>
			{
				Logger.Info("Settings popup closed.");
				await libraryStateService.RefreshAsync(cancellation);
				AlphabeticalTitleSort();
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

}