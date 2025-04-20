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

#if IOS || MACCATALYST
using CoreGraphics;
using Foundation;
using UIKit;
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
			WebKit.WKWebViewConfiguration config = MauiWKWebView.CreateConfiguration();
			config.Preferences.JavaScriptEnabled = true;
			config.Preferences.JavaScriptCanOpenWindowsAutomatically = true;
			config.ApplicationNameForUserAgent = "EpubReader/1.0.0";
			CustomWebViewNavigationDelegate navigationDelegate = new((WebViewHandler)handler);
			
			var webView = new CustomMauiWKWebView(CGRect.Empty,(WebViewHandler)handler, config)
			{
				NavigationDelegate = navigationDelegate
			};
			System.Diagnostics.Trace.WriteLine($"WebViewHandler: {webView}");
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
        builder.Services.AddSingleton<AppShell>();
        builder.Services.AddSingleton<BaseViewModel>();

		builder.Services.AddTransientPopup<SettingsPage, SettingsPageViewModel>();
		builder.Services.AddTransientWithShellRoute<LibraryPage, LibraryViewModel>("//LibraryPage");
		builder.Services.AddTransientWithShellRoute<BookPage, BookViewModel>("//BookPage");
		return builder.Build();
    }
}
#if IOS || MACCATALYST
public class CustomMauiWKWebView : MauiWKWebView
{
	//readonly WeakReference<WebViewHandler> handler;
	readonly StreamExtensions streamExtensions = Microsoft.Maui.Controls.Application.Current?.Windows[0].Page?.Handler?.MauiContext?.Services.GetRequiredService<StreamExtensions>() ?? throw new InvalidOperationException();

	public CustomMauiWKWebView(CGRect frame, WebViewHandler handler, WebKit.WKWebViewConfiguration configuration) : base(frame, handler, configuration)
	{
		_ = handler ?? throw new ArgumentNullException(nameof(handler));
		//this.handler = new WeakReference<WebViewHandler>(handler);
		BackgroundColor = UIColor.Clear;
		AutosizesSubviews = true;
		System.Diagnostics.Trace.WriteLine($"CustomMauiWKWebView: {this}");
	}

	public new void LoadUrl(string? url)
	{
		if (string.IsNullOrEmpty(url))
		{
			return;
		}
		System.Diagnostics.Trace.WriteLine($"LoadUrl: {url}");
		if (url.Contains("runcsharp"))
		{
			var urlParts = url.Split('.');
			var funcToCall = urlParts[1].Split("?");
			var methodName = funcToCall[0][..^1];
			if (methodName.Contains("next", StringComparison.CurrentCultureIgnoreCase))
			{
				WeakReferenceMessenger.Default.Send(new JavaScriptMessage("next"));
			}
			if (methodName.Contains("prev", StringComparison.CurrentCultureIgnoreCase))
			{
				WeakReferenceMessenger.Default.Send(new JavaScriptMessage("prev"));
			}
			if (methodName.Contains("pageLoad", StringComparison.CurrentCultureIgnoreCase))
			{
				WeakReferenceMessenger.Default.Send(new JavaScriptMessage("pageLoad"));
			}
			return;
		}
		try
		{
			var filename = Path.GetFileName(url);
			var mimeType = FileService.GetMimeType(filename);
			var text = streamExtensions.Content(filename);
			if (text is not null && StreamExtensions.IsText(filename))
			{
				System.Diagnostics.Trace.WriteLine($"LoadUrl: {url} - {filename}");
				var stream = StreamExtensions.GetStream(text);
				var nsData = NSData.FromStream(stream) ?? throw new InvalidOperationException("Stream cannot be null");
				LoadData(nsData, mimeType, string.Empty, new NSUrl(url));
				return;
			}
			var binary = streamExtensions.ByteContent(filename);
			if (binary is not null && StreamExtensions.IsBinary(filename))
			{
				System.Diagnostics.Trace.WriteLine($"LoadUrl: {url} - {filename}");
				var stream = StreamExtensions.GetStream(binary);
				var nsData = NSData.FromStream(stream) ?? throw new InvalidOperationException("Stream cannot be null");
				LoadData(nsData, mimeType, string.Empty, new NSUrl(url));
				return;
			}
			/*
			var file = Path.GetFileNameWithoutExtension(url);
			var ext = Path.GetExtension(url);

			var nsUrl = NSBundle.MainBundle.GetUrlForResource(file, ext);

			if (nsUrl == null)
			{
				return;
			}
			var nsData = NSData.FromString(string.Empty, NSStringEncoding.UTF8);
			//LoadFileUrl(nsUrl, nsUrl);
			LoadData(nsData, "text/html", string.Empty, nsUrl);
			return;
			*/
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine(ex.StackTrace ?? ex.Message ?? ex.InnerException?.Message ?? ex.InnerException?.InnerException?.Message ?? ex.Message);
		}
		base.LoadUrl(url);
	}

	
}
#endif