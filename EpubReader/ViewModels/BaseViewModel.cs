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
		ICollection<ResourceDictionary> mergedDictionaries = Application.Current.Resources.MergedDictionaries ?? throw new InvalidOperationException();
		var theme = mergedDictionaries.OfType<SyncfusionThemeResourceDictionary>().FirstOrDefault() ?? throw new InvalidOperationException();
		(Color? _, Color? _, Color? navigationColor) = (null, null, null);
		switch (Application.Current?.RequestedTheme)
		{
			case AppTheme.Dark:
				(_, _, navigationColor) = EbookColorScheme.GetColorSchemeColor(EbookColor.Dark);
				theme.VisualTheme = SfVisuals.MaterialLight;
				break;
			case AppTheme.Light:
				(_, _, navigationColor) = EbookColorScheme.GetColorSchemeColor(EbookColor.Default);
				theme.VisualTheme = SfVisuals.MaterialLight;
				break;
		}
		Shell.SetBackgroundColor(Application.Current?.Windows[0].Page, navigationColor);
	}
	~BaseViewModel()
	{
		if (Application.Current is not null)
		{
			Application.Current.RequestedThemeChanged -= OnRequestedThemeChanged;
		}
	}
}
