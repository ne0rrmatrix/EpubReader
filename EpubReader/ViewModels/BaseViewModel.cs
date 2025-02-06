using CommunityToolkit.Mvvm.ComponentModel;
using EpubReader.Interfaces;
using EpubReader.Models;
using EpubReader.Service;
using Syncfusion.Maui.Toolkit.Themes;

namespace EpubReader.ViewModels;

public partial class BaseViewModel : ObservableObject
{
	public readonly IDispatcher Dispatcher = Application.Current?.Dispatcher ?? throw new InvalidOperationException();
	public IDb db { get; set; } = Application.Current?.Handler.MauiContext?.Services.GetRequiredService<IDb>() ?? throw new InvalidOperationException();
	[ObservableProperty]
	public partial Book Book { get; set; }

	public BaseViewModel()
	{
		Book = new();
		Application.Current.RequestedThemeChanged += OnRequestedThemeChanged;
	}

	static void OnRequestedThemeChanged(object? sender, AppThemeChangedEventArgs e)
	{
		ArgumentNullException.ThrowIfNull(Application.Current);
		ICollection<ResourceDictionary> mergedDictionaries = Application.Current.Resources.MergedDictionaries;
		if (mergedDictionaries != null)
		{
			var theme = mergedDictionaries.OfType<SyncfusionThemeResourceDictionary>().FirstOrDefault();
			if (theme != null)
			{
				if (Application.Current?.RequestedTheme == AppTheme.Dark)
				{
					(_, _,var navigationColor) = CustomColorScheme.GetColorSchemeColor(CustomColor.Dark);
					Shell.SetBackgroundColor(Application.Current?.Windows[0].Page, navigationColor);
					theme.VisualTheme = SfVisuals.MaterialLight;
				}
				else
				{
					(_, _,var navigationColor) = CustomColorScheme.GetColorSchemeColor(CustomColor.Default);
					Shell.SetBackgroundColor(Application.Current?.Windows[0].Page, navigationColor);
					theme.VisualTheme = SfVisuals.MaterialDark;
				}
			}

		}
	}
	~BaseViewModel()
	{
		if (Application.Current is not null)
		{
			Application.Current.RequestedThemeChanged -= OnRequestedThemeChanged;
		}
	}
}
