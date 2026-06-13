using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using Android.Views.InputMethods;
using Android.Widget;

namespace EpubReader;

[Activity(Theme = "@style/Maui.SplashTheme", ResizeableActivity = true, MainLauncher = true, LaunchMode = LaunchMode.SingleTask, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
[IntentFilter(
	[Intent.ActionView],
	Categories = new[] { Intent.CategoryDefault, Intent.CategoryBrowsable },
	DataScheme = "https",
	DataHost = "epubreader-a03f6.firebaseapp.com",
	DataPathPrefix = "/__/auth")]

public class MainActivity : MauiAppCompatActivity
{
	public static event Action<int, Result, Intent?>? ActivityResult;

	protected override void OnCreate(Bundle? savedInstanceState)
	{
		base.OnCreate(savedInstanceState);
	}

	protected override void OnActivityResult(int requestCode, Result resultCode, Intent? data)
	{
		base.OnActivityResult(requestCode, resultCode, data);

		IServiceProvider serviceProvider = IPlatformApplication.Current?.Services ?? throw new InvalidOperationException("Unable to retrieve service provider");
		FolderPicker? folderPickerService = serviceProvider.GetService<IFolderPicker>() as FolderPicker;
		folderPickerService?.OnActivityResult(requestCode, resultCode, data);

		ActivityResult?.Invoke(requestCode, resultCode, data);
	}

	public override bool DispatchTouchEvent(MotionEvent? e)
	{
		if (e!.Action == MotionEventActions.Down)
		{
			Android.Views.View? focusedElement = CurrentFocus;
			if (focusedElement is EditText editText)
			{
				int[] editTextLocation = new int[2];
				editText.GetLocationOnScreen(editTextLocation);
				int clearTextButtonWidth = 100; // syncfusion clear button at the end of the control
				Rect editTextRect = new(editTextLocation[0], editTextLocation[1], editText.Width + clearTextButtonWidth, editText.Height);
				//var editTextRect = editText.GetGlobalVisibleRect(editTextRect);  //not working in MAUI, always returns 0,0,0,0
				int touchPosX = (int)e.RawX;
				int touchPosY = (int)e.RawY;
				if (!editTextRect.Contains(touchPosX, touchPosY))
				{
					editText.ClearFocus();
					InputMethodManager? inputService = GetSystemService(Context.InputMethodService) as InputMethodManager;
					inputService?.HideSoftInputFromWindow(editText.WindowToken, 0);
				}
			}
		}
		return base.DispatchTouchEvent(e);
	}

	public override void OnRequestPermissionsResult(
	   int requestCode,
	   string[] permissions,
	   Permission[] grantResults)
	{
		Platform.OnRequestPermissionsResult(requestCode, permissions, grantResults);
		base.OnRequestPermissionsResult(requestCode, permissions, grantResults);
	}
}