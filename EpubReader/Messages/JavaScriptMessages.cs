using CommunityToolkit.Mvvm.Messaging.Messages;

namespace EpubReader.Messages;
public class JavaScriptMessage(string message) : ValueChangedMessage<string>(message)
{
}
