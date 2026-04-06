namespace EpubReader.Service;

public sealed class ReaderBridgeMessageEventArgs : EventArgs
{
	public ReaderBridgeMessageEventArgs(string payload, JavaScriptBridgeSource source)
	{
		Payload = payload;
		Source = source;
	}

	public string Payload { get; }
	public JavaScriptBridgeSource Source { get; }
}
