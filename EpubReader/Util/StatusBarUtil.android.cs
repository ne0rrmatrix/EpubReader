using System.Text.RegularExpressions;
using Android.Content;
using Android.OS;
using Android.Util;
using Android.Views;
using AndroidX.Annotations;
using Java.Lang;
using Java.Lang.Reflect;

namespace EpubReader.Util;

public class StatusBarUtil
{
	public static int DEFAULT_COLOR = 0;
	public static float DEFAULT_ALPHA = 0;//Build.VERSION.SDK_INT >= Build.VERSION_CODES.LOLLIPOP ? 0.2f : 0.3f;

	//<editor-fold desc="Immersive">
	public static void Immersive(global::Android.App.Activity activity)
	{
		Immersive(activity, DEFAULT_COLOR, DEFAULT_ALPHA);
	}

	public static void Immersive(global::Android.App.Activity activity, int color, float alpha)
	{
		if (activity.Window is null)
		{
			return;
		}
		Immersive(activity.Window, color, alpha);
	}

	public static void Immersive(global::Android.App.Activity activity, int color)
	{
		if (activity.Window is null)
		{
			return;
		}
		Immersive(activity.Window, color, 1f);
	}

	public static void Immersive(global::Android.Views.Window window)
	{
		Immersive(window, DEFAULT_COLOR, DEFAULT_ALPHA);
	}

	public static void Immersive(global::Android.Views.Window window, int color)
	{
		Immersive(window, color, 1f);
	}

	public static void Immersive(global::Android.Views.Window window, int color, float alpha)
	{

		if (Build.VERSION.SdkInt >= BuildVersionCodes.Lollipop)
		{ // 21
			window.ClearFlags(global::Android.Views.WindowManagerFlags.TranslucentStatus);
			window.AddFlags(global::Android.Views.WindowManagerFlags.DrawsSystemBarBackgrounds);
			window.SetStatusBarColor(new global::Android.Graphics.Color(MixtureColor(color, alpha)));

			global::Android.Views.SystemUiFlags systemUiVisibility = (global::Android.Views.SystemUiFlags)window.DecorView.SystemUiVisibility;
			systemUiVisibility |= global::Android.Views.SystemUiFlags.LayoutFullscreen;
			systemUiVisibility |= global::Android.Views.SystemUiFlags.LayoutStable;
			window.DecorView.SystemUiVisibility = (global::Android.Views.StatusBarVisibility)systemUiVisibility;
		}
		else if (Build.VERSION.SdkInt >= BuildVersionCodes.Kitkat)
		{ // 19
			window.AddFlags(global::Android.Views.WindowManagerFlags.TranslucentStatus);
			SetTranslucentView((global::Android.Views.ViewGroup)window.DecorView, color, alpha);
		}
		else if (Build.VERSION.SdkInt >= BuildVersionCodes.JellyBean)
		{ // 16
			global::Android.Views.SystemUiFlags systemUiVisibility = (global::Android.Views.SystemUiFlags)window.DecorView.SystemUiVisibility;
			systemUiVisibility |= global::Android.Views.SystemUiFlags.LayoutFullscreen;
			systemUiVisibility |= global::Android.Views.SystemUiFlags.LayoutStable;
			window.DecorView.SystemUiVisibility = (global::Android.Views.StatusBarVisibility)systemUiVisibility;
		}
	}
	//</editor-fold>

	//<editor-fold desc="DarkMode">
	public static void DarkMode(global::Android.App.Activity activity, bool dark)
	{
		if (activity.Window is null)
		{
			return;
		}

		if (IsFlyme4Later())
		{
			DarkModeForFlyme4(activity.Window, dark);
		}
		else if (IsMIUI6Later())
		{
			DarkModeForMIUI6(activity.Window, dark);
		}
		else if (Build.VERSION.SdkInt >= BuildVersionCodes.M)
		{
			DarkModeForM(activity.Window, dark);
		}
	}

	/** set actionbar darkMode,font icon black(support MIUI6+ ,Flyme4+ ,Android M+) */
	public static void DarkMode(global::Android.App.Activity activity)
	{
		if (activity.Window is null)
		{
			return;
		}

		DarkMode(activity.Window, DEFAULT_COLOR, DEFAULT_ALPHA);
	}

	public static void DarkMode(global::Android.App.Activity activity, int color, float alpha)
	{
		if (activity.Window is null)
		{
			return;
		}

		DarkMode(activity.Window, color, alpha);
	}

	/** set actionbar darkMode,font icon black(support MIUI6+ ,Flyme4+ ,Android M+) */
	public static void DarkMode(global::Android.Views.Window window, int color, float alpha)
	{
		if (IsFlyme4Later())
		{
			DarkModeForFlyme4(window, true);
			Immersive(window, color, alpha);
		}
		else if (IsMIUI6Later())
		{
			DarkModeForMIUI6(window, true);
			Immersive(window, color, alpha);
		}
		else if (Build.VERSION.SdkInt >= BuildVersionCodes.M)
		{
			DarkModeForM(window, true);
			Immersive(window, color, alpha);
		}
		else if (Build.VERSION.SdkInt >= BuildVersionCodes.Kitkat)
		{ //19
			window.AddFlags(global::Android.Views.WindowManagerFlags.TranslucentStatus);
			SetTranslucentView((global::Android.Views.ViewGroup)window.DecorView, color, alpha);
		}
		//        if (Build.VERSION.SDK_INT >= 21) {
		//            window.clearFlags(WindowManager.LayoutParams.FLAG_TRANSLUCENT_STATUS);
		//            window.addFlags(WindowManager.LayoutParams.FLAG_DRAWS_SYSTEM_BAR_BACKGROUNDS);
		//            window.setStatusBarColor(Color.TRANSPARENT);
		//        } else if (Build.VERSION.SDK_INT >= 19) {
		//            window.addFlags(WindowManager.LayoutParams.FLAG_TRANSLUCENT_STATUS);
		//        }

		//        setTranslucentView((ViewGroup) window.getDecorView(), color, alpha);
	}

	//------------------------->

	/** android 6.0 set fontcolor */
	[RequiresApi(Api = (int)BuildVersionCodes.M)]
	static void DarkModeForM(global::Android.Views.Window window, bool dark)
	{
		//        window.clearFlags(WindowManager.LayoutParams.FLAG_TRANSLUCENT_STATUS);
		//        window.addFlags(WindowManager.LayoutParams.FLAG_DRAWS_SYSTEM_BAR_BACKGROUNDS);
		//        window.setStatusBarColor(Color.TRANSPARENT);

		global::Android.Views.SystemUiFlags systemUiVisibility = (SystemUiFlags)window.DecorView.SystemUiVisibility;
		if (dark)
		{
			systemUiVisibility |= SystemUiFlags.LightStatusBar;
		}
		else
		{
			systemUiVisibility &= ~SystemUiFlags.LightStatusBar;
		}
		window.DecorView.SystemUiVisibility = (StatusBarVisibility)systemUiVisibility;
	}

	/**
     * set Flyme4+ darkMode,darkMode fontcolor and icon black
     * http://open-wiki.flyme.cn/index.php?title=Flyme%E7%B3%BB%E7%BB%9FAPI
     */
	public static bool DarkModeForFlyme4(global::Android.Views.Window window, bool dark)
	{
		bool result = false;
		if (window is not null)
		{
			try
			{
				WindowManagerLayoutParams e = window.Attributes ?? throw new InvalidOperationException("");
				Java.Lang.Reflect.Field darkFlag = e.Class.GetDeclaredField("MEIZU_FLAG_DARK_STATUS_BAR_ICON") ?? throw new InvalidOperationException("");
				Java.Lang.Reflect.Field meizuFlags = e.Class.GetDeclaredField("meizuFlags") ?? throw new InvalidOperationException("");
				darkFlag.Accessible = true;
				meizuFlags.Accessible = true;
				int bit = darkFlag.GetInt(null);
				int value = meizuFlags.GetInt(e);
				if (dark)
				{
					value |= bit;
				}
				else
				{
					value &= ~bit;
				}

				meizuFlags.SetInt(e, value);
				window.Attributes = e;
				result = true;
			}
			catch (Java.Lang.Exception var8)
			{
				Log.Error("StatusBar", "darkIcon: failed");
			}
		}

		return result;
	}

	/**
     * set Flyme4+ darkMode,darkMode fontcolor and icon black
     * http://dev.xiaomi.com/doc/p=4769/
     */
	public static bool DarkModeForMIUI6(global::Android.Views.Window window, bool darkmode)
	{
		Class clazz = window.Class ?? throw new InvalidOperationException("");
		try
		{
			int darkModeFlag = 0;
			Class layoutParams = Class.ForName("android.view.MiuiWindowManager$LayoutParams") ?? throw new InvalidOperationException("");
			Java.Lang.Reflect.Field field = layoutParams.GetField("EXTRA_FLAG_STATUS_BAR_DARK_MODE") ?? throw new InvalidOperationException("");
			darkModeFlag = field.GetInt(layoutParams);
			Method extraFlagField = clazz.GetMethod("setExtraFlags", Class.FromType(typeof(Java.Lang.Integer)), Class.FromType(typeof(Java.Lang.Integer))) ?? throw new InvalidOperationException("");
			extraFlagField.Invoke(window, darkmode ? darkModeFlag : 0, darkModeFlag);
			return true;
		}
		catch (Java.Lang.Exception e)
		{
			e.PrintStackTrace();
			return false;
		}
	}

	/** \check if isFlyme4 + */
	public static bool IsFlyme4Later()
	{
		if (Build.Fingerprint is null)
		{
			return false;
		}
		if (Build.VERSION.Incremental is null)
		{
			return false;
		}
		if (Build.Display is null)
		{
			return false;
		}
		return Build.Fingerprint.Contains("Flyme_OS_4")
				|| Build.VERSION.Incremental.Contains("Flyme_OS_4")
				|| Regex.IsMatch(Build.Display, "Flyme OS [4|5]", RegexOptions.IgnoreCase);
	}

	/** check if MIUI6+ */
	public static bool IsMIUI6Later()
	{
		try
		{
			Class clz = Class.ForName("android.os.SystemProperties") ?? throw new InvalidOperationException("");
			Method mtd = clz.GetMethod("get", Class.FromType(typeof(Java.Lang.String))) ?? throw new InvalidOperationException("");
			string val = (string?)mtd?.Invoke(null, "ro.miui.ui.version.name"!) ?? throw new InvalidOperationException("");
			val = val.Replace("[vV]", "");
			int version = Integer.ParseInt(val);
			return version >= 6;
		}
		catch (Java.Lang.Exception e)
		{
			return false;
		}
	}
	//</editor-fold>


	/** add View paddingTop, */
	public static void SetPadding(Context context, global::Android.Views.View view)
	{
		if (Build.VERSION.SdkInt >= BuildVersionCodes.JellyBean)
		{ // 16
			view.SetPadding(view.PaddingLeft, view.PaddingTop + GetStatusBarHeight(context),
					view.PaddingRight, view.PaddingBottom);
		}
	}
	/** add paddingTop of View,  this value is height of actionbar */
	public static void SetPaddingSmart(Context context, global::Android.Views.View view)
	{
		if (Build.VERSION.SdkInt >= BuildVersionCodes.JellyBean)
		{ // 16
			ViewGroup.LayoutParams lp = view.LayoutParameters ?? throw new InvalidOperationException("");
			if (lp != null && lp.Height > 0)
			{
				lp.Height += GetStatusBarHeight(context);//
			}
			view.SetPadding(view.PaddingLeft, view.PaddingTop + GetStatusBarHeight(context),
					view.PaddingRight, view.PaddingBottom);
		}
	}

	/** add height and paddingTop of View,this value is height of actionbar.for ToolBar in Immersive */
	public static void SetHeightAndPadding(Context context, global::Android.Views.View view)
	{
		if (Build.VERSION.SdkInt >= BuildVersionCodes.JellyBean)
		{ // 16
			ViewGroup.LayoutParams lp = view.LayoutParameters ?? throw new InvalidOperationException("");
			lp.Height += GetStatusBarHeight(context);//
			view.SetPadding(view.PaddingLeft, view.PaddingTop + GetStatusBarHeight(context),
					view.PaddingRight, view.PaddingBottom);
		}
	}
	/** add MarginTop of View ，for WARP_CONTENT of other control*/
	public static void SetMargin(Context context, global::Android.Views.View view)
	{
		if (Build.VERSION.SdkInt >= BuildVersionCodes.JellyBean)
		{ // 16
			ViewGroup.LayoutParams lp = view.LayoutParameters ?? throw new InvalidOperationException("");
			if (lp is ViewGroup.MarginLayoutParams)
			{
				((ViewGroup.MarginLayoutParams)lp).TopMargin += GetStatusBarHeight(context);//
			}
			view.LayoutParameters = lp;
		}
	}
	/**
     * create TranslucentView
     */
	public static void SetTranslucentView(global::Android.Views.ViewGroup container, int color, float alpha)
	{
		if (Build.VERSION.SdkInt >= BuildVersionCodes.Kitkat)
		{ // 19
			int _mixtureColor = MixtureColor(color, alpha);
			global::Android.Views.View? translucentView = container.FindViewById(global::Android.Resource.Id.Custom);
			if (translucentView is null && _mixtureColor != 0 && container.Context is not null)
			{
				translucentView = new global::Android.Views.View(container.Context);
				translucentView.Id = global::Android.Resource.Id.Custom;
				ViewGroup.LayoutParams lp = new ViewGroup.LayoutParams(ViewGroup.LayoutParams.MatchParent, GetStatusBarHeight(container.Context)) ?? throw new InvalidOperationException("");
				container.AddView(translucentView, lp);
			}
			if (translucentView is not null)
			{
				translucentView.SetBackgroundColor(new global::Android.Graphics.Color(_mixtureColor));
			}
		}
	}

	public static int MixtureColor(int color, float alpha)
	{
		int a = (color & 0x000000) == 0 ? 0xff : color >> 24;
		return (color & 0xffffff) | (((int)(a * alpha)) << 24);
	}


	public static int GetStatusBarHeight(Context context)
	{
		int result = 24;
		int resId = context.Resources?.GetIdentifier("status_bar_height", "dimen", "android") ?? throw new InvalidOperationException("resId cannot be null");
		if (resId > 0)
		{
			result = context.Resources.GetDimensionPixelSize(resId);
		}
		else
		{
			result = (int)TypedValue.ApplyDimension(ComplexUnitType.Dip,
					result, global::Android.Content.Res.Resources.System?.DisplayMetrics);
		}
		return result;
	}
}
