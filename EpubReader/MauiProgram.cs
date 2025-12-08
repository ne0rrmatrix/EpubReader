using FFImageLoading.Maui;
using MetroLog.Operators;
using MetroLog.Targets;
using Microsoft.Maui.Handlers;
using Syncfusion.Maui.Toolkit.Hosting;
using LoggerFactory = MetroLog.LoggerFactory;
using LogLevel = MetroLog.LogLevel;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.LifecycleEvents;

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
		})
		.UseMauiCommunityToolkit(static options =>
		{
			options.SetShouldEnableSnackbarOnWindows(true);
			options.SetPopupDefaults(new DefaultPopupSettings());
			options.SetPopupOptionsDefaults(new DefaultPopupOptionsSettings());
		})
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
		FirebaseConfigLoader.InjectFirebaseSecrets();
		// Validate configuration and log helpful diagnostic if invalid
		if (!FirebaseConfigLoader.IsConfigValid())
		{
			System.Diagnostics.Trace.WriteLine("WARNING: Firebase configuration not found via env vars or assets. Android resources may still contain placeholders.");
		}

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
		builder.Services.AddTransientWithShellRoute<BookPage, BookViewModel>("BookPage");
		builder.Services.AddTransientWithShellRoute<CalibrePage, CalibrePageViewModel>("CalibrePage");
		builder.Services.AddTransientWithShellRoute<LoginPage, LoginPageViewModel>("LoginPage");
		return builder.Build();
	}

	static MauiAppBuilder RegisterFirebaseServices(this MauiAppBuilder builder)
	{
		builder.ConfigureLifecycleEvents(events =>
		{
#if ANDROID
			events.AddAndroid(android => android.OnCreate((activity, _) =>
			{
				// Ensure config loader runs and initialize Firebase programmatically so we never rely on Android resource strings
				try
				{
					FirebaseConfigLoader.InjectFirebaseSecrets();

					// Build FirebaseOptions from loaded configuration
					var appId = FirebaseConfigLoader.GetConfigValue("google_app_id");
					var apiKey = FirebaseConfigLoader.GetConfigValue("google_api_key");
					var databaseUrl = FirebaseConfigLoader.GetConfigValue("firebase_database_url");

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
					var clientId = FirebaseConfigLoader.GetConfigValue("default_web_client_id");
						
				}
				catch (Exception ex)
				{
					System.Diagnostics.Trace.WriteLine($"DEBUG: Firebase startup error: {ex.Message}");
				}
			}));
#endif
		});

		return builder;
	}
}