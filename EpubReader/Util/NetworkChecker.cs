using System;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text.RegularExpressions;
using System.Threading.Tasks; // For simplified IP range checking

namespace EpubReader.Util;

/// <summary>
/// Provides functionality to determine if a given web address is local to a home network or external, and applies
/// specific rules for handling HTTP and HTTPS protocols.
/// </summary>
/// <remarks>This class is designed to help differentiate between local and external web addresses based on common
/// private IP ranges. It also applies specific rules for HTTP and HTTPS protocols, allowing HTTP connections only if
/// they are local, while permitting HTTPS connections regardless of locality.</remarks>
public class NetworkChecker
{
	protected NetworkChecker()
	{
		// This constructor is protected to prevent instantiation of this class.
		// Use static methods instead.
	}

	public static async Task<bool> ValidateNetworkConnection(string url, CancellationToken cancellationToken = default)
	{
		try
		{
			using var client = new HttpClient();
			client.Timeout = TimeSpan.FromSeconds(10); // Set a timeout for the request
			var response = await client.GetAsync(url, cancellationToken);
			if (response.IsSuccessStatusCode)
			{
				return true;
			}
			else
			{
				return false;
			}
		}
		catch (HttpRequestException e)
		{
			System.Diagnostics.Trace.TraceError($"Network request failed: {e.Message}");
			return false;
		}
		catch (TaskCanceledException)
		{
			System.Diagnostics.Trace.TraceError("Network request timed out.");
			return false;
		}
		catch (Exception ex)
		{
			System.Diagnostics.Trace.TraceError($"An unexpected error occurred: {ex.Message}");
			return false;
		}
	}

	/// <summary>
	/// Validates the server's SSL certificate based on custom logic.
	/// </summary>
	/// <remarks>This method allows inspection of the server's certificate details and applies custom validation
	/// logic to determine its validity. It logs information about the certificate and the SSL errors
	/// encountered.</remarks>
	/// <param name="requestMessage">The HTTP request message associated with the server certificate.</param>
	/// <param name="certificate">The server's SSL certificate to validate. Can be <see langword="null"/> if no certificate is provided.</param>
	/// <param name="chain">The chain of certificate authorities associated with the server's certificate. Can be <see langword="null"/> if no
	/// chain is provided.</param>
	/// <param name="sslErrors">The SSL policy errors encountered during the certificate validation process.</param>
	/// <returns><see langword="true"/> if the certificate is considered valid based on the custom logic; otherwise, <see
	/// langword="false"/>.</returns>
	public static bool ServerCertificateCustomValidation(HttpRequestMessage requestMessage, X509Certificate2? certificate, X509Chain? chain, SslPolicyErrors sslErrors)
	{
		// It is possible to inspect the certificate provided by the server.
		System.Diagnostics.Trace.TraceInformation($"Requested URI: {requestMessage.RequestUri}");
		System.Diagnostics.Trace.TraceInformation($"Effective date: {certificate?.GetEffectiveDateString()}");
		System.Diagnostics.Trace.TraceInformation($"Exp date: {certificate?.GetExpirationDateString()}");
		System.Diagnostics.Trace.TraceInformation($"Issuer: {certificate?.Issuer}");
		System.Diagnostics.Trace.TraceInformation($"Subject: {certificate?.Subject}");

		// Based on the custom logic it is possible to decide whether the client considers certificate valid or not
		System.Diagnostics.Trace.TraceInformation($"Errors: {sslErrors}");
		return sslErrors == SslPolicyErrors.RemoteCertificateChainErrors
			|| sslErrors == SslPolicyErrors.RemoteCertificateNameMismatch
			|| sslErrors == SslPolicyErrors.RemoteCertificateNotAvailable
			|| sslErrors == SslPolicyErrors.RemoteCertificateChainErrors
			|| sslErrors == SslPolicyErrors.RemoteCertificateNameMismatch
			|| sslErrors == SslPolicyErrors.RemoteCertificateNotAvailable;
	}

	/// <summary>
	/// Validates the SSL certificate of the specified URL by attempting an HTTP GET request.
	/// </summary>
	/// <remarks>This method uses a custom server certificate validation callback defined in <see
	/// cref="NetworkChecker.ServerCertificateCustomValidation"/>. The request times out after 10 seconds if the server
	/// does not respond.</remarks>
	/// <param name="url">The URL of the server whose SSL certificate is to be validated. Must be a valid URI.</param>
	/// <returns><see langword="true"/> if the SSL certificate is valid and the server responds successfully;  otherwise, <see
	/// langword="false"/>. </returns>
	public static async Task<bool> ValidateSSLCerticate(string url)
	{
		HttpClientHandler handler = new()
		{
			ServerCertificateCustomValidationCallback = ServerCertificateCustomValidation
		};
		HttpClient client = new(handler)
		{
			Timeout = TimeSpan.FromSeconds(10)
		};
		try
		{
			HttpResponseMessage response = await client.GetAsync(url);
			response.EnsureSuccessStatusCode();

			string responseBody = await response.Content.ReadAsStringAsync();
			if (string.IsNullOrEmpty(responseBody))
			{
				return false;
			}
		}
		catch (HttpRequestException e)
		{
			System.Diagnostics.Trace.TraceError($"HTTP(S) request failed: {e.Message}");
			return false;
		}
		finally
		{
			handler.Dispose();
			client.Dispose();
		}
		return true;
	}
	// Define your local IP address ranges. These are common private IP ranges.
	// You might need to adjust these based on your specific home network configuration.
	// For example, if your router is 192.168.1.1, your local range is likely 192.168.1.0/24.
	static readonly string[] localIpRanges =
	[
		"10.0.0.0/8",      // 10.0.0.0 - 10.255.255.255
        "172.16.0.0/12",   // 172.16.0.0 - 172.31.255.255
        "192.168.0.0/16"   // 192.168.0.0 - 192.168.255.255
    ];

	/// <summary>
	/// Checks if a given web address is local or external to a home network
	/// and applies specific HTTP-related rules for external addresses.
	/// </summary>
	/// <param name="webAddress">The web address to check (e.g., "http://localhost", "https://example.com", "http://192.168.1.100").</param>
	/// <returns>
	/// True if the address is local.
	/// False if the address is external AND uses HTTP.
	/// True if the address is external AND uses HTTPS (or another non-HTTP protocol).
	/// </returns>
	public static bool IsAddressLocalOrPermittedExternal(string webAddress)
	{
		Uri uri;
		try
		{
			uri = new Uri(webAddress);
		}
		catch (UriFormatException)
		{
			// If the address is not a valid URI, we can't process it.
			// You might want to throw an exception or return false here.
			Console.WriteLine($"Error: Invalid URI format for '{webAddress}'");
			return false;
		}

		// 1. Determine the protocol
		bool isHttp = uri.Scheme.Equals("http", StringComparison.OrdinalIgnoreCase);
		bool isHttps = uri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase);

		// 2. Resolve hostname to IP address(es)
		IPAddress[] ipAddresses;
		try
		{
			// Dns.GetHostAddresses can resolve both hostnames and IP literals
			ipAddresses = Dns.GetHostAddresses(uri.Host);
		}
		catch (SocketException)
		{
			// Could not resolve the host, assume external and potentially problematic.
			Console.WriteLine($"Warning: Could not resolve host '{uri.Host}'. Treating as external.");
			// If it's HTTP and we can't resolve, it's safer to return false based on your rule.
			return !(isHttp && !isHttps); // If HTTP and not HTTPS, return false. Otherwise, true.
		}

		// 3. Check if any resolved IP address is local
		bool isLocal = false;
		foreach (IPAddress ip in ipAddresses)
		{
			if (ip.AddressFamily == AddressFamily.InterNetwork && IsPrivateIP(ip)) // Focus on IPv4 for simplicity in common home networks
			{
				isLocal = true;
				break; // Found a local IP, no need to check others
			}
		}

		// 4. Apply the specified logic
		if (isLocal)
		{
			return true; // If it's local, always return true.
		}
		else
		{
			// If it's not local (external)
			if (isHttp)
			{
				return false; // If it's HTTP and not local, return false.
			}
			else
			{
				// If it's external AND not HTTP (e.g., HTTPS), return true.
				return true;
			}
		}
	}

	/// <summary>
	/// Checks if an IPv4 address falls within common private IP ranges.
	/// Note: This is a simplified check. For a robust solution, consider
	/// a dedicated IP range parsing library or more sophisticated CIDR matching.
	/// </summary>
	/// <param name="ipAddress">The IPAddress to check.</param>
	/// <returns>True if the IPAddress is a private (local) IP, false otherwise.</returns>
	static bool IsPrivateIP(IPAddress ipAddress)
	{
		byte[] ipBytes = ipAddress.GetAddressBytes();

		// Loopback address: 127.0.0.1
		if (IPAddress.IsLoopback(ipAddress))
		{
			return true;
		}

		// Check against common private IP ranges
		foreach (string rangeCidr in localIpRanges)
		{
			// Simple parsing assuming CIDR format like "192.168.0.0/16"
			string[] parts = rangeCidr.Split('/');
			if (parts.Length == 2 && IPAddress.TryParse(parts[0], out _) && int.TryParse(parts[1], out _))
			{
				// This is a simplified check. For full CIDR matching,
				// you'd typically convert to int and use bitwise operations.
				// For demonstration, we'll use string comparison on initial bytes.
				// A more robust solution would involve proper CIDR subnet calculation.
				// For example, 192.168.0.0/16 means 192.168.x.x
				// 10.0.0.0/8 means 10.x.x.x
				// 172.16.0.0/12 means 172.16.x.x - 172.31.x.x

				// Simple check for common private ranges
				if (ipBytes[0] == 10)
				{
					return true; // 10.x.x.x
				}

				if (ipBytes[0] == 172 && ipBytes[1] >= 16 && ipBytes[1] <= 31)
				{
					return true; // 172.16-31.x.x
				}

				if (ipBytes[0] == 192 && ipBytes[1] == 168)
				{
					return true; // 192.168.x.x
				}
			}
		}

		return false;
	}
}
