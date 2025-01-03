using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EpubReader.Database;
using EpubReader.Models;
using EpubReader.Service;
using System.Collections.ObjectModel;
using ILogger = MetroLog.ILogger;
using LoggerFactory = MetroLog.LoggerFactory;

namespace EpubReader.ViewModels;
public partial class LibraryViewModel : BaseViewModel
{
    static readonly ILogger logger = LoggerFactory.GetLogger(nameof(LibraryViewModel));
    static readonly string[] epub = [".epub", ".epub"];
    static readonly string[] android_epub = ["application/epub+zip", ".epub"];
    [ObservableProperty]
    public partial ObservableCollection<Book> Books { get; set; } = new();
   
    readonly Db db;
    public LibraryViewModel(Db dataBase)
    {
        this.db = dataBase;
        _ = LoadBooks();
    }

    async Task LoadBooks(CancellationToken cancellationToken = default)
    {
        List<FileData> temp = await db.GetFileData(cancellationToken);
        var books = new ObservableCollection<Book>();
        foreach (var item in temp)
        {
            var book = EbookService.OpenEbook(item.FileName);
            book.CoverImageFileName = item.CoverImageFileName;
            books.Add(book);
        }
        MainThread.BeginInvokeOnMainThread(() => { Books = books; OnPropertyChanged(nameof(Books)); });
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
            var filePath = await Service.FileService.SaveFile(result).ConfigureAwait(false);
            var ebook = Service.EbookService.OpenEbook(filePath);
            string tempFile = Path.GetFileNameWithoutExtension(ebook.CoverImageFileName);
            string coverImagePath = tempFile + ".jpg";
            string fullPath = Path.Combine(FileService.saveDirectory, coverImagePath);
            string imagePath = await FileService.SaveFile(ebook.CoverImage, fullPath, CancellationToken.None);
            FileData fileData = new()
            {
                Title = ebook.Title,
                FileName = filePath,
                CoverImageFileName = imagePath
            };
            ebook.CoverImageFileName = imagePath;
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
            try
            {
                if(File.Exists(book.FilePath))
                {
                    FileService.DeleteFile(book.FilePath);
                    return;
                }
                else
                {
                    logger.Error($"File? {book.FilePath} does not exist");
                }
                if(File.Exists(book.CoverImageFileName))
                {
                    FileService.DeleteFile(book.CoverImageFileName);
                    return;
                }
                else
                {
                    logger.Error($"File? {book.CoverImageFileName} does not exist");
                }
            }
            catch (Exception ex)
            {
                logger.Error($"Error removing book files: {ex.Message}");
            }
           
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
}
