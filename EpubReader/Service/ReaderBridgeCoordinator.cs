namespace EpubReader.Service;

public sealed class ReaderBridgeCoordinator : IReaderBridgeCoordinator
{
	public event EventHandler<ReaderBridgeMessageEventArgs>? MessageReceived;

	public void Publish(string payload, JavaScriptBridgeSource source)
	{
		if (string.IsNullOrWhiteSpace(payload))
		{
			return;
		}

		MessageReceived?.Invoke(this, new ReaderBridgeMessageEventArgs(payload, source));
	}
}
