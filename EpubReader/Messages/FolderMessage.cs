using CommunityToolkit.Mvvm.Messaging.Messages;

namespace EpubReader.Messages;

/// <summary>
/// Represents a message that contains information about a folder.
/// </summary>
/// <remarks>This class is used to encapsulate folder information within a message, typically for use in messaging
/// systems where folder state changes need to be communicated. It inherits from <see cref="ValueChangedMessage{T}"/>
/// with <see cref="FolderInfo"/> as the type parameter, indicating that it carries folder-related data.</remarks>
/// <param name="folderInfo"></param>
public class FolderMessage(FolderInfo folderInfo) : ValueChangedMessage<FolderInfo>(folderInfo)
{
}