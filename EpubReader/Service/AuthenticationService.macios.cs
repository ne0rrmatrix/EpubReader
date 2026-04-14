using System.Diagnostics;

namespace EpubReader.Service;

public partial class AuthenticationService
{
	public Task<string> SignInWithGooglePlatformAsync(CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();
		Trace.TraceWarning("Google sign-in on iOS/MacCatalyst is intentionally blocked until GoogleService-Info.plist is added and native Firebase config is completed.");
		throw new InvalidOperationException("Google sign-in on iOS/MacCatalyst requires GoogleService-Info.plist and native Firebase configuration before the modern SDK can be enabled.");
	}
}