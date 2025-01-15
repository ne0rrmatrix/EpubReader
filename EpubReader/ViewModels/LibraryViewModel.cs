using System.Collections.ObjectModel;
using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EpubReader.Interfaces;
using EpubReader.Models;
using EpubReader.Service;
using Font = Microsoft.Maui.Font;
using ILogger = MetroLog.ILogger;
using LoggerFactory = MetroLog.LoggerFactory;

namespace EpubReader.ViewModels;
public partial class LibraryViewModel : BaseViewModel, IDisposable
{
	readonly Task loadTask;
	readonly CancellationTokenSource? cancellationtokensource;
	static readonly ILogger logger = LoggerFactory.GetLogger(nameof(LibraryViewModel));
    static readonly string[] epub = [".epub", ".epub"];
    static readonly string[] android_epub = ["application/epub+zip", ".epub"];
	bool disposedValue;

	[ObservableProperty]
    public partial ObservableCollection<Book> Books { get; set; } = new();
   
	public LibraryViewModel()
    {
		cancellationtokensource = new CancellationTokenSource();
		loadTask = LoadBooks(cancellationtokensource.Token);
		if(loadTask.IsFaulted)
		{
			logger.Error("Error loading books");
		}
	}

    async Task LoadBooks(CancellationToken cancellationToken = default)
    {
        List<FileData> temp = await db.GetAllFileData(cancellationToken);
		var bookData = await db.GetAllBooks(cancellationToken) ?? [];
		var books = new ObservableCollection<Book>();
        foreach (var item in temp)
        {
            var book = EbookService.OpenEbook(item.FileName);
			var savedBook = bookData.Find(x => x.Title == book.Title);
			if(savedBook is null)
			{
				logger.Error("Book not found in database");
			}
			book.CurrentChapter = savedBook?.CurrentChapter ?? 0;
			book.CurrentPage = savedBook?.CurrentPage ?? 0;
			books.Add(book);
        }
        Dispatcher.Dispatch(() => { Books = books; OnPropertyChanged(nameof(Books)); });
    }

    [RelayCommand]
    public static async Task GotoBookPage(Book Book)
    {
		if(Book is null)
		{
			logger.Error("Book is null");
			return;
		}
			var navigationParams = new Dictionary<string, object>
        {
            { "Book", Book }
        };
        await Shell.Current.GoToAsync($"//BookPage", navigationParams).WaitAsync(CancellationToken.None).ConfigureAwait(false);
    }

    [RelayCommand]
    async Task Add(CancellationToken cancellationToken = default)
    {
        var customFileType = new FilePickerFileType(
                new Dictionary<DevicePlatform, IEnumerable<string>>
                {
                    { DevicePlatform.iOS, epub },
                    { DevicePlatform.Android, android_epub },
                    { DevicePlatform.WinUI, epub },
                    { DevicePlatform.Tizen, epub },
                    { DevicePlatform.macOS, epub },
                });

        var result = await PickAndShow(new PickOptions
        {
            FileTypes = customFileType,
            PickerTitle = "Please select a epub book"
        }).ConfigureAwait(false);
		
		if (result is not null)
        {
			var currentFileData = await db.GetAllFileData(cancellationToken);
			var exists = currentFileData.Any(x => x.FileName == FileService.GetFileName(result.FileName));
			if(exists)
			{
				await ShowSnackBar("Book already exists in library", "OK", cancellationToken).ConfigureAwait(false);
				logger.Info("Book already exists in library");
				return;
			}
			var filePath = await FileService.SaveFile(result);
            // Open the epub file
            var ebook = EbookService.OpenEbook(filePath);
			
            FileData fileData = new()
            {
                Title = ebook.Title,
                FileName = filePath,
            };
			await db.SaveBookData(ebook, cancellationToken);
			await db.SaveFileData(fileData, cancellationToken);
			Books.Add(ebook);
			return;
        }
        logger.Error("Error saving book");
    }

	static async Task ShowSnackBar(string text, string actionButtonText, CancellationToken cancellationToken = default)
	{
		var snackbarOptions = new SnackbarOptions
		{
			BackgroundColor = Colors.Red,
			TextColor = Colors.White,
			ActionButtonTextColor = Colors.Yellow,
			CornerRadius = new CornerRadius(10),
			Font = Font.SystemFontOfSize(14),
			ActionButtonFont = Font.SystemFontOfSize(14),
			CharacterSpacing = 0.5
		};

		TimeSpan duration = TimeSpan.FromSeconds(3);

		var snackbar = Snackbar.Make(text, null, actionButtonText, duration, snackbarOptions);

		await snackbar.Show(cancellationToken).ConfigureAwait(false);
	}
    [RelayCommand]
    async Task RemoveBook(Book book, CancellationToken cancellationToken = default)
    {
        if (book is not null)
        {
            logger.Info("Removing book");
            FileService.DeleteFile(book.FilePath);
			var fileData = await db.GetAllFileData(cancellationToken);
			var item = fileData.FirstOrDefault(x => x.FileName == book.FilePath);
			await db.RemoveFileData(item, cancellationToken);
			await db.RemoveBook(book, cancellationToken);
			Books.Remove(book);
            logger.Info("Book removed from library.");
            OnPropertyChanged(nameof(Books));
        }
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



	protected virtual void Dispose(bool disposing)
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
	}

	public void Dispose()
	{
		Dispose(disposing: true);
		GC.SuppressFinalize(this);
	}
}
