using CommunityToolkit.Mvvm.Messaging.Messages;

namespace EpubReader.Messages;

/// <summary>
/// Represents a message containing JavaScript code or content.
/// </summary>
/// <remarks>This class is used to encapsulate a string message that typically contains JavaScript code or related
/// content. It inherits from <see cref="ValueChangedMessage{T}"/> with a string type parameter, allowing it to be used
/// in scenarios where message passing or notification of JavaScript content changes is required.</remarks>
/// <param name="message"></param>
public class JavaScriptMessage(string message) : ValueChangedMessage<string>(message)
{
}