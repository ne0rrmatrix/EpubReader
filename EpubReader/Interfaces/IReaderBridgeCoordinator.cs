namespace EpubReader.Interfaces;

public interface IReaderBridgeCoordinator
{
	event EventHandler<ReaderBridgeMessageEventArgs>? MessageReceived;

	void Publish(BookPageJsMessage message, JavaScriptBridgeSource source, string rawPayload);
}
