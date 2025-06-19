using CommunityToolkit.Mvvm.Messaging.Messages;

namespace EpubReader.Messages;
public class SettingsMessage(bool value) : ValueChangedMessage<bool>(value)
{
}
