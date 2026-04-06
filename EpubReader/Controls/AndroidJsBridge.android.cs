using Android.Webkit;
using EpubReader.Converter;
using Java.Interop;

namespace EpubReader.Controls;

public class JSBridge : Java.Lang.Object
{
	static IJavaScriptBridgeDispatcher? GetDispatcher()
	{
		return Microsoft.Maui.Controls.Application.Current?.Windows[0].Page?.Handler?.MauiContext?.Services.GetService<IJavaScriptBridgeDispatcher>();
	}

	[JavascriptInterface]
	[Export("sendMessageToCSharp")] // This is the name JavaScript will use
	public static void SendMessageToCSharp(string message)
	{
		if (string.IsNullOrEmpty(message))
		{
			System.Diagnostics.Trace.TraceWarning("JSBridge.postMessage called with null or empty message");
			return;
		}
		var dispatcher = GetDispatcher();
		if (dispatcher is null)
		{
			System.Diagnostics.Trace.TraceWarning("JSBridge.postMessage could not resolve bridge dispatcher");
			return;
		}

		dispatcher.Dispatch(message, JavaScriptBridgeSource.Android, isBase64Encoded: true);
	}
}