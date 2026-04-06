namespace EpubReader.Service;

public sealed class ReaderBridgeCoordinator : IReaderBridgeCoordinator
{
	public event EventHandler<ReaderBridgeMessageEventArgs>? MessageReceived;

	public void Publish(BookPageJsMessage message, JavaScriptBridgeSource source, string rawPayload)
	{
		if (message is null || string.IsNullOrWhiteSpace(rawPayload))
		{
			return;
		}

		MessageReceived?.Invoke(this, new ReaderBridgeMessageEventArgs(message, source, rawPayload));
	}
}
