using FFImageLoading.Maui;
using MetroLog.Operators;
using MetroLog.Targets;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.LifecycleEvents;
using Syncfusion.Maui.Toolkit.Hosting;
using LoggerFactory = MetroLog.LoggerFactory;
using LogLevel = MetroLog.LogLevel;
using Plugin.Maui.Audio;

#if ANDROID
using Plugin.Firebase.Core.Platforms.Android;
using Plugin.Firebase.Auth;
using EpubReader.Platforms.Android; // <- add loader reference
#endif

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
			fonts.AddFont("OpenDyslexic3-Regular.ttf", "OpenDyslexic3Regular");
			fonts.AddFont("arial.ttf", "Arial");
			fonts.AddFont("times.ttf", "Times New Roman");
			fonts.AddFont("comic.ttf", "Comic Sans MS");
			fonts.AddFont("georgia.ttf", "Georgia");
			fonts.AddFont("cour.ttf", "Courier New");
			fonts.AddFont("trebuc.ttf", "Trebuchet MS");
			fonts.AddFont("Helvetica.ttf", "Helvetica");
			fonts.AddFont("verdana.ttf", "Verdana");
			fonts.AddFont("tahoma.ttf", "Tahoma");
		})
#if DEBUG
								.UseMauiCommunityToolkit(static options =>
								{
									options.SetShouldEnableSnackbarOnWindows(true);
									options.SetPopupDefaults(new DefaultPopupSettings());
									options.SetPopupOptionsDefaults(new DefaultPopupOptionsSettings());
								})
#else
								.UseMauiCommunityToolkit(static options =>
								{
									options.SetShouldEnableSnackbarOnWindows(true);
									options.SetShouldSuppressExceptionsInConverters(true);
									options.SetShouldSuppressExceptionsInBehaviors(true);
									options.SetShouldSuppressExceptionsInAnimations(true);
								})
#endif
		.ConfigureSyncfusionToolkit()
		.UseFFImageLoading()
		.RegisterFirebaseServices()
		.ConfigureMauiHandlers(handlers =>
		{
#if IOS || MACCATALYST
			handlers.AddHandler<CollectionView, Microsoft.Maui.Controls.Handlers.Items2.CollectionViewHandler2>();
#endif
		});
#if ANDROID
		// Load Firebase config early on Android
		FirebaseConfig.TryLoadFromGoogleServicesJson();

		WebViewHandler.Mapper.ModifyMapping(
	  nameof(Android.Webkit.WebView.WebViewClient),
	  (handler, view, args) =>
	  {
		handler.PlatformView.SetWebViewClient(new CustomWebViewClient(handler));
	  });
#elif WINDOWS
		WebViewHandler.Mapper.ModifyMapping(
	  nameof(Microsoft.UI.Xaml.Controls.WebView2),
	  async (handler, view, args) =>{ WebViewExtensions.Initialize(handler); await handler.PlatformView.EnsureCoreWebView2Async(); });
#elif IOS || MACCATALYST
		WebViewHandler.PlatformViewFactory = (handler) =>
		{
			var userContentController = new WKUserContentController();
			var messageHandler = new MyWKScriptMessageHandler();
			userContentController.AddScriptMessageHandler(messageHandler, "webwindowinterop"); // Register the handler with the name
			var config = new WKWebViewConfiguration { UserContentController = userContentController };
			if (OperatingSystem.IsMacCatalystVersionAtLeast(10) || OperatingSystem.IsIOSVersionAtLeast(10))
			{
				config.AllowsPictureInPictureMediaPlayback = true;
				config.AllowsInlineMediaPlayback = true;
				config.MediaTypesRequiringUserActionForPlayback = WKAudiovisualMediaTypes.None;
			}
			config.DefaultWebpagePreferences!.AllowsContentJavaScript = true;
			config.SetUrlSchemeHandler(new CustomUrlSchemeHandler(), "app");

			var webView = new CustomMauiWKWebView(CGRect.Empty, (WebViewHandler)handler, config)
			{
				NavigationDelegate = new CustomWebViewNavigationDelegate((WebViewHandler)handler),
			};
			if (OperatingSystem.IsIOSVersionAtLeast(17) || OperatingSystem.IsMacCatalystVersionAtLeast(17))
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
		// Register services
		builder.Services.AddSingleton<IDb, Db>();
		builder.Services.AddSingleton<AuthenticationService>();
		builder.Services.AddSingleton<ISyncService, FirebaseSyncService>();
		builder.Services.AddSingleton<IAudioManager>(_ => AudioManager.Current);
		LoggerFactory.Initialize(config);
		builder.Services.AddSingleton(LogOperatorRetriever.Instance);
		builder.Services.AddSingleton<StreamExtensions>();
		builder.Services.AddSingleton<IFolderPicker, FolderPicker>();
		builder.Services.AddSingleton<AppShell>();
		builder.Services.AddSingleton<BaseViewModel>();
		builder.Services.AddSingleton<ProcessEpubFiles>();

		// Register Popup pages and their view models
		builder.Services.AddTransientPopup<SettingsPage, SettingsPageViewModel>();
		builder.Services.AddTransientPopup<CalibreSettingsPage, CalibreSettingsPageViewModel>();

		// Register main pages and their view models
		builder.Services.AddTransientWithShellRoute<LibraryPage, LibraryViewModel>("LibraryPage");
		builder.Services.AddTransientWithShellRoute<RecentBooksPage, RecentBooksViewModel>("RecentBooksPage");
		builder.Services.AddTransientWithShellRoute<BookPage, BookViewModel>("BookPage");
		builder.Services.AddTransientWithShellRoute<BookDetailsPage, BookDetailsViewModel>("BookDetailsPage");
		builder.Services.AddTransientWithShellRoute<CalibrePage, CalibrePageViewModel>("CalibrePage");
		builder.Services.AddTransientWithShellRoute<LoginPage, LoginPageViewModel>("LoginPage");
		builder.Services.AddTransientWithShellRoute<PrivacyPage, PrivacyPageViewModel>("privacy");
		return builder.Build();
	}

	static MauiAppBuilder RegisterFirebaseServices(this MauiAppBuilder builder)
	{
		builder.ConfigureLifecycleEvents(events =>
		{
#if ANDROID
			events.AddAndroid(android => android.OnCreate((activity, _) =>
			{
				InitializeFirebaseOnAndroid(activity);
			}));
#endif
		});

		return builder;
	}

#if ANDROID
	static void InitializeFirebaseOnAndroid(Android.App.Activity activity)
	{
		// Ensure config loader runs and initialize Firebase programmatically so we never rely on Android resource strings
		try
		{

			// Build FirebaseOptions from loaded configuration
			var appId = FirebaseConfig.AppId;
			var apiKey = FirebaseConfig.ApiKey;
			var databaseUrl = FirebaseConfig.DatabaseUrl;

			if (!string.IsNullOrWhiteSpace(appId))
			{
				var optionsBuilder = new Firebase.FirebaseOptions.Builder()
					.SetApplicationId(appId);

				if (!string.IsNullOrWhiteSpace(apiKey))
				{
					optionsBuilder.SetApiKey(apiKey);
				}

				if (!string.IsNullOrWhiteSpace(databaseUrl))
				{
					optionsBuilder.SetDatabaseUrl(databaseUrl);
				}

				var options = optionsBuilder.Build();
				Firebase.FirebaseApp.InitializeApp(activity, options);
			}

			CrossFirebase.Initialize(activity);
		}
		catch (Exception ex)
		{
			System.Diagnostics.Trace.TraceWarning($"DEBUG: Firebase startup error: {ex.Message}");
		}
	}
#endif
}