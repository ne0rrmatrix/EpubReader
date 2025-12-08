using Android.App;
using Android.Content;

namespace EpubReader.Platforms.Android;

/// <summary>
/// Platform-specific Google authentication handler for Android
/// Manages OAuth flow and activity result handling
/// </summary>
public static class GoogleAuthHandler
{
    static TaskCompletionSource<string>? redirectCompletionSource;

    /// <summary>
    /// Initiates OAuth redirect handling
    /// </summary>
    public static Task<string> HandleOAuthRedirect()
    {
        redirectCompletionSource = new TaskCompletionSource<string>();
        return redirectCompletionSource.Task;
    }

    /// <summary>
    /// Called from MainActivity.OnActivityResult to complete the OAuth flow
    /// </summary>
    public static void CompleteOAuthFlow(int requestCode, Result resultCode, Intent? data)
    {
        if (redirectCompletionSource is null)
        {
            return;
        }

        if (resultCode == Result.Ok && data?.Data is not null)
        {
            redirectCompletionSource.SetResult(data.Data.ToString() ?? string.Empty);
        }
        else
        {
            redirectCompletionSource.SetException(
                new System.OperationCanceledException("OAuth flow was cancelled"));
        }

        redirectCompletionSource = null;
    }
}