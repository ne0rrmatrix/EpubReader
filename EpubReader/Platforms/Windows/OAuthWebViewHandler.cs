using System.Diagnostics;
using Microsoft.UI.Xaml.Controls;
using WinUIWindow = Microsoft.UI.Xaml.Window;
using WinUIApplication = Microsoft.UI.Xaml.Application;
using WinUIGrid = Microsoft.UI.Xaml.Controls.Grid;
using WinUIButton = Microsoft.UI.Xaml.Controls.Button;
using WinUIRowDefinition = Microsoft.UI.Xaml.Controls.RowDefinition;
using WinUIGridLength = Microsoft.UI.Xaml.GridLength;
using WinUIGridUnitType = Microsoft.UI.Xaml.GridUnitType;
using WinUIHorizontalAlignment = Microsoft.UI.Xaml.HorizontalAlignment;
using WinUIThickness = Microsoft.UI.Xaml.Thickness;

namespace EpubReader.Platforms.Windows;

/// <summary>
/// Handles OAuth authentication flow using WebView2 on Windows platform.
/// </summary>
public static class OAuthWebViewHandler
{
	static TaskCompletionSource<Uri>? authCompletionSource;
	static WinUIWindow? authWindow;

	/// <summary>
	/// Opens a WebView window to handle OAuth authentication flow.
	/// </summary>
	/// <param name="authUri">The OAuth authorization URL to navigate to.</param>
	/// <param name="callbackUrlScheme">The URL scheme to intercept for the callback (e.g., "http://localhost").</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The callback URI with authentication results.</returns>
	public static async Task<Uri> AuthenticateAsync(Uri authUri, string callbackUrlScheme, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();

		// Clean up any existing auth window
		CloseAuthWindow();

		authCompletionSource = new TaskCompletionSource<Uri>();

		// Register cancellation
		using var registration = cancellationToken.Register(() =>
		{
			authCompletionSource?.TrySetCanceled(cancellationToken);
			CloseAuthWindow();
		});

		try
		{
			// Create and show the auth window on the UI thread
			var dispatcherQueue = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
			dispatcherQueue.TryEnqueue(() =>
			{
				CreateAuthWindow(authUri, callbackUrlScheme);
			});

			// Wait for authentication to complete
			var result = await authCompletionSource.Task;
			return result;
		}
		finally
		{
			CloseAuthWindow();
		}
	}

	static void CreateAuthWindow(Uri authUri, string callbackUrlScheme)
	{
		// Create a new window for authentication
		authWindow = new WinUIWindow
		{
			Title = "Sign in with Google",
		};

		// Get the native window handle and set size
		var windowHandle = WinRT.Interop.WindowNative.GetWindowHandle(authWindow);
		var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(windowHandle);
		var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);
		
		// Set window size - larger to fit Google sign-in page properly
		appWindow.Resize(new global::Windows.Graphics.SizeInt32 { Width = 600, Height = 900 });

		// Create WebView2
		var webView = new WebView2
		{
			Source = authUri
		};

		// Handle navigation events
		webView.NavigationStarting += (sender, args) =>
		{
			Trace.TraceInformation($"WebView navigating to: {args.Uri}");

			// Check if this is the callback URL
			if (args.Uri.StartsWith(callbackUrlScheme, StringComparison.OrdinalIgnoreCase))
			{
				Trace.TraceInformation("OAuth callback detected");
				args.Cancel = true;

				// Parse the callback URL and complete the authentication
				var callbackUri = new Uri(args.Uri);
				authCompletionSource?.TrySetResult(callbackUri);
			}
		};

		// Handle navigation failures
		webView.NavigationCompleted += (sender, args) =>
		{
			if (!args.IsSuccess)
			{
				Trace.TraceError($"WebView navigation failed: {args.WebErrorStatus}");
				authCompletionSource?.TrySetException(
					new Exception($"Authentication navigation failed: {args.WebErrorStatus}"));
			}
		};

		// Create a grid with the WebView and a close button
		var grid = new WinUIGrid();
		grid.RowDefinitions.Add(new WinUIRowDefinition { Height = new WinUIGridLength(1, WinUIGridUnitType.Auto) });
		grid.RowDefinitions.Add(new WinUIRowDefinition { Height = new WinUIGridLength(1, WinUIGridUnitType.Star) });

		// Add a close button
		var closeButton = new WinUIButton
		{
			Content = "Cancel",
			HorizontalAlignment = WinUIHorizontalAlignment.Right,
			Margin = new WinUIThickness(10)
		};
		closeButton.Click += (s, e) =>
		{
			authCompletionSource?.TrySetCanceled();
		};
		WinUIGrid.SetRow(closeButton, 0);

		WinUIGrid.SetRow(webView, 1);

		grid.Children.Add(closeButton);
		grid.Children.Add(webView);

		authWindow.Content = grid;

		// Handle window closed event
		authWindow.Closed += (s, e) =>
		{
			if (authCompletionSource?.Task.IsCompleted == false)
			{
				authCompletionSource.TrySetCanceled();
			}
		};

		// Activate the window
		authWindow.Activate();
	}

	static void CloseAuthWindow()
	{
		if (authWindow is not null)
		{
			try
			{
				authWindow.Close();
			}
			catch (Exception ex)
			{
				Trace.TraceWarning($"Error closing auth window: {ex.Message}");
			}
			authWindow = null;
		}

		authCompletionSource = null;
	}

	/// <summary>
	/// Clears WebView2 browser data including cookies and cache.
	/// </summary>
	public static async Task ClearWebViewDataAsync()
	{
		try
		{
			// Get the user data folder for WebView2
			var userDataFolder = Path.Combine(FileSystem.AppDataDirectory, "WebView2");
			
			if (Directory.Exists(userDataFolder))
			{
				// Delete the entire WebView2 cache directory
				// This forces a fresh session on next login
				await Task.Run(() =>
				{
					try
					{
						Directory.Delete(userDataFolder, recursive: true);
						Trace.TraceInformation("WebView2 cache directory deleted");
					}
					catch (IOException ioEx)
					{
						// Files might be in use, try to delete specific subdirectories
						Trace.TraceWarning($"Could not delete entire WebView2 folder: {ioEx.Message}");
						
						// Try to delete the Cookies and Cache folders specifically
						var cookiesPath = Path.Combine(userDataFolder, "Default", "Cookies");
						var cachePath = Path.Combine(userDataFolder, "Default", "Cache");
						
						if (File.Exists(cookiesPath))
						{
							File.Delete(cookiesPath);
						}
						
						if (Directory.Exists(cachePath))
						{
							Directory.Delete(cachePath, recursive: true);
						}
					}
				});

				Trace.TraceInformation("WebView2 browsing data cleared");
			}
		}
		catch (Exception ex)
		{
			Trace.TraceWarning($"Failed to clear WebView2 data: {ex.Message}");
			// Don't throw - this is a cleanup operation
		}
	}
}
