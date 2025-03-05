#if ANDROID
using Android.Views;
using AndroidX.Core.View;

using CommunityToolkit.Maui.PlatformConfiguration.AndroidSpecific;

#endif

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EpubReader.Models;
using EpubReader.Service;
using CommunityToolkit.Maui.Core;

namespace EpubReader.ViewModels;

public partial class BookViewModel : BaseViewModel, IQueryAttributable
{
	[ObservableProperty]
	public partial bool IsNavMenuVisible { get; set; }

	[ObservableProperty]
	public partial Settings Settings { get; set; }
	readonly IPopupService popupService;
	public BookViewModel(IPopupService popupService)
	{
		Settings = new();
		this.popupService = popupService;
		IsNavMenuVisible = true;
		Press();
	}
	public async void ApplyQueryAttributes(IDictionary<string, object> query)
	{
		if (query.TryGetValue("Book", out var bookObj) && bookObj is Book book)
		{
			Book = book;
			Settings = await db.GetSettings(CancellationToken.None).ConfigureAwait(false);
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
