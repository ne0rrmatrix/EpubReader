using CommunityToolkit.Maui.Core;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace EpubReader.ViewModels;

public partial class BookDetailsViewModel : BaseViewModel, IQueryAttributable
{
    readonly ISyncService syncService = Application.Current?.Handler.MauiContext?.Services.GetRequiredService<ISyncService>() ?? throw new InvalidOperationException();

    [ObservableProperty]
    public partial string CoverImage { get; set; } = "";

    public async void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("Book", out var bookObj) && bookObj is Book b)
        {
            Book = b;
			CoverImage = b.CoverImagePath;
			var existingBook = await db.GetBook(Book);
			ArgumentNullException.ThrowIfNull(existingBook);
			existingBook.SyncId = await BookIdentityService.ComputeSyncIdAsync(existingBook, CancellationTokenSource.Token);
			await db.SaveBookData(existingBook, CancellationTokenSource.Token);

			Book = await EbookService.OpenEbookAsync(Book.FilePath)
				?? throw new InvalidOperationException("Error opening ebook");

			// Ensure the Book has the existing DB Id immediately so any background saves
			// (progress, webview helper) don't accidentally insert an incomplete record.
			Book.Id = existingBook.Id;
			Book.SyncId = existingBook.SyncId;
            Book.CoverImagePath = b.CoverImagePath;
           
            
			// Restore local reading position from the database so the book opens at the user's local position.
			Book.CurrentChapter = existingBook.CurrentChapter;
			Book.CurrentPage = existingBook.CurrentPage;
			// Populate sync cache / cloud progress if available, but do NOT overwrite the Book's local position here.
			await RestoreProgressAsync(existingBook);

			StreamExtensions.Instance?.SetBook(Book);
        }
    }

    [RelayCommand]
    public async Task ReadAsync()
    {
        	try
		{
			var navigationParams = new Dictionary<string, object>
			{
				{ "Book", Book }
			};

			await Shell.Current.GoToAsync("BookPage", navigationParams);
		}
		catch (Exception ex)
		{
			Logger.Error($"Error navigating to book page: {ex.Message}");
			await ShowErrorToastAsync("Error opening book. Please try again.");
		}
    }

    async Task RestoreProgressAsync(Book existingBook)
	{
		var token = CancellationTokenSource.Token;
		var syncId = await BookIdentityService.ComputeSyncIdAsync(existingBook, token);

		// Get progress (sync service checks local cache first, then cloud)
		var progress = await syncService.GetProgressAsync(syncId, token);

		// Backfill from legacy Book fields if no progress record yet
		if (progress is null && (existingBook.CurrentChapter > 0 || existingBook.CurrentPage > 0))
		{
			progress = new ReadingProgress
			{
				BookId = syncId,
				CurrentChapter = existingBook.CurrentChapter,
				CurrentPage = existingBook.CurrentPage,
				LastUpdated = DateTimeOffset.UtcNow.ToString("o"),
				DeviceId = string.Empty,
				DeviceName = string.Empty,
				IsSynced = false
			};
			await syncService.SaveProgressAsync(progress, token);
		}

		// Do not apply the remote progress to the opened Book here. BookPage will compare and prompt the user
		// whether to move to the remote position. Leaving this method responsible only for ensuring the
		// sync cache is populated and legacy backfill occurs.
	}
}
