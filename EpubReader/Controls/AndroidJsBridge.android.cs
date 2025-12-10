using System.Text;
using Android.Webkit;
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
		var bytes = Convert.FromBase64String(message);
		var json = Encoding.UTF8.GetString(bytes);
		Microsoft.Maui.Controls.Application.Current?.Dispatcher.Dispatch(() =>
		{
			WeakReferenceMessenger.Default.Send(new JavaScriptMessage(json));
		});
    }
}
