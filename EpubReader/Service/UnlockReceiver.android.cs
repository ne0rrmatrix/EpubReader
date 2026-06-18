using Android.Content;

namespace EpubReader.Service;

public class UnlockReceiver : BroadcastReceiver
{
	public event EventHandler? ScreenUnlocked;
	public override void OnReceive(Context? context, Intent? intent)
	{
		if (intent?.Action == Intent.ActionUserPresent)
		{
			ScreenUnlocked?.Invoke(this, EventArgs.Empty);
		}
	}


}
