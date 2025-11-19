using System.Runtime.InteropServices;

namespace MandarinJuiceCore.Helpers;

public static class Array2Base64
{
    /// <summary>
    /// Encodes the contents of the specified read-only span of value types as a Base64 string.
    /// </summary>
    /// <typeparam name="T">The value type of the elements in the span to encode.</typeparam>
    /// <param name="span">The read-only span of value type elements to encode as Base64. The span is interpreted as a sequence of bytes.</param>
    /// <returns>A Base64-encoded string representing the binary contents of the span.</returns>
    public static string ToBase64<T>(this ReadOnlySpan<T> span) where T : struct
        => Convert.ToBase64String(MemoryMarshal.AsBytes(span));

    /// <summary>
    /// Decodes a base64-encoded string into an array of value type elements of the specified type.
    /// </summary>
    /// <typeparam name="T">The value type to which the decoded bytes will be cast.</typeparam>
    /// <param name="b64String">The base64-encoded string representing the binary data to decode and cast.</param>
    /// <returns>An array of elements of type <typeparamref name="T"/> containing the decoded data.</returns>
    public static T[] FromBase64<T>(this string b64String) where T : struct
        => MemoryMarshal.Cast<byte, T>(Convert.FromBase64String(b64String)).ToArray();
}