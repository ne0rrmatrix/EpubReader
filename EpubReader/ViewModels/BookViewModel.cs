#if ANDROID
using Android.Views;
using AndroidX.Core.View;

using CommunityToolkit.Maui.PlatformConfiguration.AndroidSpecific;

#endif

using CommunityToolkit.Maui.Core;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EpubReader.Models;
using EpubReader.Service;

namespace EpubReader.ViewModels;

public partial class BookViewModel : BaseViewModel, IQueryAttributable
{
	[ObservableProperty]
	public partial string Path { get; set; } = string.Empty;
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
			Path = book.WWWPath;
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
		StatusBarExtensions.SetStatusBarsHidden(IsNavMenuVisible);
#endif
		IsNavMenuVisible = !IsNavMenuVisible;
		Shell.SetNavBarIsVisible(Application.Current?.Windows[0].Page, IsNavMenuVisible);
	}
}
