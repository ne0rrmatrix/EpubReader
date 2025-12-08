using System.Diagnostics;

namespace EpubReader.ViewModels;

public partial class LoginPageViewModel(AuthenticationService authenticationService) : BaseViewModel
{
	readonly AuthenticationService authenticationService = authenticationService;

	[ObservableProperty]
	public partial bool IsBusy { get; set; }

	[ObservableProperty]
	public partial bool IsNotBusy { get; set; } = true;

	[ObservableProperty]
	public partial string StatusMessage { get; set; } = string.Empty;

	[RelayCommand]
	async Task SignInWithGoogleAsync(CancellationToken cancellationToken = default)
	{
		if (IsBusy)
		{
			return;
		}

		try
		{
			IsBusy = true;
			IsNotBusy = false;
			StatusMessage = "Signing in with Google...";

			var userId = await authenticationService.SignInWithGoogleAsync(cancellationToken);

			if (!string.IsNullOrWhiteSpace(userId))
			{
				StatusMessage = "Sign in successful!";
				//await Shell.Current.GoToAsync("..");
				await Shell.Current.Navigation.PopModalAsync();
			}
			else
			{
				StatusMessage = "Sign in was cancelled or failed.";
			}
		}
		catch (OperationCanceledException)
		{
			StatusMessage = "Sign in was cancelled.";
			Trace.TraceInformation("Google sign-in cancelled by user");
		}
		catch (Exception ex)
		{
			StatusMessage = $"Sign in failed: {ex.Message}";
			Trace.TraceError($"Google sign-in error: {ex.Message}");
		}
		finally
		{
			IsBusy = false;
			IsNotBusy = true;
		}
	}

	[RelayCommand]
	async Task SkipAndUseLocallyAsync(CancellationToken cancellationToken = default)
	{
		if (IsBusy)
		{
			return;
		}

		try
		{
			IsBusy = true;
			IsNotBusy = false;
			StatusMessage = "Setting up local mode...";

			await AuthenticationService.SetLocalOnlyModeAsync(cancellationToken);

			StatusMessage = "Local mode enabled!";

			// Navigate back or close the login page
			//await Shell.Current.GoToAsync("..");
			await Shell.Current.Navigation.PopModalAsync();
		}
		catch (Exception ex)
		{
			StatusMessage = $"Setup failed: {ex.Message}";
			Trace.TraceError($"Local mode setup error: {ex.Message}");
		}
		finally
		{
			IsBusy = false;
			IsNotBusy = true;
		}
	}
}
