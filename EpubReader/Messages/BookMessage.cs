using CommunityToolkit.Mvvm.Messaging.Messages;

namespace EpubReader.Messages;

/// <summary>
/// Represents a message that indicates a change in the state of a <see cref="Book"/> object.
/// </summary>
/// <remarks>This class is used to notify subscribers about changes to a <see cref="Book"/> instance. It inherits
/// from <see cref="ValueChangedMessage{T}"/> to provide the updated <see cref="Book"/> value.</remarks>
/// <param name="value"></param>
public class BookMessage(Book value) : ValueChangedMessage<Book>(value)
{
}