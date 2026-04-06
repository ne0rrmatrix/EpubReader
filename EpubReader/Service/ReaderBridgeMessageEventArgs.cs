namespace EpubReader.Service;

public sealed class ReaderBridgeMessageEventArgs : EventArgs
{
	public ReaderBridgeMessageEventArgs(BookPageJsMessage message, JavaScriptBridgeSource source, string rawPayload)
	{
		Message = message;
		Source = source;
		RawPayload = rawPayload;
	}

	public BookPageJsMessage Message { get; }
	public JavaScriptBridgeSource Source { get; }
	public string RawPayload { get; }
}
