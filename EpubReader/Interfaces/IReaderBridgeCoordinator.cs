namespace EpubReader.Interfaces;

public interface IReaderBridgeCoordinator
{
	event EventHandler<ReaderBridgeMessageEventArgs>? MessageReceived;

	void Publish(string payload, JavaScriptBridgeSource source);
}
