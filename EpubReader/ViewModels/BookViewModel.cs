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
using EpubReader.Views;

namespace EpubReader.ViewModels;

public partial class BookViewModel() : BaseViewModel, IQueryAttributable
{
#if ANDROID
	int platformColor;
#endif

	[ObservableProperty]
    public partial bool IsNavMenuVisible { get; set; } = true;

    Book? book;
    public Book? Book
    {
        get => book;
        set
        {
            SetProperty(ref book, value);
            IsNavMenuVisible = false;
        }
    }

    public IDb db { get; set; } = Application.Current?.Handler.MauiContext?.Services.GetRequiredService<IDb>() ?? throw new InvalidOperationException();

	public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("Book", out var bookObj))
        {
            Book = bookObj as Book;
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
			
            IsNavMenuVisible = false;
            Shell.SetNavBarIsVisible(Application.Current?.Windows[0].Page, false);

#if ANDROID
			var activity = Platform.CurrentActivity ?? throw new InvalidOperationException();
			var decorView = activity.Window?.DecorView ?? throw new InvalidOperationException();
			var window = activity.Window ?? throw new InvalidOperationException();

			window.SetStatusBarColor(Android.Graphics.Color.ParseColor("#000000"));
			window.ClearFlags(WindowManagerFlags.DrawsSystemBarBackgrounds);
			platformColor = window.StatusBarColor;
			window.SetFlags(WindowManagerFlags.LayoutNoLimits, WindowManagerFlags.LayoutNoLimits);
			var insets = WindowCompat.GetInsetsController(window, activity.Window.DecorView) ?? throw new InvalidOperationException();
			insets.Hide(WindowInsets.Type.NavigationBars());
#endif

		}
		else
        {
            IsNavMenuVisible = true;
            Shell.SetNavBarIsVisible(Application.Current?.Windows[0].Page, true);
#if ANDROID
			StatusBar.SetColor(Color.FromInt(platformColor));
			var activity = Platform.CurrentActivity ?? throw new InvalidOperationException();
			var window = activity.Window ?? throw new InvalidOperationException();
			var insets = WindowCompat.GetInsetsController(window, activity.Window.DecorView) ?? throw new InvalidOperationException();
			insets.Show(WindowInsets.Type.NavigationBars());
			window.ClearFlags(WindowManagerFlags.LayoutNoLimits);
			window.SetFlags(WindowManagerFlags.DrawsSystemBarBackgrounds, WindowManagerFlags.DrawsSystemBarBackgrounds);	
#endif

		}
	}
}
