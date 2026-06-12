namespace EpubReader.Interfaces;

public interface IAuthentication
{
	event EventHandler<bool>? AuthStateChanged;
	Task<string> SignInWithGooglePlatformAsync(CancellationToken cancellationToken);
	Task<bool> NeedsAuthenticationAsync(CancellationToken cancellationToken = default);
	Task<bool> IsLocalOnlyModeAsync(CancellationToken cancellationToken = default);
	Task SetLocalOnlyModeAsync(CancellationToken cancellationToken = default);
	Task<string> SignInWithGoogleAsync(CancellationToken cancellationToken = default);
	Task<string> GetAuthTokenAsync(CancellationToken cancellationToken = default);
	Task<bool> IsAuthenticatedAsync(CancellationToken cancellationToken = default);
	Task<string> GetCurrentUserIdAsync(CancellationToken cancellationToken = default);
	Task<string> GetCurrentUserEmailAsync(CancellationToken cancellationToken = default);
	Task SignOutAsync(CancellationToken cancellationToken = default);
}
