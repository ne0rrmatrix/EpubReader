namespace EpubReader.Service;

public sealed class ReaderBridgeMessageEventArgs(BookPageJsMessage message, JavaScriptBridgeSource source, string rawPayload) : EventArgs
{
	public BookPageJsMessage Message { get; } = message;
	public JavaScriptBridgeSource Source { get; } = source;
	public string RawPayload { get; } = rawPayload;
}