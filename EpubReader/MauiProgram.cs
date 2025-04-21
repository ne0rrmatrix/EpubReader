using System.Reflection.Metadata;
using CommunityToolkit.Maui;
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
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform;
using Syncfusion.Maui.Toolkit.Hosting;

using LoggerFactory = MetroLog.LoggerFactory;
using LogLevel = MetroLog.LogLevel;
using Microsoft.Maui.Controls;
using CommunityToolkit.Mvvm.Messaging;
using EpubReader.Messages;
using Microsoft.Maui.Animations;
using System.Buffers.Text;
using System.Runtime.Versioning;
using System.Globalization;






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
#elif WINDOWS
		Microsoft.Maui.Handlers.WebViewHandler.Mapper.ModifyMapping(
	  nameof(Microsoft.UI.Xaml.Controls.WebView2),
	  async (handler, view, args) =>{ EpubReader.Util.WebViewExtensions.Initialize(handler); await handler.PlatformView.EnsureCoreWebView2Async(); });
#elif IOS || MACCATALYST
		Microsoft.Maui.Handlers.WebViewHandler.PlatformViewFactory = (handler) => 
		{
			var config = new WKWebViewConfiguration();

			// By default, setting inline media playback to allowed, including autoplay
			// and picture in picture, since these things MUST be set during the webview
			// creation, and have no effect if set afterwards.
			// A custom handler factory delegate could be set to disable these defaults
			// but if we do not set them here, they cannot be changed once the
			// handler's platform view is created, so erring on the side of wanting this
			// capability by default.
			if (OperatingSystem.IsMacCatalystVersionAtLeast(10) || OperatingSystem.IsIOSVersionAtLeast(10))
			{
				config.AllowsPictureInPictureMediaPlayback = true;
				config.AllowsInlineMediaPlayback = true;
				config.MediaTypesRequiringUserActionForPlayback = WKAudiovisualMediaTypes.None;
			}
			config.DefaultWebpagePreferences!.AllowsContentJavaScript = true;
			config.SetUrlSchemeHandler(new CustomUrlSchemeHandler((WebViewHandler)handler), "app");
			return new CustomMauiWKWebView(CGRect.Empty,(WebViewHandler)handler, config)
			{
				Inspectable = true,
				NavigationDelegate = new CustomWebViewNavigationDelegate((WebViewHandler)handler),
			};
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
        builder.Services.AddSingleton<AppShell>();
        builder.Services.AddSingleton<BaseViewModel>();

		builder.Services.AddTransientPopup<SettingsPage, SettingsPageViewModel>();
		builder.Services.AddTransientWithShellRoute<LibraryPage, LibraryViewModel>("//LibraryPage");
		builder.Services.AddTransientWithShellRoute<BookPage, BookViewModel>("//BookPage");
		return builder.Build();
    }
}
#if IOS || MACCATALYST
class CustomUrlSchemeHandler :NSObject, IWKUrlSchemeHandler
{
	readonly WebViewHandler handler;
	public CustomUrlSchemeHandler(WebViewHandler handler)
	{
		this.handler = handler ?? throw new ArgumentNullException(nameof(handler));
	}
	[Export("webView:startURLSchemeTask:")]
	[SupportedOSPlatform("ios11.0")]
	public void StartUrlSchemeTask(WKWebView webView, IWKUrlSchemeTask urlSchemeTask)
	{
		var url = urlSchemeTask.Request.Url.AbsoluteString ?? "";
		var baseUrl = NSUrl.FromString("app://demo/");
		if (url.StartsWith("app://"))
		{
			var path = url.Substring("app://demo/".Length);
			var filename = Path.GetFileName(path) ?? throw new InvalidOperationException("url is null");
			System.Diagnostics.Debug.WriteLine($"fileName: {filename}");
			var mimeType = FileService.GetMimeType(filename);
			var text = StreamExtensions.Instance?.Content(filename);
			if (text is not null && StreamExtensions.IsText(filename))
			{
				System.Diagnostics.Debug.WriteLine($"File: {filename} mimeType: {mimeType} url: {url} baseUrl: {baseUrl}");
				var stream = StreamExtensions.GetStream(text) ?? throw new InvalidOperationException("stream is null");
				var data = NSData.FromStream(stream) ?? throw new InvalidOperationException("data is null");
				
				using var dic = new NSMutableDictionary<NSString, NSString>();
				if (mimeType is not null)
				{
					dic[(NSString)"Content-Type"] = (NSString)mimeType;
				}
				// Disable local caching which would otherwise prevent user scripts from executing correctly.
				dic[(NSString)"Cache-Control"] = (NSString)"no-cache, max-age=0, must-revalidate, no-store";
				dic[(NSString)"Content-Length"] = (NSString)data.Length.ToString(CultureInfo.InvariantCulture);

				using var response = new NSHttpUrlResponse(urlSchemeTask.Request.Url, 200, "HTTP/1.1", dic);
				// 2.a. Send the response
				urlSchemeTask.DidReceiveResponse(response);
				// 2.c. Send the data
				urlSchemeTask.DidReceiveData(data);

				// 2.d. Finish the task
				urlSchemeTask.DidFinish();
			}
			else
			{
				System.Diagnostics.Debug.WriteLine("File not text file");
			}
			var binary = StreamExtensions.Instance?.ByteContent(filename);
			if (binary is not null && StreamExtensions.IsBinary(filename))
			{
				System.Diagnostics.Debug.WriteLine($"File: {filename} mimeType: {mimeType} url: {url} baseUrl: {baseUrl}");
				var stream = StreamExtensions.GetStream(binary) ?? throw new InvalidOperationException("stream is null");
				var data = NSData.FromStream(stream) ?? throw new InvalidOperationException("data is null");
				using var dic = new NSMutableDictionary<NSString, NSString>();
				if (mimeType is not null)
				{
					dic[(NSString)"Content-Type"] = (NSString)mimeType;
				}
				// Disable local caching which would otherwise prevent user scripts from executing correctly.
				dic[(NSString)"Cache-Control"] = (NSString)"no-cache, max-age=0, must-revalidate, no-store";
				dic[(NSString)"Content-Length"] = (NSString)data.Length.ToString(CultureInfo.InvariantCulture);

				using var response = new NSHttpUrlResponse(urlSchemeTask.Request.Url, 200, "HTTP/1.1", dic);
				// 2.a. Send the response
				urlSchemeTask.DidReceiveResponse(response);
				// 2.c. Send the data
				urlSchemeTask.DidReceiveData(data);

				// 2.d. Finish the task
				urlSchemeTask.DidFinish();
			}
		}
	}
}

#endif