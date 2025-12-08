using Application = Microsoft.Maui.Controls.Application;

namespace EpubReader;

public partial class App : Application, IDisposable
{
	readonly AppShell appShell;
	readonly AuthenticationService authenticationService;
	readonly ISyncService syncService;
	readonly CancellationTokenSource lifecycleCts = new();
	bool disposed;

	public App(
		AppShell appShell,
		AuthenticationService authenticationService,
		ISyncService syncService)
	{
		InitializeComponent();
		this.appShell = appShell;
		this.authenticationService = authenticationService;
		this.syncService = syncService;
		this.authenticationService.AuthStateChanged += OnAuthStateChanged;
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		if (appShell is null)
		{
			return new Window(new AppShell());
		}

		return new Window(appShell);
	}

	protected override async void OnStart()
	{
		base.OnStart();
		await InitializeAppAsync(lifecycleCts.Token);
	}

	protected override async void OnSleep()
	{
		base.OnSleep();
		await FlushSyncAsync(lifecycleCts.Token);
	}

	protected override async void OnResume()
	{
		base.OnResume();
		await InitializeAppAsync(lifecycleCts.Token);
	}

	async Task InitializeAppAsync(CancellationToken token)
	{
		bool needsAuth = await AuthenticationService.NeedsAuthenticationAsync(token);
		if (needsAuth)
		{
			await Shell.Current.GoToAsync("LoginPage");
			return;
		}
		await InitializeSyncAsync(token);
	}

	async Task InitializeSyncAsync(CancellationToken token)
	{
		var isAuthenticated = await authenticationService.IsAuthenticatedAsync(token);
		var isLocalOnly = await AuthenticationService.IsLocalOnlyModeAsync(token);

		if (isLocalOnly)
		{
			await syncService.InitializeLocalOnlyAsync(token);
		}
		else if (isAuthenticated)
		{
			var userId = await authenticationService.GetCurrentUserIdAsync(token);
			
			await syncService.InitializeAsync(userId, token);
			await syncService.SubscribeToRemoteChangesAsync(token);
		}
		else
		{
			await syncService.InitializeLocalOnlyAsync(token);	
		}
	}

	public Task ReinitializeSyncAsync(CancellationToken token)
	{
		return InitializeSyncAsync(token);
	}

	async void OnAuthStateChanged(object? sender, bool isAuthenticated)
	{
		await ReinitializeSyncAsync(lifecycleCts.Token);
	}

	async Task FlushSyncAsync(CancellationToken token)
	{
		await syncService.FlushOfflineQueueAsync(token);
	}

	public void Dispose()
	{
		Dispose(true);
		GC.SuppressFinalize(this);
	}

	protected virtual void Dispose(bool disposing)
	{
		if (disposed)
		{
			return;
		}
		if (disposing)
		{
			lifecycleCts.Cancel();
			this.authenticationService.AuthStateChanged -= OnAuthStateChanged;
			lifecycleCts.Dispose();
		}
		disposed = true;
	}
}