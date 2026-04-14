using Android.Webkit;
using EpubReader.Converter;
using Java.Interop;

namespace EpubReader.Controls;

public class JSBridge(IJavaScriptBridgeDispatcher dispatcher) : Java.Lang.Object
{
	readonly IJavaScriptBridgeDispatcher dispatcher = dispatcher;

	[JavascriptInterface]
	[Export("sendMessageToCSharp")] // This is the name JavaScript will use
	public void SendMessageToCSharp(string message)
	{
		if (string.IsNullOrEmpty(message))
		{
			System.Diagnostics.Trace.TraceWarning("JSBridge.postMessage called with null or empty message");
			return;
		}

		dispatcher.Dispatch(message, JavaScriptBridgeSource.Android, isBase64Encoded: true);
	}
}