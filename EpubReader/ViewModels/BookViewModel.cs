using CommunityToolkit.Maui.Core;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EpubReader.Models;
using EpubReader.Service;
using EpubReader.Util;

namespace EpubReader.ViewModels;

public partial class BookViewModel : BaseViewModel, IQueryAttributable
{
	[ObservableProperty]
	public partial bool IsActive { get; set; } = true;
	readonly StreamExtensions streamExtensions = Application.Current?.Windows[0].Page?.Handler?.MauiContext?.Services.GetRequiredService<StreamExtensions>() ?? throw new InvalidOperationException();
#pragma warning disable S1075 // URIs should not be hardcoded
	static readonly string url = "https://demo/index.html";
#pragma warning restore S1075 // URIs should not be hardcoded

	[ObservableProperty]
	public partial WebViewSource? Source { get; set; } = new UrlWebViewSource
	{
		Url = "about:blank",
	};

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
#if ANDROID || WINDOWS
			Source = new UrlWebViewSource
			{
				Url = url,
			};
#elif IOS || MACCATALYST
			Source = new UrlWebViewSource
			{
				Url = "app://demo/index.html",
			};
#endif
		}
	}

	[RelayCommand]
	void ShowPopup()
	{
		popupService.ShowPopup<SettingsPageViewModel>();
	}

	[RelayCommand]
	public void Press()
	{
#if ANDROID
		EpubReader.Service.StatusBarExtensions.SetStatusBarsHidden(IsNavMenuVisible);
#endif
		IsNavMenuVisible = !IsNavMenuVisible;
		Shell.SetNavBarIsVisible(Application.Current?.Windows[0].Page, IsNavMenuVisible);
	}
}
