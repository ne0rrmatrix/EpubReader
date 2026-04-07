#if ANDROID
using Android.Content;
using Android.Net.Nsd;
using Android.Net.Wifi;
using Android.OS;

namespace EpubReader.Util;

public partial class CalibreZeroConf
{
	static async Task<List<(string IpAddress, int Port)>> DiscoverCalibreServersOnAndroidAsync(TimeSpan scanTime)
	{
		using var discoveryScope = BeginPlatformDiscoveryScope();
		var wifiManager = GetWifiManager();
		var context = Android.App.Application.Context;
		if (context?.GetSystemService(Context.NsdService) is not NsdManager nsdManager)
		{
			logger.Warn("Android NSD manager unavailable; falling back to Zeroconf discovery.");
			return await DiscoverServersWithoutNsdAsync(wifiManager, scanTime);
		}

		Dictionary<string, (string IpAddress, int Port)> servers = new(StringComparer.OrdinalIgnoreCase);
		System.Threading.Lock syncRoot = new();

		foreach (var serviceType in GetCalibreServiceTypes())
		{
			using var discoveryListener = new AndroidCalibreDiscoveryListener(nsdManager, servers, syncRoot);

			logger.Info($"Starting Android NSD discovery for Calibre services with type '{serviceType}'.");
			nsdManager.DiscoverServices(serviceType, NsdProtocol.DnsSd, discoveryListener);

			try
			{
				await Task.Delay(scanTime);
			}
			finally
			{
				discoveryListener.StopDiscovery();
				await Task.Delay(TimeSpan.FromMilliseconds(250));
			}

			lock (syncRoot)
			{
				if (servers.Count > 0)
				{
					break;
				}
			}
		}

		List<(string IpAddress, int Port)> results;
		lock (syncRoot)
		{
			results = [.. servers.Values];
		}

		if (results.Count > 0)
		{
			logger.Info($"Android NSD discovered {results.Count} Calibre service(s).");
			return results;
		}

		logger.Warn("Android NSD did not discover any Calibre services; falling back to Zeroconf discovery.");
		return await DiscoverServersWithoutNsdAsync(wifiManager, scanTime);
	}

	static async Task<List<(string IpAddress, int Port)>> DiscoverServersWithoutNsdAsync(WifiManager? wifiManager, TimeSpan scanTime)
	{
		var zeroconfResults = await DiscoverCalibreServersWithZeroconfAsync(scanTime);
		if (zeroconfResults.Count > 0)
		{
			logger.Info($"Zeroconf discovered {zeroconfResults.Count} Calibre service(s) after Android NSD returned no results.");
			return zeroconfResults;
		}

		logger.Warn("Zeroconf did not discover any Calibre services; probing the local subnet for OPDS endpoints.");
		return await DiscoverCalibreServersBySubnetProbeAsync(wifiManager, scanTime);
	}

	static string[] GetCalibreServiceTypes()
		=> ["_calibre._tcp", "_calibre._tcp.", "_http._tcp", "_http._tcp."];

	static bool IsPotentialCalibreService(NsdServiceInfo serviceInfo)
	{
		if (MatchesCalibreServiceType(serviceInfo.ServiceType))
		{
			return true;
		}

		if (serviceInfo.ServiceName?.Contains("calibre", StringComparison.OrdinalIgnoreCase) == true)
		{
			return true;
		}

		return IsPotentialCalibrePort(serviceInfo.Port);
	}

	static async Task<List<(string IpAddress, int Port)>> DiscoverCalibreServersBySubnetProbeAsync(WifiManager? wifiManager, TimeSpan scanTime)
	{
		if (!TryGetSubnetCandidates(wifiManager, out List<string>? hostsToProbe, out string currentHost) || hostsToProbe is null)
		{
			logger.Warn("Android subnet probe could not determine the current Wi-Fi subnet.");
			return [];
		}

		logger.Info($"Android subnet probe will scan {hostsToProbe.Count} host(s) near {currentHost} for Calibre OPDS endpoints.");

		Dictionary<string, (string IpAddress, int Port)> discoveredServers = new(StringComparer.OrdinalIgnoreCase);
		System.Threading.Lock syncRoot = new();
		using HttpClient client = new()
		{
			Timeout = TimeSpan.FromMilliseconds(Math.Clamp((int)(scanTime.TotalMilliseconds / 8), 300, 600))
		};

		using SemaphoreSlim gate = new(64, 64);
		List<Task> probeTasks = [];

		foreach (var host in hostsToProbe)
		{
			foreach (var port in GetLikelyCalibrePorts())
			{
				probeTasks.Add(ProbeCalibreEndpointAsync(client, gate, discoveredServers, syncRoot, host, port));
			}
		}

		await Task.WhenAll(probeTasks);

		lock (syncRoot)
		{
			if (discoveredServers.Count > 0)
			{
				logger.Info($"Android subnet probe discovered {discoveredServers.Count} Calibre candidate endpoint(s).");
				return [.. discoveredServers.Values];
			}
		}

		logger.Warn("Android subnet probe did not find any responding Calibre OPDS endpoints.");
		return [];
	}

	static async Task ProbeCalibreEndpointAsync(HttpClient client, SemaphoreSlim gate, IDictionary<string, (string IpAddress, int Port)> discoveredServers, System.Threading.Lock syncRoot, string host, int port)
	{
		await gate.WaitAsync();
		try
		{
			using HttpRequestMessage request = new(HttpMethod.Get, $"http://{host}:{port}/opds");
			using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
			if (!response.IsSuccessStatusCode)
			{
				return;
			}

			if (!response.Content.Headers.ContentType?.MediaType?.Contains("xml", StringComparison.OrdinalIgnoreCase) ?? true)
			{
				logger.Info($"Android subnet probe received a non-OPDS content type from http://{host}:{port}/opds.");
				return;
			}

			AddServerIfMissing(discoveredServers, syncRoot, host, port);
			logger.Info($"Android subnet probe found a responding OPDS endpoint at http://{host}:{port}/opds.");
		}
		catch (Exception)
		{
			return;
		}
		finally
		{
			gate.Release();
		}
	}

	static int[] GetLikelyCalibrePorts()
		=> [8080, 8081];

	static bool TryGetSubnetCandidates(WifiManager? wifiManager, out List<string>? hostsToProbe, out string currentHost)
	{
		hostsToProbe = null;
		currentHost = string.Empty;


#pragma warning disable CA1422
		var dhcpInfo = wifiManager?.DhcpInfo;
#pragma warning restore CA1422
		if (dhcpInfo is null || dhcpInfo.IpAddress == 0)
		{
			return false;
		}

		uint ipAddress = unchecked((uint)dhcpInfo.IpAddress);
		currentHost = ConvertToIpAddressString(ipAddress);
		string[] parts = currentHost.Split('.');
		if (parts.Length != 4)
		{
			return false;
		}

		string subnetPrefix = $"{parts[0]}.{parts[1]}.{parts[2]}";
		if (!int.TryParse(parts[3], out int currentHostOctet))
		{
			return false;
		}

		hostsToProbe = [];
		for (int candidate = 1; candidate <= 254; candidate++)
		{
			if (candidate == currentHostOctet)
			{
				continue;
			}

			hostsToProbe.Add($"{subnetPrefix}.{candidate}");
		}

		return hostsToProbe.Count > 0;
	}

	static string ConvertToIpAddressString(uint address)
		=> string.Join('.',
			address & 0xFF,
			(address >> 8) & 0xFF,
			(address >> 16) & 0xFF,
			(address >> 24) & 0xFF);

	static string GetServiceKey(string host, int port)
		=> $"{host}:{port}";

	static void AddServerIfMissing(IDictionary<string, (string IpAddress, int Port)> servers, System.Threading.Lock syncRoot, string host, int port)
	{
		if (string.IsNullOrWhiteSpace(host) || port <= 0)
		{
			return;
		}

		lock (syncRoot)
		{
			servers[GetServiceKey(host, port)] = (host, port);
		}
	}

	static string? GetHostAddress(NsdServiceInfo? serviceInfo)
	{
		if (serviceInfo is null)
		{
			return null;
		}

		if (OperatingSystem.IsAndroidVersionAtLeast(34))
		{
			return serviceInfo.HostAddresses?.FirstOrDefault()?.HostAddress;
		}

#pragma warning disable CA1422
		return serviceInfo.Host?.HostAddress;
#pragma warning restore CA1422
	}

	static WifiManager? GetWifiManager()
	{
		var context = Android.App.Application.Context;
		if (context?.GetSystemService(Context.WifiService) is WifiManager wifiManager)
		{
			return wifiManager;
		}

		logger.Warn("Android Wi-Fi manager unavailable.");
		return null;
	}

	static void ResolveService(NsdManager nsdManager, NsdServiceInfo serviceInfo, AndroidCalibreResolveListener resolveListener)
	{
#pragma warning disable CA1422
		nsdManager.ResolveService(serviceInfo, resolveListener);
#pragma warning restore CA1422
	}

	static DiscoveryScope? BeginPlatformDiscoveryScope()
	{
		var wifiManager = GetWifiManager();
		if (wifiManager is null)
		{
			logger.Warn("Android Wi-Fi manager unavailable; discovery will proceed without multicast lock.");
			return null;
		}

		var multicastLock = wifiManager.CreateMulticastLock("EpubReader.CalibreZeroConf");
		if (multicastLock is null)
		{
			logger.Warn("Android multicast lock could not be created; discovery will proceed without it.");
			return null;
		}

		multicastLock.SetReferenceCounted(false);
		multicastLock.Acquire();
		logger.Info("Acquired Android multicast lock for Calibre discovery.");
		return new DiscoveryScope(multicastLock);
	}

	sealed class DiscoveryScope : IDisposable
	{
		readonly WifiManager.MulticastLock multicastLock;

		public DiscoveryScope(WifiManager.MulticastLock multicastLock)
		{
			this.multicastLock = multicastLock;
		}

		public void Dispose()
		{
			if (multicastLock.IsHeld)
			{
				multicastLock.Release();
				logger.Info("Released Android multicast lock after Calibre discovery.");
			}
		}
	}

	sealed class AndroidCalibreDiscoveryListener : Java.Lang.Object, NsdManager.IDiscoveryListener
	{
		readonly NsdManager nsdManager;
		readonly IDictionary<string, (string IpAddress, int Port)> servers;
		readonly System.Threading.Lock syncRoot;
		bool isStopped;

		public AndroidCalibreDiscoveryListener(NsdManager nsdManager, IDictionary<string, (string IpAddress, int Port)> servers, System.Threading.Lock syncRoot)
		{
			this.nsdManager = nsdManager;
			this.servers = servers;
			this.syncRoot = syncRoot;
		}

		public void OnDiscoveryStarted(string? serviceType)
		{
			logger.Info($"Android NSD discovery started for service type '{serviceType}'.");
		}

		public void OnDiscoveryStopped(string? serviceType)
		{
			logger.Info($"Android NSD discovery stopped for service type '{serviceType}'.");
		}

		public void OnServiceFound(NsdServiceInfo? serviceInfo)
		{
			if (serviceInfo is null)
			{
				return;
			}

			logger.Info($"Android NSD service found: {serviceInfo.ServiceName} ({serviceInfo.ServiceType})");
			if (!IsPotentialCalibreService(serviceInfo))
			{
				logger.Info($"Android NSD skipping non-Calibre candidate: {serviceInfo.ServiceName} ({serviceInfo.ServiceType})");
				return;
			}

			try
			{
				ResolveService(nsdManager, serviceInfo, new AndroidCalibreResolveListener(servers, syncRoot));
			}
			catch (Exception ex)
			{
				logger.Warn($"Failed to resolve Android NSD service '{serviceInfo.ServiceName}': {ex.Message}");
			}
		}

		public void OnServiceLost(NsdServiceInfo? serviceInfo)
		{
			if (serviceInfo is null)
			{
				return;
			}

			if (GetHostAddress(serviceInfo) is not string hostAddress || serviceInfo.Port <= 0)
			{
				return;
			}

			lock (syncRoot)
			{
				servers.Remove(GetServiceKey(hostAddress, serviceInfo.Port));
			}
			logger.Info($"Android NSD service lost: {serviceInfo.ServiceName} ({hostAddress}:{serviceInfo.Port})");
		}

		public void OnStartDiscoveryFailed(string? serviceType, NsdFailure errorCode)
		{
			logger.Warn($"Android NSD failed to start discovery for '{serviceType}': {errorCode}");
			StopDiscovery();
		}

		public void OnStopDiscoveryFailed(string? serviceType, NsdFailure errorCode)
		{
			logger.Warn($"Android NSD failed to stop discovery for '{serviceType}': {errorCode}");
			StopDiscovery();
		}

		public void StopDiscovery()
		{
			if (isStopped)
			{
				return;
			}

			isStopped = true;
			try
			{
				nsdManager.StopServiceDiscovery(this);
			}
			catch (Exception ex)
			{
				logger.Warn($"Android NSD stop discovery failed: {ex.Message}");
			}
		}
	}

	sealed class AndroidCalibreResolveListener : Java.Lang.Object, NsdManager.IResolveListener
	{
		readonly IDictionary<string, (string IpAddress, int Port)> servers;
		readonly System.Threading.Lock syncRoot;

		public AndroidCalibreResolveListener(IDictionary<string, (string IpAddress, int Port)> servers, System.Threading.Lock syncRoot)
		{
			this.servers = servers;
			this.syncRoot = syncRoot;
		}

		public void OnResolveFailed(NsdServiceInfo? serviceInfo, NsdFailure errorCode)
		{
			logger.Warn($"Android NSD failed to resolve service '{serviceInfo?.ServiceName}': {errorCode}");
		}

		public void OnServiceResolved(NsdServiceInfo? serviceInfo)
		{
			if (serviceInfo is null)
			{
				logger.Warn("Android NSD resolved a null service info instance.");
				return;
			}

			if (GetHostAddress(serviceInfo) is not string hostAddress)
			{
				logger.Warn($"Android NSD resolved service '{serviceInfo?.ServiceName}' without a host address.");
				return;
			}

			if (!IsPotentialCalibreService(serviceInfo))
			{
				logger.Info($"Android NSD resolved service '{serviceInfo.ServiceName}' but it does not look like a Calibre endpoint.");
				return;
			}

			AddServerIfMissing(servers, syncRoot, hostAddress, serviceInfo.Port);
			logger.Info($"Android NSD resolved Calibre service '{serviceInfo.ServiceName}' to {hostAddress}:{serviceInfo.Port}");
		}
	}
}
#endif