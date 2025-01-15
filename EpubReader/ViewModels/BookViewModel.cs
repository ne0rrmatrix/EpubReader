#if ANDROID
using Android.Views;
using AndroidX.Core.View;
using CommunityToolkit.Maui.Core.Platform;
using CommunityToolkit.Maui.PlatformConfiguration.AndroidSpecific;

#endif

using CommunityToolkit.Maui.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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
	[ObservableProperty]
	public partial Settings Settings { get; set; }

	public async void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("Book", out var bookObj) && bookObj is Book book)
        {
			Book = book;
			Settings = await db.GetSettings(CancellationToken.None);
			Source = InjectIntoHtml.InjectAllCss(Book.Chapters[book.CurrentChapter].HtmlFile, book, Settings);
			if(OperatingSystem.IsAndroid())
			{
				IsNavMenuVisible = false;
			}
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
	public void DoubleTapped()
	{
		HandleMenuCommand();
	}

	[RelayCommand]
	void LongPress()
	{
		HandleMenuCommand();
	}

	void HandleMenuCommand()
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
