using CommunityToolkit.Maui.Core;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EpubReader.Models;
using EpubReader.Util;

namespace EpubReader.ViewModels;

public partial class BookViewModel : BaseViewModel, IQueryAttributable
{
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
		Service.StatusBarExtensions.SetStatusBarsHidden(IsNavMenuVisible);
#endif
		IsNavMenuVisible = !IsNavMenuVisible;
		Shell.SetNavBarIsVisible(Application.Current?.Windows[0].Page, IsNavMenuVisible);
	}
}
