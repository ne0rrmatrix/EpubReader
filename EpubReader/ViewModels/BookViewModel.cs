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
using MetroLog;

namespace EpubReader.ViewModels;

public partial class BookViewModel : BaseViewModel, IQueryAttributable
{
	[ObservableProperty]
	public partial bool IsNavMenuVisible { get; set; } = true;
	[ObservableProperty]
	public partial string Source { get; set; } = string.Empty;
	[ObservableProperty]
	public partial Settings Settings { get; set; }

	static readonly ILogger logger = LoggerFactory.GetLogger(nameof(BookViewModel));

	public BookViewModel()
	{
		Settings = new();
		Source = string.Empty;
#if ANDROID
		StatusBarExtensions.SetStatusBarsHidden(IsNavMenuVisible);
#endif
		IsNavMenuVisible = !IsNavMenuVisible;
		Shell.SetNavBarIsVisible(Application.Current?.Windows[0].Page, IsNavMenuVisible);
	}
	public async void ApplyQueryAttributes(IDictionary<string, object> query)
	{
		if (query.TryGetValue("Book", out var bookObj) && bookObj is Book book)
		{
			Book = book;
			Settings = await db.GetSettings(CancellationToken.None).ConfigureAwait(false) ?? new Settings();
			var result = InjectIntoHtml.InjectAllCss(Book.Chapters[book.CurrentChapter].HtmlFile, book, Settings);
			if(!string.IsNullOrEmpty(result))
			{
				logger.Info("Setting source");
				Source = result;
			}
			else
			{
				logger.Info("html is null or empty");
			}
		}
		else
		{
			logger.Info("Book is null");
		}
	}

	[RelayCommand]
	static void ShowPopup()
	{
		SettingsPage popup = new();
		Shell.Current.ShowPopup(popup);
	}

	[RelayCommand]
	void Press()
	{
#if ANDROID
		StatusBarExtensions.SetStatusBarsHidden(IsNavMenuVisible);
#endif
		IsNavMenuVisible = !IsNavMenuVisible;
		Shell.SetNavBarIsVisible(Application.Current?.Windows[0].Page, IsNavMenuVisible);
		Shell.SetBackgroundColor(Application.Current?.Windows[0].Page, Color.FromArgb(Settings.BackgroundColor));
	}
}
