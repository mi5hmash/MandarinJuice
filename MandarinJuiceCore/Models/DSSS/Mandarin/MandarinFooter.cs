using System.Runtime.InteropServices;

namespace MandarinJuiceCore.Models.DSSS.Mandarin;

public class MandarinFooter
{
    public const int Size = 0x8C;
    private const int SaltSize = 0x80;

    /// <summary>
    /// Readonly buffer.
    /// </summary>
    private readonly uint[] _data = new uint[Size / sizeof(uint)];

    /// <summary>
    /// A block of random bytes.
    /// </summary>
    public Span<uint> Salt
    {
        get => _data.AsSpan(0, SaltSize / sizeof(uint));
        set => value[..(SaltSize / sizeof(uint))].CopyTo(_data);
    }

    /// <summary>
    /// A length of a decrypted data in bytes.
    /// </summary>
    public long DecryptedDataLength
    {
        get => _data[^3] | ((long)_data[^2] << 32);
        set
        {
            _data[^3] = (uint)(value & 0xFFFFFFFF);
            _data[^2] = (uint)(value >> 32);
        }
    }

    /// <summary>
    /// A file signature.
    /// </summary>
    public uint Signature
    {
        get => _data[^1];
        set => _data[^1] = value;
    }
    
    /// <summary>
    /// Copies the contents of the specified read-only span into the internal data buffer, casting each element to an unsigned 32-bit integer.
    /// </summary>
    /// <typeparam name="T">The unmanaged type of the elements in the input span to be copied.</typeparam>
    /// <param name="data">A read-only span containing the data to copy.</param>
    public void SetData<T>(ReadOnlySpan<T> data) where T : unmanaged
    {
        var dataSpan = MemoryMarshal.Cast<T, uint>(data);
        dataSpan[..(Size / sizeof(uint))].CopyTo(_data);
    }

    /// <summary>
    /// Returns the array of unsigned integer data currently held by the instance.
    /// </summary>
    /// <returns>An array of <see cref="uint"/> values representing the current data.</returns>
    public uint[] GetData()
        => _data;

    /// <summary>
    /// Returns a read-only span over the underlying sequence of unsigned integers.
    /// </summary>
    /// <returns>A <see cref="ReadOnlySpan{uint}"/> representing the current data. The span reflects the contents at the time of the call and is valid only while the underlying data remains unchanged.</returns>
    public ReadOnlySpan<uint> GetDataAsSpan()
        => _data.AsSpan();

    /// <summary>
    /// Generates a new random salt and populates the internal data buffer with cryptographically random bytes.
    /// </summary>
    public void GenerateSalt()
    {
        Random random = new();
        var saltSpan = MemoryMarshal.Cast<uint, byte>(Salt);
        // Fill the salt portion with random bytes
        for (var i = 0; i < saltSpan.Length; i++) 
            saltSpan[i] = (byte)random.Next(byte.MaxValue + 1);
    }
}