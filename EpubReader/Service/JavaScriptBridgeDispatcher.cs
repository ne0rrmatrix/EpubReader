using System.Diagnostics;
using EpubReader.Converter;
using EpubReader.Util;

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

		if (!BookPageJsMessage.TryParse(normalizedPayload, out var message))
		{
			Trace.TraceWarning($"Ignoring malformed reader bridge payload from {source}: {ShortenForTrace(normalizedPayload)}");
			return;
		}

		if (message.Action == ReaderBridgeAction.Unknown)
		{
			Trace.TraceWarning($"Ignoring unsupported reader bridge action '{message.ActionName}' from {source}: {ShortenForTrace(normalizedPayload)}");
			return;
		}

		coordinator.Publish(message, source, normalizedPayload);
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

	static string ShortenForTrace(string payload)
	{
		const int maxLength = 300;
		if (payload.Length <= maxLength)
		{
			return payload;
		}

		return payload[..maxLength] + "…";
	}
}