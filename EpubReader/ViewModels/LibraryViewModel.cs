using System.Collections.ObjectModel;
using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EpubReader.Interfaces;
using EpubReader.Models;
using EpubReader.Service;
using EpubReader.Util;
using ILogger = MetroLog.ILogger;
using LoggerFactory = MetroLog.LoggerFactory;

namespace EpubReader.ViewModels;
public partial class LibraryViewModel : BaseViewModel
{
	static readonly ILogger logger = LoggerFactory.GetLogger(nameof(LibraryViewModel));
	readonly IFolderPicker folderPicker = Application.Current?.Handler.MauiContext?.Services.GetRequiredService<IFolderPicker>() ?? throw new InvalidOperationException();
	static readonly string[] epub = [".epub", ".epub"];
    static readonly string[] android_epub = ["application/epub+zip", ".epub"];
	readonly FilePickerFileType customFileType = new(
		  new Dictionary<DevicePlatform, IEnumerable<string>>
		  {
			 { DevicePlatform.iOS, new[] { "org.idpf.epub-container" } },
			 { DevicePlatform.MacCatalyst, new[] { "org.idpf.epub-container" } },
			 { DevicePlatform.Android, android_epub },
			 { DevicePlatform.WinUI, epub },
			 { DevicePlatform.Tizen, epub },
		  });

	[ObservableProperty]
    public partial ObservableCollection<Book> Books { get; set; }
   
	public LibraryViewModel()
    {
		Books = [.. db.GetAllBooks() ?? []];
	}

    [RelayCommand]
    public async Task GotoBookPage(Book book)
    {
		var temp = db.GetBook(book);
		ArgumentNullException.ThrowIfNull(temp);
		Book = await EbookService.OpenEbook(book.FilePath).ConfigureAwait(true) ?? throw new InvalidOperationException("Error opening ebook");
		Book.CurrentChapter = temp.CurrentChapter;
		Book.Id = temp.Id;
		Util.StreamExtensions.Instance?.SetBook(Book);
		var navigationParams = new Dictionary<string, object>
        {
            { "Book", Book }
        };
        await Shell.Current.GoToAsync($"BookPage", navigationParams);
    }

	[RelayCommand]
	async Task AddFolder(CancellationToken cancellationToken = default)
	{
		var folderUri = await folderPicker.PickFolder();
		if (string.IsNullOrEmpty(folderUri))
		{
			logger.Info("No folder selected");
			return;
		}
		logger.Info($"Selected folder: {folderUri}");
		var epubFiles = await folderPicker.EnumerateEpubFilesInFolderAsync(folderUri);
		if (epubFiles.Count == 0)
		{
			logger.Info("No epub files found in the selected folder");
			await Dispatcher.DispatchAsync(async () => await Toast.Make("No epub files found in the selected folder", ToastDuration.Short, 12).Show(cancellationToken));
			return;
		}
		logger.Info($"Found {epubFiles.Count} epub files in the selected folder");
		string message = string.Empty;

		
		foreach (var file in epubFiles)
		{
			var bookData = db.GetAllBooks() ?? [];
			var stream = await folderPicker.PerformFileOperationOnEpubAsync(file);
			if (stream is null)
			{
				logger.Info($"Failed to open stream for file: {file}");
				continue;
			}
			Book? ebook = null;
			ebook = EbookService.GetListing(stream, file);
			stream.Seek(0, SeekOrigin.Begin); // Reset stream position for reading
			if (ebook is null)
			{
				message = $"Error opening Book: {file}";
				await Dispatcher.DispatchAsync(async () => await Toast.Make(message, ToastDuration.Short, 12).Show(cancellationToken));
				logger.Info(message);
				continue;
			}
			if (Books.Any(x => x.Title == ebook.Title))
			{
				message = $"Book already exists in library: {ebook.Title}";
				await Dispatcher.DispatchAsync(async () => await Toast.Make(message, ToastDuration.Short, 12).Show(cancellationToken));
				logger.Info(message);
				continue;
			}
			
			ebook.FilePath = await FileService.SaveFile(stream, file);
			ebook.CoverImagePath = await FileService.SaveImage(file, ebook.CoverImage);
			if (File.Exists(ebook.FilePath) && File.Exists(ebook.CoverImagePath))
			{
				logger.Info($"Book {ebook.Title} saved successfully.");
			}
			else
			{
				logger.Error($"Failed to save book {ebook.Title} or its cover image.");
				message = $"Failed to save book {ebook.Title} or its cover image.";
				await Dispatcher.DispatchAsync(async () => await Toast.Make(message, ToastDuration.Short, 12).Show(cancellationToken));
				continue;
			}

			db.SaveBookData(ebook);
			Books.Add(ebook);
		}
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

		var ebook = EbookService.GetListing(result.FullPath);
		if (ebook is null)
		{
			message = "Error opening Book.";
			await Dispatcher.DispatchAsync(async () => await Toast.Make(message, ToastDuration.Short, 12).Show(cancellationToken));
			logger.Info(message);
			return;
		}
		if (Books.Any(x => x.Title == ebook.Title))
		{
			message = $"Book already exists in library: {ebook.Title}";
			await Dispatcher.DispatchAsync(async () => await Toast.Make(message, ToastDuration.Short, 12).Show(cancellationToken));
			logger.Info(message);
			return;
		}
		
		ebook.FilePath =  await FileService.SaveFile(result, ebook.FilePath).ConfigureAwait(false);
		ebook.CoverImagePath = await FileService.SaveImage(ebook.FilePath, ebook.CoverImage).ConfigureAwait(false);
		db.SaveBookData(ebook);
		Books.Add(ebook);

		message = "Book added to library";
		await Dispatcher.DispatchAsync(async () => await Toast.Make(message, ToastDuration.Short, 12).Show(cancellationToken));
		logger.Info(message);
	}

    [RelayCommand]
    void RemoveBook(Book book)
    {
		logger.Info($"Removing book {book.FilePath}");
		var directory = Path.GetDirectoryName(book.FilePath);
		if(directory is not null)
		{
			Directory.Delete(directory, true);
		}
		
		db.RemoveBook(book);
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

}
