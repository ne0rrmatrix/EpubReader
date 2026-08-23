using Foundation;
using UIKit;

namespace EpubReader.Service;

public partial class FolderPicker
{
	class PickerDelegate : UIDocumentPickerDelegate
	{

		public Action<NSUrl[]>? PickHandler { get; set; }

		public override void WasCancelled(UIDocumentPickerViewController controller)
			=> PickHandler?.Invoke(null!);

		public override void DidPickDocument(UIDocumentPickerViewController controller, NSUrl[] urls)
			=> PickHandler?.Invoke(urls);

		public override void DidPickDocument(UIDocumentPickerViewController controller, NSUrl url)
			=> PickHandler?.Invoke([url]);
	}
}