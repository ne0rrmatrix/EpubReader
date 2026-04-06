using EpubReader.Converter;

namespace EpubReader.Service;

public sealed class JavaScriptBridgeDispatcher(IReaderBridgeCoordinator coordinator) : IJavaScriptBridgeDispatcher
{
	readonly IReaderBridgeCoordinator coordinator = coordinator;

	public void Dispatch(string payload, JavaScriptBridgeSource source, bool isBase64Encoded = false, CancellationToken token = default)
	{
		token.ThrowIfCancellationRequested();
		if (string.IsNullOrWhiteSpace(payload))
		{
			return;
		}

		var normalizedPayload = isBase64Encoded
			? DecodePayload(payload)
			: payload;
		if (string.IsNullOrWhiteSpace(normalizedPayload))
		{
			return;
		}

		normalizedPayload = NormalizeJsonPayload(normalizedPayload);
		coordinator.Publish(normalizedPayload, source);
	}

	static string DecodePayload(string payload)
	{
		var decoded = Base64Decoder.DecodeFromBase64(payload);
		if (!string.IsNullOrWhiteSpace(decoded))
		{
			return decoded;
		}

		try
		{
			var bytes = Convert.FromBase64String(payload);
			return System.Text.Encoding.UTF8.GetString(bytes);
		}
		catch (FormatException)
		{
			return string.Empty;
		}
	}

	static string NormalizeJsonPayload(string payload)
	{
		if (!payload.TrimStart().StartsWith('{'))
		{
			return payload;
		}

		if (!BookPageJsMessage.TryParse(payload, out var message))
		{
			return payload;
		}

		return message.ToRuncsharpUrl() ?? payload;
	}
}
