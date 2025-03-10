﻿using System.Collections.ObjectModel;
using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;
using CommunityToolkit.Maui.Core.Platform;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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
#if ANDROID
		StatusBar.SetColor(Color.FromArgb("#3E8EED"));
#endif
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
			var ebook = EbookService.OpenEbook(item.FilePath) ?? throw new InvalidOperationException();
			ebook.CurrentChapter = item.CurrentChapter;
			Books.Add(ebook);
		}
	}

    [RelayCommand]
    public static async Task GotoBookPage(Book Book)
    {
		if(Book is null)
		{
			logger.Info("Book is null");
			return;
		}
		var navigationParams = new Dictionary<string, object>
        {
            { "Book", Book }
        };
        await Shell.Current.GoToAsync($"//BookPage", navigationParams);
    }

    [RelayCommand]
    async Task Add(CancellationToken cancellationToken = default)
    {
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
		var ebook = EbookService.OpenEbook(result.FullPath);
		if (ebook is null)
		{
			logger.Info("Error opening ebook");
			return;
		}

		if (bookData.Any(x => x.Title == ebook.Title))
		{
			await ShowSnackBar("Book already exists in library", "OK", cancellationToken);
			logger.Info("Book already exists in library");
			return;
		}

		var filePath = await FileService.SaveFile(result).ConfigureAwait(false);
		ebook.FilePath = filePath;
		await db.SaveBookData(ebook, cancellationToken).ConfigureAwait(false);
		Books.Add(ebook);
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
