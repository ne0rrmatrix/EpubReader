using System.Collections.ObjectModel;
using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EpubReader.Models;
using EpubReader.Service;
using Font = Microsoft.Maui.Font;
using ILogger = MetroLog.ILogger;
using LoggerFactory = MetroLog.LoggerFactory;

namespace EpubReader.ViewModels;
public partial class LibraryViewModel : BaseViewModel
{
	readonly Task loadTask;
	readonly CancellationTokenSource? cancellationtokensource;
	static readonly ILogger logger = LoggerFactory.GetLogger(nameof(LibraryViewModel));
    static readonly string[] epub = [".epub", ".epub"];
    static readonly string[] android_epub = ["application/epub+zip", ".epub"];
	readonly FilePickerFileType customFileType = new(
			   new Dictionary<DevicePlatform, IEnumerable<string>>
			   {
					{ DevicePlatform.iOS, epub },
					{ DevicePlatform.Android, android_epub },
					{ DevicePlatform.WinUI, epub },
					{ DevicePlatform.Tizen, epub },
					{ DevicePlatform.macOS, epub },
			   });
	bool disposedValue;

	[ObservableProperty]
    public partial ObservableCollection<Book> Books { get; set; } = new();
   
	public LibraryViewModel()
    {
		cancellationtokensource = new CancellationTokenSource();
		loadTask = LoadBooks(cancellationtokensource.Token);
		
		if (loadTask.IsFaulted)
		{
			logger.Error("Error loading books");
		}
	}

	async Task LoadBooks(CancellationToken cancellationToken = default)
	{
		if(Books.Count > 0)
		{
			Books.Clear();
		}
		var bookData = await db.GetAllBooks(cancellationToken).ConfigureAwait(false) ?? [];
		foreach (var item in bookData)
		{
			var ebook = EbookService.GetListing(item.FilePath) ?? throw new InvalidOperationException("Error opening ebook");
			ebook.CurrentChapter = item.CurrentChapter;
			Books.Add(ebook);
		}
	}

    [RelayCommand]
    public static async Task GotoBookPage(Book book)
    {
		if(book is null)
		{
			logger.Info("Book is null");
			return;
		}

		var Book = EbookService.OpenEbook(book.FilePath) ?? throw new InvalidOperationException();
		var navigationParams = new Dictionary<string, object>
        {
            { "Book", Book }
        };
        await Shell.Current.GoToAsync($"//BookPage", navigationParams);
    }

    [RelayCommand]
    async Task Add(CancellationToken cancellationToken = default)
    {
		string message = string.Empty;
        var result = await PickAndShow(new PickOptions
        {
            FileTypes = customFileType,
            PickerTitle = "Please select a epub book"
        }).ConfigureAwait(false);
		if(result is null)
		{
			logger.Info("No file selected");
			return;
		}
		var bookData = await db.GetAllBooks(cancellationToken).ConfigureAwait(false) ?? [];
		var ebook = EbookService.GetListing(result.FullPath);
		if (ebook is null)
		{
			message = "Error opening Book.";
			await Dispatcher.DispatchAsync(async () => await Toast.Make(message, ToastDuration.Short, 12).Show(cancellationToken));
			logger.Info(message);
			return;
		}

		if (bookData.Any(x => x.Title == ebook.Title))
		{
			message = "Book already exists in library";
			await Dispatcher.DispatchAsync(async () => await Toast.Make(message, ToastDuration.Short, 12).Show(cancellationToken));
			logger.Info(message);
			return;
		}

		ebook.FilePath = FileService.GetFileName(result.FileName);
		await FileService.SaveFile(result).ConfigureAwait(false);
		await db.SaveBookData(ebook, cancellationToken).ConfigureAwait(false);
		Books.Add(ebook);

		message = "Book added to library";
		await Dispatcher.DispatchAsync(async () => await Toast.Make(message, ToastDuration.Short, 12).Show(cancellationToken));
		logger.Info(message);
	}

    [RelayCommand]
    async Task RemoveBook(Book book, CancellationToken cancellationToken = default)
    {
		logger.Info("Removing book");
		FileService.DeleteFile(book.FilePath);
		await db.RemoveBook(book, cancellationToken).ConfigureAwait(false);
		Books.Remove(book);
		logger.Info("Book removed from library.");
		OnPropertyChanged(nameof(Books));
	}

	public static async Task<FileResult?> PickAndShow(PickOptions options)
    {
        try
        {
            return await FilePicker.PickAsync(options).WaitAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            logger.Error($"Exception choosing file: {ex.Message}");
            return null;
        }
    }

	protected override void Dispose(bool disposing)
	{
		if (!disposedValue)
		{
			if (disposing)
			{
				cancellationtokensource?.Dispose();
				loadTask.Dispose();
			}
			disposedValue = true;
		}
		base.Dispose(disposing);
	}
}
