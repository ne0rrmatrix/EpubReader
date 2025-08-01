using View = Android.Views.View;

namespace EpubReader.Controls;

class LongClickListener : Java.Lang.Object, View.IOnLongClickListener
{

    public LongClickListener()
    {
    }

    public bool OnLongClick(View? v)
    {
		if (v is null)
		{
			System.Diagnostics.Trace.TraceInformation("Long click detected on a null view.");
			return false; // No view to handle the long click
		}
		return true; // Indicate that the long click was handled
	}
}