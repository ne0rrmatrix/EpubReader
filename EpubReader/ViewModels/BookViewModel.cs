using CommunityToolkit.Maui;
using CommunityToolkit.Maui.Core;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EpubReader.Models;
using EpubReader.Util;

namespace EpubReader.ViewModels;

public partial class BookViewModel : BaseViewModel, IQueryAttributable
{
	[ObservableProperty]
	public partial WebViewSource Source { get; set; } = string.Empty;

	[ObservableProperty]
	public partial ImageSource CoverImage { get; set; } = string.Empty;
	[ObservableProperty]
	public partial bool IsActive { get; set; } = true;
	readonly StreamExtensions streamExtensions = Application.Current?.Windows[0].Page?.Handler?.MauiContext?.Services.GetRequiredService<StreamExtensions>() ?? throw new InvalidOperationException();

	[ObservableProperty]
	public partial bool IsNavMenuVisible { get; set; }

	readonly IPopupService popupService;
	public BookViewModel(IPopupService popupService)
	{
		this.popupService = popupService;
		IsNavMenuVisible = true;
		Press();
	}

	public void ApplyQueryAttributes(IDictionary<string, object> query)
	{
		if (query.TryGetValue("Book", out var bookObj) && bookObj is Book book)
		{
			Book = book;
			streamExtensions.SetBook(Book);
			StreamExtensions.Instance?.SetBook(book);
			var bytes = book.CoverImage ?? throw new InvalidOperationException("CoverImage is null");
			CoverImage = ImageSource.FromStream(() => new MemoryStream(bytes));
#pragma warning disable S1075 // URIs should not be hardcoded
#if ANDROID || WINDOWS
			Source = new UrlWebViewSource
		{
			Url = "https://demo/index.html",
		};
#endif
#if IOS || MACCATALYST
			Source = new UrlWebViewSource
			{
				Url = "app://demo/index.html",
			};
#endif
#pragma warning restore S1075 // URIs should not be hardcoded
		}
	}

	[RelayCommand]
	void ShowPopup()
	{
		this.popupService.ShowPopup<SettingsPageViewModel>(Shell.Current, options: new PopupOptions
		{
			CanBeDismissedByTappingOutsideOfPopup = true,
		});
	}

	[RelayCommand]
	public void Press()
	{
#if ANDROID
		Service.StatusBarExtensions.SetStatusBarsHidden(IsNavMenuVisible);
#endif
		IsNavMenuVisible = !IsNavMenuVisible;
		Shell.SetNavBarIsVisible(Application.Current?.Windows[0].Page, IsNavMenuVisible);
	}
}
