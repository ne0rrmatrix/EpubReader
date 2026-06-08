using System.Diagnostics;
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
		byte[]? bytes = null;
		try
		{
			bytes = Convert.FromBase64String(payload);
		}
		catch (FormatException)
		{
			// Payload is not base64-encoded — treat it as plain text.
		}

		if (bytes is not null)
		{
			return System.Text.Encoding.UTF8.GetString(bytes);
		}

		// Fallback: return the raw payload as plain text (e.g. JSON from JS bridge).
		return payload;
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