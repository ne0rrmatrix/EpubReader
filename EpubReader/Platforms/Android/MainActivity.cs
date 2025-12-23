using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using Android.Views.InputMethods;
using Android.Widget;
using Plugin.Firebase.Auth.Google;

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

		var serviceProvider = IPlatformApplication.Current?.Services ?? throw new InvalidOperationException("Unable to retrieve service provider");
		var folderPickerService = serviceProvider.GetService<IFolderPicker>() as FolderPicker;
		folderPickerService?.OnActivityResult(requestCode, resultCode, data);

		// Only forward to the Google plugin when the requestCode matches the Google Sign-In flow.
		// Default Google Sign-In request code is typically 9001, but confirm in your sign-in starter.
		const int GoogleSignInRequestCode = 9001;
		if (requestCode == GoogleSignInRequestCode)
		{
			try
			{
				FirebaseAuthGoogleImplementation.HandleActivityResultAsync(requestCode, resultCode, data);
			}
			catch (Exception ex)
			{
				// Prevent plugin null-ref or config errors from crashing the app; log for diagnostics.
				System.Diagnostics.Trace.TraceWarning($"Firebase Google HandleActivityResultAsync ignored: {ex.GetType().Name}: {ex.Message}");
			}
		}
		else
		{
			// Optional: helpful telemetry during debugging
			System.Diagnostics.Trace.TraceWarning($"OnActivityResult: Ignored by Google plugin. requestCode={requestCode}, resultCode={resultCode}");
		}

		ActivityResult?.Invoke(requestCode, resultCode, data);
	}

	public override bool DispatchTouchEvent(MotionEvent? e)
	{
		if (e!.Action == MotionEventActions.Down)
		{
			var focusedElement = CurrentFocus;
			if (focusedElement is EditText editText)
			{
				var editTextLocation = new int[2];
				editText.GetLocationOnScreen(editTextLocation);
				var clearTextButtonWidth = 100; // syncfusion clear button at the end of the control
				var editTextRect = new Rect(editTextLocation[0], editTextLocation[1], editText.Width + clearTextButtonWidth, editText.Height);
				//var editTextRect = editText.GetGlobalVisibleRect(editTextRect);  //not working in MAUI, always returns 0,0,0,0
				var touchPosX = (int)e.RawX;
				var touchPosY = (int)e.RawY;
				if (!editTextRect.Contains(touchPosX, touchPosY))
				{
					editText.ClearFocus();
					var inputService = GetSystemService(Context.InputMethodService) as InputMethodManager;
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