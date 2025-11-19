using System.Runtime.InteropServices;

namespace MandarinJuiceCore.Models.DSSS.Mandarin;

public class MandarinHeader(MandarinFileFlavor expectedFileFlavor)
{
    public const int Size = 0x10;
    protected const uint Dsss = 0x5353_5344;

    /// <summary>
    /// Readonly buffer
    /// </summary>
    private readonly uint[] _data = [Dsss, 0x2, (uint)expectedFileFlavor, 0];

    /// <summary>
    /// Gets or sets the magic number used to identify or validate the data format.
    /// </summary>
    public uint MagicNumber
    {
        get => _data[0];
        set => _data[0] = value;
    }

    /// <summary>
    /// Don't know what's there.
    /// </summary>
    public uint Unknown1
    {
        get => _data[1];
        set => _data[1] = value;
    }

    /// <summary>
    /// Save File encryption type.
    /// Possible values: 2:None; 3:AutoStrong; ?:XOR; ?:BlowFish; 4:Citrus; 16:Lime|Mandarin; ?:RdsModule
    /// </summary>
    public uint EncryptionType
    {
        get => _data[2];
        set => _data[2] = value;
    }

    /// <summary>
    /// Don't know what's there.
    /// </summary>
    public uint Unknown2
    {
        get => _data[3];
        set => _data[3] = value;
    }

    /// <summary>
    /// Copies the contents of the specified read-only span into the internal data buffer, casting each element to an unsigned 32-bit integer.
    /// </summary>
    /// <typeparam name="T">The unmanaged type of the elements in the input span to be copied.</typeparam>
    /// <param name="data">A read-only span containing the data to copy.</param>
    /// <exception cref="InvalidDataException">Thrown if the file's magic number or encryption type is invalid.</exception>
    public void SetData<T>(ReadOnlySpan<T> data) where T : unmanaged
    {
        var dataSpan = MemoryMarshal.Cast<T, uint>(data);
        dataSpan[..(Size / sizeof(uint))].CopyTo(_data);
        CheckIntegrity();
    }

    /// <summary>
    /// Returns the array of unsigned integer data currently held by the instance.
    /// </summary>
    /// <returns>An array of <see cref="uint"/> values representing the current data.</returns>
    public uint[] GetData() => _data;

    /// <summary>
    /// Returns a read-only span over the underlying sequence of unsigned integers.
    /// </summary>
    /// <returns>A <see cref="ReadOnlySpan{uint}"/> representing the current data. The span reflects the contents at the time of the call and is valid only while the underlying data remains unchanged.</returns>
    public ReadOnlySpan<uint> GetDataAsSpan() => _data.AsSpan();

    /// <summary>
    /// Determines whether the specified magic number does not match the expected value.
    /// </summary>
    /// <param name="magicNumber">The magic number to check.</param>
    /// <returns><see langword="true"/> if the magic number does not equal the expected value; otherwise, <see langword="false"/>.</returns>
    public bool CheckMagicNumber(uint? magicNumber = null)
    {
        magicNumber ??= MagicNumber;
        return magicNumber == Dsss;
    }

    /// <summary>
    /// Determines whether the specified encryption type matches one of the supported encryption types.
    /// </summary>
    /// <param name="encryptionType">The encryption type to check. If <paramref name="encryptionType"/> is <see langword="null"/>, the current encryption type is used.</param>
    /// <returns><see langword="true"/> if the encryption type is supported; otherwise, <see langword="false"/>.</returns>
    public bool CheckEncryptionType(uint? encryptionType = null)
    {
        encryptionType ??= EncryptionType;
        return encryptionType == (uint)expectedFileFlavor;
    }

    /// <summary>
    /// Checks the integrity of the file by validating its magic number and encryption type.
    /// </summary>
    /// <exception cref="InvalidDataException">Thrown if the file's magic number or encryption type is invalid.</exception>
    public void CheckIntegrity()
    {
        if (!CheckMagicNumber()) throw new InvalidDataException("Invalid file magic number.");
        if (!CheckEncryptionType()) throw new InvalidDataException("Invalid file encryption type.");
    }
}