using CommunityToolkit.Mvvm.Messaging.Messages;

namespace EpubReader.Messages;

/// <summary>
/// Represents a message indicating that a settings value has changed.
/// </summary>
/// <remarks>This message is used to notify subscribers about changes to a boolean settings value. It inherits
/// from <see cref="ValueChangedMessage{T}"/> with a type parameter of <see langword="bool"/>.</remarks>
/// <param name="value"></param>
public class SettingsMessage(bool value) : ValueChangedMessage<bool>(value)
{
}