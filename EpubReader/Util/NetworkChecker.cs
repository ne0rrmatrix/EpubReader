using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;

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
	static readonly ILogger logger = LoggerFactory.GetLogger(nameof(NetworkChecker));
	protected NetworkChecker()
	{
		// This constructor is protected to prevent instantiation of this class.
		// Use static methods instead.
	}

	/// <summary>
	/// Validates the network connection by attempting to send a GET request to the specified URL.
	/// </summary>
	/// <remarks>This method logs information about any network request failures or timeouts. It uses a default
	/// timeout of 10 seconds for the request.</remarks>
	/// <param name="url">The URL to which the network connection is validated. Must be a valid URI.</param>
	/// <param name="cancellationToken">A token to cancel the operation. Defaults to <see cref="CancellationToken.None"/>.</param>
	/// <returns><see langword="true"/> if the network connection is successful and the response status code indicates success;
	/// otherwise, <see langword="false"/>.</returns>
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
			logger.Info($"Network request failed: {e.Message}");
			return false;
		}
		catch (TaskCanceledException)
		{
			logger.Info("Network request timed out.");
			return false;
		}
		catch (Exception ex)
		{
			logger.Info($"An unexpected error occurred: {ex.Message}");
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
		logger.Info($"Requested URI: {requestMessage.RequestUri}");
		logger.Info($"Effective date: {certificate?.GetEffectiveDateString()}");
		logger.Info($"Exp date: {certificate?.GetExpirationDateString()}");
		logger.Info($"Issuer: {certificate?.Issuer}");
		logger.Info($"Subject: {certificate?.Subject}");
		logger.Info($"Errors: {sslErrors}");

		// Return true only when there are NO errors
		return sslErrors == SslPolicyErrors.None;
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
	public static async Task<bool> ValidateSSLCertificate(string url)
	{
		bool certificateValid = false;

		HttpClientHandler handler = new()
		{
			ServerCertificateCustomValidationCallback = (request, cert, chain, errors) =>
			{
				certificateValid = ServerCertificateCustomValidation(request, cert, chain, errors);
				return certificateValid; // Or return true to always allow connection for testing
			}
		};

		using HttpClient client = new(handler) { Timeout = TimeSpan.FromSeconds(10) };

		try
		{
			HttpResponseMessage response = await client.GetAsync(url);
			response.EnsureSuccessStatusCode();
			return certificateValid; // Return the actual certificate validation result
		}
		catch (HttpRequestException e)
		{
			logger.Info($"HTTP(S) request failed: {e.Message}");
			return false;
		}
	}

	// Define your local IP address ranges. These are common private IP ranges.
	// You might need to adjust these based on your specific home network configuration.
	// For example, if your router is 192.168.1.1, your local range is likely 192.168.1.0/24.
	static readonly string[] localIpRanges =
	[
		// IPv4 Private Ranges (RFC 1918)
		"10.0.0.0/8",      // 10.0.0.0 - 10.255.255.255
    "172.16.0.0/12",   // 172.16.0.0 - 172.31.255.255
    "192.168.0.0/16",  // 192.168.0.0 - 192.168.255.255
    
    // IPv4 Additional Local Ranges
    "127.0.0.0/8",     // 127.0.0.0 - 127.255.255.255 (Loopback)
    "169.254.0.0/16",  // 169.254.0.0 - 169.254.255.255 (Link-local/APIPA)
    
    // IPv6 Private/Local Ranges
    "::1/128",         // ::1 (IPv6 loopback)
    "fc00::/7",        // fc00:: - fdff:: (Unique Local Addresses - ULA)
    "fe80::/10",       // fe80:: - febf:: (Link-local addresses)
    
    // IPv6 Documentation/Reserved ranges (optional - for completeness)
    "2001:db8::/32",   // 2001:db8:: (Documentation prefix - RFC 3849)
    "::/128",          // :: (Unspecified address)
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
			logger.Info($"Error: Invalid URI format for '{webAddress}'");
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
			logger.Info($"Warning: Could not resolve host '{uri.Host}'. Treating as external.");
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
		if (localIpRanges.Any(rangeCidr => CheckIPBytes(ipBytes, rangeCidr)))
		{
			return true; // If it matches any private range, return true.
		}
		return false; // If it doesn't match any range, return false.
	}

	/// <summary>
	/// Determines whether the specified IP address bytes fall within a given CIDR range.
	/// </summary>
	/// <remarks>The method checks if the IP address, represented by <paramref name="ipBytes"/>, is within the range
	/// specified by <paramref name="rangeCidr"/>. The CIDR notation must be valid, and the prefix length must be
	/// appropriate for the address family (IPv4 or IPv6).</remarks>
	/// <param name="ipBytes">The byte array representing the IP address to check.</param>
	/// <param name="rangeCidr">The CIDR notation string representing the IP range, in the format "address/prefixLength".</param>
	/// <returns><see langword="true"/> if the IP address bytes are within the specified CIDR range; otherwise, <see
	/// langword="false"/>.</returns>
	static bool CheckIPBytes(byte[] ipBytes, string rangeCidr)
	{
		string[] parts = rangeCidr.Split('/');
		if (parts.Length == 2 && IPAddress.TryParse(parts[0], out IPAddress? rangeAddress) && int.TryParse(parts[1], out int prefixLength))
		{
			byte[] rangeBytes = rangeAddress.GetAddressBytes();

			// Determine max prefix length based on address family (IPv4 = 32, IPv6 = 128)
			int maxPrefixLength = rangeBytes.Length == 4 ? 32 : 128;

			if (ipBytes.Length != rangeBytes.Length || prefixLength < 0 || prefixLength > maxPrefixLength)
			{
				return false;
			}

			int fullBytes = prefixLength / 8;
			int remainingBits = prefixLength % 8;

			return CompareIPBytes(ipBytes, rangeBytes, fullBytes, remainingBits);
		}
		return false;
	}

	/// <summary>
	/// Compares two IP address byte arrays to determine if they match up to a specified number of full bytes and remaining
	/// bits.
	/// </summary>
	/// <param name="ipBytes">The byte array representing the IP address to compare.</param>
	/// <param name="rangeBytes">The byte array representing the IP address range to compare against.</param>
	/// <param name="fullBytes">The number of full bytes to compare between the two byte arrays.</param>
	/// <param name="remainingBits">The number of bits to compare in the next byte after the full bytes.</param>
	/// <returns><see langword="true"/> if the IP address matches the range up to the specified number of full bytes and remaining
	/// bits; otherwise, <see langword="false"/>.</returns>
	static bool CompareIPBytes(byte[] ipBytes, byte[] rangeBytes, int fullBytes, int remainingBits)
	{
		// Compare full bytes
		for (int i = 0; i < fullBytes; i++)
		{
			if (ipBytes[i] != rangeBytes[i])
			{
				return false;
			}
		}

		// Compare remaining bits if any
		if (remainingBits > 0)
		{
			byte mask = (byte)(0xFF << (8 - remainingBits));
			if ((ipBytes[fullBytes] & mask) != (rangeBytes[fullBytes] & mask))
			{
				return false;
			}
		}

		return true;
	}
}
