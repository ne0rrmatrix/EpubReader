using CommunityToolkit.Maui;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EpubReader.Models;
using EpubReader.Util;

namespace EpubReader.ViewModels;

public partial class BookViewModel : BaseViewModel, IQueryAttributable
{
#if ANDROID || WINDOWS
	const string url = "https://demo/index.html";
#elif IOS || MACCATALYST
	const string url = "app://demo/index.html";
#endif

	readonly IPopupService popupService;
	readonly StreamExtensions streamExtensions = Application.Current?.Windows[0].Page?.Handler?.MauiContext?.Services.GetRequiredService<StreamExtensions>() ?? throw new InvalidOperationException();

	[ObservableProperty]
	public partial WebViewSource Source { get; set; } = new UrlWebViewSource
	{
		Url = url,
	};
	
	[ObservableProperty]
	public partial ImageSource CoverImage { get; set; } = string.Empty;
	
	[ObservableProperty]
	public partial bool IsActive { get; set; } = true;
	
	[ObservableProperty]
	public partial bool isPopupActive { get; set; } = false;

	[ObservableProperty]
	public partial bool IsNavMenuVisible { get; set; } = true;

	public BookViewModel(IPopupService popupService)
	{
		this.popupService = popupService;
		Press();
	}

	public void ApplyQueryAttributes(IDictionary<string, object> query)
	{
		if (query.TryGetValue("Book", out var bookObj) && bookObj is Book book)
		{
			Book = book;
			streamExtensions.SetBook(Book);
			var bytes = book.CoverImage ?? throw new InvalidOperationException("CoverImage is null");
			CoverImage = ImageSource.FromStream(() => new MemoryStream(bytes));
		}
	}

	[RelayCommand]
	async Task ShowPopup()
	{
		isPopupActive = true;
		var result = await this.popupService.ShowPopupAsync<SettingsPageViewModel>(Shell.Current);
		if (result.WasDismissedByTappingOutsideOfPopup)
		{
			isPopupActive = false;
		}
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
