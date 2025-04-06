using CommunityToolkit.Maui;
using CommunityToolkit.Maui.Storage;
using EpubReader.Database;
using EpubReader.Interfaces;
using EpubReader.Util;
using EpubReader.ViewModels;
using EpubReader.Views;
using FFImageLoading.Maui;
using MetroLog;
using MetroLog.Operators;
using MetroLog.Targets;
using Microsoft.Extensions.Logging;
using Syncfusion.Maui.Toolkit.Hosting;
using LoggerFactory = MetroLog.LoggerFactory;
using LogLevel = MetroLog.LogLevel;

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
		.UseFFImageLoading()
		.UseMauiCommunityToolkit(options =>
		{
			options.SetShouldEnableSnackbarOnWindows(true);
		})
		.ConfigureSyncfusionToolkit()
		.ConfigureMauiHandlers(handlers =>
		{
#if IOS || MACCATALYST
			handlers.AddHandler<CollectionView, Microsoft.Maui.Controls.Handlers.Items2.CollectionViewHandler2>();
			handlers.AddHandler<CarouselView, Microsoft.Maui.Controls.Handlers.Items2.CarouselViewHandler2>();
#endif
		});
#if ANDROID
		Microsoft.Maui.Handlers.WebViewHandler.Mapper.ModifyMapping(
	  nameof(Android.Webkit.WebView.WebViewClient),
	  (handler, view, args) => handler.PlatformView.SetWebViewClient(new CustomWebViewClient(handler)));
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
		builder.Services.AddHybridWebViewDeveloperTools();
#endif
		builder.Services.AddSingleton<IDb, Db>();		
		LoggerFactory.Initialize(config);
        builder.Services.AddSingleton(LogOperatorRetriever.Instance);
		builder.Services.AddSingleton<StreamExtensions>();
		builder.Services.AddSingleton<IFolderPicker>(FolderPicker.Default);
        builder.Services.AddSingleton<IFilePicker>(FilePicker.Default);
        builder.Services.AddSingleton<AppShell>();
        builder.Services.AddSingleton<BaseViewModel>();

		builder.Services.AddTransientPopup<SettingsPage, SettingsPageViewModel>();
		builder.Services.AddTransientWithShellRoute<LibraryPage, LibraryViewModel>("//LibraryPage");
		builder.Services.AddTransientWithShellRoute<BookPage, BookViewModel>("//BookPage");
		return builder.Build();
    }
}
