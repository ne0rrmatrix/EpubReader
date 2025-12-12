using Android.Webkit;
using EpubReader.Converter;
using Java.Interop;

namespace EpubReader.Controls;

public class JSBridge : Java.Lang.Object
{
	[JavascriptInterface]
	[Export("sendMessageToCSharp")] // This is the name JavaScript will use
	public static void SendMessageToCSharp(string message)
	{
		if (string.IsNullOrEmpty(message))
		{
			System.Diagnostics.Trace.TraceWarning("JSBridge.postMessage called with null or empty message");
			return;
		}
		var json = Base64Decoder.DecodeFromBase64(message);
		if (json is null)
		{
			System.Diagnostics.Trace.TraceWarning("JSBridge.postMessage failed to decode base64 message");
			return;
		}
		Microsoft.Maui.Controls.Application.Current?.Dispatcher.Dispatch(() => WeakReferenceMessenger.Default.Send(new JavaScriptMessage(json)));
	}
}