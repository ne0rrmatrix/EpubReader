namespace EpubReader.Interfaces;

public interface IJavaScriptBridgeDispatcher
{
	void Dispatch(string payload, JavaScriptBridgeSource source, bool isBase64Encoded = false, CancellationToken token = default);
}