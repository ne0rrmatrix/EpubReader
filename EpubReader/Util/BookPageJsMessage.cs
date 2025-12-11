namespace EpubReader.Util;

using System;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

/// <summary>
/// Represents a JavaScript message sent from the WebView to the native layer and
/// provides helpers to parse it and convert it to the internal "runcsharp" URL format.
/// </summary>
public sealed class BookPageJsMessage
{
    public string Action { get; init; } = string.Empty;

    public string? Href { get; init; }

    public int? Position { get; init; }

	public bool? Enabled { get; init; }

	public string? Message { get; init; }

	public static bool TryParse(string json, [NotNullWhen(true)] out BookPageJsMessage? message)
	{
		message = null;
		if (string.IsNullOrWhiteSpace(json))
		{
			return false;
		}

		try
		{
			using var doc = JsonDocument.Parse(json);
			var root = doc.RootElement;
			if (!root.TryGetProperty("action", out var actionElem))
			{
				return false;
			}

			var action = actionElem.GetString() ?? string.Empty;
			var href = root.TryGetProperty("href", out var hrefElem) ? hrefElem.GetString() : null;

			// Convert HTTP to HTTPS if needed
			if (!string.IsNullOrEmpty(href) && href.StartsWith("http://"))
			{
				href = "https://" + href[7..];
			}

			int? pos = null;
			if (root.TryGetProperty("position", out var posElem) && posElem.ValueKind == JsonValueKind.Number && posElem.TryGetInt32(out var p))
			{
				pos = p;
			}

			bool? enabled = null;
			if (root.TryGetProperty("enabled", out var enabledElem) &&
				(enabledElem.ValueKind == JsonValueKind.True || enabledElem.ValueKind == JsonValueKind.False))
			{
				enabled = enabledElem.GetBoolean();
			}

			string? messageText = null;
			if (root.TryGetProperty("message", out var messageElem))
			{
				messageText = messageElem.GetString();
			}

			message = new BookPageJsMessage
			{
				Action = action,
				Href = href,
				Position = pos,
				Enabled = enabled,
				Message = messageText
			};
			return true;
		}
		catch
		{
			// parse failure, return false with null message
			message = null;
			return false;
		}
	}

	/// <summary>
	/// Converts the parsed message into the internal "runcsharp" URL used by the native handler.
	/// Returns null if no mapping is available.
	/// </summary>
	public string? ToRuncsharpUrl()
    {
        var action = Action?.ToLowerInvariant() ?? string.Empty;
		return action switch
        {
            "jump" => Href is not null ? $"https://runcsharp.jump?{Href}" : null,
            "next" => "https://runcsharp.next?true",
            "prev" => "https://runcsharp.prev?true",
            "menu" => "https://runcsharp.menu?true",
            "pageload" => "https://runcsharp.pageLoad?true",
			"characterposition" => Position.HasValue ? $"https://runcsharp.characterposition?{Position.Value}" : null,
			"mediaoverlaytoggle" => Enabled.HasValue ? $"https://runcsharp.mediaoverlaytoggle?{Enabled.Value}" : null,
			"mediaoverlayplay" => "https://runcsharp.mediaoverlayplay?true",
			"mediaoverlaypause" => "https://runcsharp.mediaoverlaypause?true",
			"mediaoverlaynext" => "https://runcsharp.mediaoverlaynext?true",
			"mediaoverlayprev" => "https://runcsharp.mediaoverlayprev?true",
			"mediaoverlaylog" => !string.IsNullOrEmpty(Message) ? $"https://runcsharp.mediaoverlaylog?{Uri.EscapeDataString(Message)}" : null,
            _ => null,
        };
    }
}
