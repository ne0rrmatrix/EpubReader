using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EpubReader.Interfaces;
using EpubReader.Models;
using EpubReader.Service;
using ILogger = MetroLog.ILogger;
using LoggerFactory = MetroLog.LoggerFactory;

namespace EpubReader.ViewModels;
public partial class LibraryViewModel : BaseViewModel, IDisposable
{
	readonly Task loadTask;
	readonly CancellationTokenSource? cancellationtokensource;
	bool disposedValue;
	static readonly ILogger logger = LoggerFactory.GetLogger(nameof(LibraryViewModel));
    static readonly string[] epub = [".epub", ".epub"];
    static readonly string[] android_epub = ["application/epub+zip", ".epub"];
    [ObservableProperty]
    public partial ObservableCollection<Book> Books { get; set; } = new();
   
    IDb db { get; set; } = Application.Current?.Handler.MauiContext?.Services.GetRequiredService<IDb>() ?? throw new InvalidOperationException();
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
        List<FileData> temp = await db.GetFileData(cancellationToken);
        var books = new ObservableCollection<Book>();
        foreach (var item in temp)
        {
            var book = EbookService.OpenEbook(item.FileName);
            books.Add(book);
        }
        Dispatcher.Dispatch(() => { Books = books; OnPropertyChanged(nameof(Books)); });
    }

    [RelayCommand]
    public static async Task GotoBookPage(Book Book)
    {
        var navigationParams = new Dictionary<string, object>
        {
            { "Book", Book }
        };
        await Shell.Current.GoToAsync($"//BookPage", navigationParams).WaitAsync(CancellationToken.None).ConfigureAwait(false);
    }

    [RelayCommand]
    public async Task Add(CancellationToken cancellationToken = default)
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
        });
        if (result is not null)
        {
            var filePath = await FileService.SaveFile(result).ConfigureAwait(false);
            // Open the epub file
            var ebook = EbookService.OpenEbook(filePath);
 
            FileData fileData = new()
            {
                Title = ebook.Title,
                FileName = filePath,
            };
            await db.SaveFileData(fileData, cancellationToken).ConfigureAwait(false);
            Books.Add(ebook);

            return;
        }
        logger.Error("Error saving book");
    }

    [RelayCommand]
    public async Task RemoveBook(Book book, CancellationToken cancellationToken = default)
    {
        if (book is not null)
        {
            logger.Info("Removing book");
            FileService.DeleteFile(book.FilePath);
            await db.RemoveFileData(book, cancellationToken).ConfigureAwait(false);
            Books.Remove(book);
            logger.Info("Book removed from library.");
            OnPropertyChanged(nameof(Books));
        }
    }

    public static async Task<FileResult?> PickAndShow(PickOptions options)
    {
        try
        {
            return await FilePicker.PickAsync(options).WaitAsync(CancellationToken.None).ConfigureAwait(false);
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
