
using CommunityToolkit.Mvvm.Messaging.Messages;

namespace EpubReader.Messages;
/// <summary>
/// Initializes a new instance of the <see cref="CalibreMessage"/> class with the specified value.
/// </summary>
/// <param name="value">The value indicating the state of Calibre integration.</param>
public class CalibreMessage(bool value) : ValueChangedMessage<bool>(value)
{
}
