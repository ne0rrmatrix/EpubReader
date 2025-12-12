using System.Text;

namespace EpubReader.Converter;

public static class Base64Decoder
{
    public static string? DecodeFromBase64(string base64Encoded)
    {
        try
        {
            byte[] bytes = Convert.FromBase64String(base64Encoded);
            return Encoding.UTF8.GetString(bytes);
        }
        catch (Exception ex)
        {
            // Handle decoding errors
            System.Diagnostics.Trace.TraceError($"Error decoding base64: {ex.Message}");
            return null;
        }
    }
}