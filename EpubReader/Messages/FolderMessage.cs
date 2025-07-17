using CommunityToolkit.Mvvm.Messaging.Messages;
using EpubReader.Models;

namespace EpubReader.Messages;
public class FolderMessage(FolderInfo folderInfo) : ValueChangedMessage<FolderInfo>(folderInfo)
{
}
