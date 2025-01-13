#if ANDROID
using Android.Views;
using AndroidX.Core.View;
using CommunityToolkit.Maui.Core.Platform;
using CommunityToolkit.Maui.PlatformConfiguration.AndroidSpecific;

#endif

using CommunityToolkit.Maui.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EpubReader.Interfaces;
using EpubReader.Models;
using EpubReader.Service;
using EpubReader.Views;

namespace EpubReader.ViewModels;

public partial class BookViewModel() : BaseViewModel, IQueryAttributable
{
	[ObservableProperty]
    public partial bool IsNavMenuVisible { get; set; } = true;
	[ObservableProperty]
	public partial string Source { get; set; }
	
    public async void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("Book", out var bookObj) && bookObj is Book book)
        {
			
			var temp = await db.GetBook(book.Title, CancellationToken.None).ConfigureAwait(true);
			book.CurrentChapter = temp.CurrentChapter;
			book.CurrentPage = temp.CurrentPage;
			Book = book;

			System.Diagnostics.Debug.WriteLine(Book.Title);
			Source = InjectIntoHtml.InjectAllCss(Book.Chapters[Book.CurrentChapter].HtmlFile, book, Settings);
		}
		else
		{
			throw new InvalidOperationException("Book not found");
		}
	}

    [RelayCommand]
    static void ShowPopup()
    {
        SettingsPage popup = new();
        Shell.Current.ShowPopup(popup);
    }

	[RelayCommand]
	void LongPress()
	{
		if (IsNavMenuVisible)
		{
			System.Diagnostics.Debug.WriteLine("Long press");
			IsNavMenuVisible = false;
			Shell.SetNavBarIsVisible(Application.Current?.Windows[0].Page, false);
#if ANDROID
			StatusBarExtensions.SetStatusBarsHidden(true);
#endif
		}
		else
		{
			System.Diagnostics.Debug.WriteLine("Long press");
			IsNavMenuVisible = true;
			Shell.SetNavBarIsVisible(Application.Current?.Windows[0].Page, true);
#if ANDROID
			StatusBarExtensions.SetStatusBarsHidden(false);
#endif
		}
	}
}
