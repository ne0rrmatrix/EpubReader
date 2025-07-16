using CommunityToolkit.Maui;
using EpubReader.Controls;
using EpubReader.Database;
using EpubReader.Interfaces;
using EpubReader.Util;
using EpubReader.ViewModels;
using EpubReader.Views;
using MetroLog;
using MetroLog.Operators;
using MetroLog.Targets;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Handlers;
using Syncfusion.Maui.Toolkit.Hosting;
using LoggerFactory = MetroLog.LoggerFactory;
using LogLevel = MetroLog.LogLevel;
using EpubReader.Service;

#if IOS || MACCATALYST
using CoreGraphics;
using Foundation;
using UIKit;
using WebKit;
#endif

[assembly: XamlCompilation(XamlCompilationOptions.Compile)]

namespace EpubReader;
public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder.UseMauiApp<App>().ConfigureFonts(fonts =>
		{
			fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
			fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
		})
		.UseMauiCommunityToolkit(static options =>
		{
			options.SetShouldEnableSnackbarOnWindows(true);
			options.SetPopupDefaults(new DefaultPopupSettings());
			options.SetPopupOptionsDefaults(new DefaultPopupOptionsSettings());
		})
		.ConfigureSyncfusionToolkit()
		.ConfigureMauiHandlers(handlers =>
		{
#if IOS || MACCATALYST
			handlers.AddHandler<CollectionView, Microsoft.Maui.Controls.Handlers.Items2.CollectionViewHandler2>();
#endif
		});
#if ANDROID
		WebViewHandler.Mapper.ModifyMapping(
	  nameof(Android.Webkit.WebView.WebViewClient),
	  (handler, view, args) => handler.PlatformView.SetWebViewClient(new CustomWebViewClient(handler)));
#elif WINDOWS
		WebViewHandler.Mapper.ModifyMapping(
	  nameof(Microsoft.UI.Xaml.Controls.WebView2),
	  async (handler, view, args) =>{ WebViewExtensions.Initialize(handler); await handler.PlatformView.EnsureCoreWebView2Async(); });
#elif IOS || MACCATALYST
		WebViewHandler.PlatformViewFactory = (handler) => 
		{
			var config = new WKWebViewConfiguration();
			if (OperatingSystem.IsMacCatalystVersionAtLeast(10) || OperatingSystem.IsIOSVersionAtLeast(10))
			{
				config.AllowsPictureInPictureMediaPlayback = true;
				config.AllowsInlineMediaPlayback = true;
				config.MediaTypesRequiringUserActionForPlayback = WKAudiovisualMediaTypes.None;
			}
			config.DefaultWebpagePreferences!.AllowsContentJavaScript = true;
			config.SetUrlSchemeHandler(new CustomUrlSchemeHandler(), "app");
			
			var webView = new CustomMauiWKWebView(CGRect.Empty,(WebViewHandler)handler, config)
			{
				NavigationDelegate = new CustomWebViewNavigationDelegate((WebViewHandler)handler),
			};
			if(OperatingSystem.IsIOSVersionAtLeast(17) || OperatingSystem.IsMacCatalystVersionAtLeast(17))
			{
				webView.Inspectable = true;
			}
			return webView;
		};
#endif
		var config = new LoggingConfiguration();
#if RELEASE
        config.AddTarget(
            LogLevel.Info,
            LogLevel.Fatal,
            new StreamingFileTarget(retainDays: 2));
#else
		// Will write logs to the Debug output
		config.AddTarget(
			LogLevel.Trace,
			LogLevel.Fatal,
			new TraceTarget());
#endif

        // will write logs to the console output (Logcat for android)
        config.AddTarget(
            LogLevel.Info,
            LogLevel.Fatal,
            new ConsoleTarget());

        config.AddTarget(
            LogLevel.Info,
            LogLevel.Fatal,
            new MemoryTarget(2048));
#if DEBUG
		builder.Logging.AddDebug();
#endif
		builder.Services.AddSingleton<IDb, Db>();		
		LoggerFactory.Initialize(config);
        builder.Services.AddSingleton(LogOperatorRetriever.Instance);
		builder.Services.AddSingleton<StreamExtensions>();
		builder.Services.AddSingleton<IFolderPicker, FolderPicker>();
		builder.Services.AddSingleton<AppShell>();
        builder.Services.AddSingleton<BaseViewModel>();

		builder.Services.AddSingleton<WebViewHelper>();
		builder.Services.AddTransientPopup<SettingsPage, SettingsPageViewModel>();
		builder.Services.AddTransientWithShellRoute<LibraryPage, LibraryViewModel>("LibraryPage");
		builder.Services.AddTransientWithShellRoute<BookPage, BookViewModel>("BookPage");
		return builder.Build();
    }
}