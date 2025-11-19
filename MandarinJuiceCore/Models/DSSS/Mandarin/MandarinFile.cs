using MandarinJuiceCore.Helpers;
using System.IO.Compression;
using System.Runtime.InteropServices;

namespace MandarinJuiceCore.Models.DSSS.Mandarin;

public class MandarinFile(MandarinDeencryptor deencryptor, MandarinFileFlavor mandarinFileFlavor)
{
    /// <summary>
    /// File extension of the <see cref="MandarinFile"/>.
    /// </summary>
    public const string FileExtension = ".bin";

    /// <summary>
    /// Gets the Mandarin deencryption service used to deencrypt data.
    /// </summary>
    public MandarinDeencryptor Deencryptor { get; } = deencryptor;

    /// <summary>
    /// Header of the <see cref="MandarinFile"/>.
    /// </summary>
    public MandarinHeader Header { get; set; } = new(mandarinFileFlavor);

    /// <summary>
    /// Data of the <see cref="MandarinFile"/>.
    /// </summary>
    public byte[] Data { get; set; } = [];

    /// <summary>
    /// Footer of the <see cref="MandarinFile"/>.
    /// </summary>
    public MandarinFooter Footer { get; set; } = new();

    /// <summary>
    /// Stores the encryption state of the current file.
    /// </summary>
    public bool IsEncrypted { get; private set; }

    /// <summary>
    /// Attempts to parse and set the file header, data segments, and footer from the specified binary data buffer. Throws an exception if the data format is invalid.
    /// </summary>
    /// <param name="data">A read-only span of bytes containing the binary file data to be parsed.</param>
    /// <exception cref="InvalidDataException">Thrown if the file header, any data segment, or the file footer structure is invalid or cannot be parsed from the provided data.</exception>
    private void TrySetFileData(ReadOnlySpan<byte> data)
    {
        // HEADER
        // try to load header data into the Header
        try { Header.SetData(data); }
        catch (Exception e) { throw new InvalidDataException(e.Message); }
        // DATA
        var dataLength = data.Length - (MandarinHeader.Size + MandarinFooter.Size);
        Data = data.Slice(MandarinHeader.Size, dataLength).ToArray();
        // FOOTER
        // try to load footer data into the Footer
        Footer.SetData(data[^MandarinFooter.Size..]);
    }

    /// <summary>
    /// Attempts to set the file data from the specified byte span, using encrypted format if possible.
    /// </summary>
    /// <param name="data">A read-only span of bytes containing the file data to be loaded. The data may be in encrypted or raw format.</param>
    /// <param name="encryptedFilesOnly">If set to <see langword="true"/>, only encrypted file data will be accepted; otherwise, raw file data will be loaded if encrypted data is not available.</param>
    public void SetFileData(ReadOnlySpan<byte> data, bool encryptedFilesOnly = false)
    {
        IsEncrypted = false;
        try
        {
            // try to load the encrypted data
            TrySetFileData(data);
            IsEncrypted = true;
        }
        catch
        {
            // escape the function if only the encrypted data is needed
            if (encryptedFilesOnly) return;

            // reset header and footer
            Header = new MandarinHeader(mandarinFileFlavor);
            Footer = new MandarinFooter();

            // load the raw data
            Data = data.ToArray();

            // update footer decrypted data length
            Footer.DecryptedDataLength = Data.Length;
        }
    }

    /// <summary>
    /// Generates and returns the complete file data, including header, content, and signature, in binary format.
    /// </summary>
    /// <returns>A byte array containing the assembled file data.</returns>
    public byte[] GetFileData()
    {
        // randomize footer salt
        Footer.GenerateSalt();
        // prepare memory stream and binary writer
        using MemoryStream ms = new();
        using BinaryWriter bw = new(ms);
        // write DSSS HEADER content
        var data = MemoryMarshal.Cast<uint, byte>(Header.GetDataAsSpan());
        bw.Write(data);
        // write DSSS DATA content
        bw.Write(Data.AsSpan());
        // write DSSS FOOTER content
        data = MemoryMarshal.Cast<uint, byte>(Footer.GetDataAsSpan());
        bw.Write(data);

        var dataAsBytes = ms.ToArray();
        var dataSpan = dataAsBytes.AsSpan();

        // sign file
        SignFile(ref dataSpan);

        // return data
        return dataAsBytes;
    }

    /// <summary>
    /// Determines whether compression is permitted based on the current encryption type specified in the header.
    /// </summary>
    /// <returns><see langword="true"/> if the encryption type allows compression; otherwise, <see langword="false"/>.</returns>
    private bool IsCompressionAllowed()
        => Header.EncryptionType == (uint)MandarinFileFlavor.Compressible;

    /// <summary>
    /// Decompresses data from a Deflate-compressed byte span, returning the original uncompressed bytes.
    /// </summary>
    /// <param name="compressedData">A read-only span containing the Deflate-compressed data. The span must include any required header information; only the data after the first 24 bytes will be decompressed.</param>
    /// <returns>A byte array containing the decompressed data extracted from the input span.</returns>
    public static byte[] DecompressData(ReadOnlySpan<byte> compressedData)
    {
        using var ms = new MemoryStream(compressedData[24..].ToArray());
        using var ds = new DeflateStream(ms, CompressionMode.Decompress);
        using var msO = new MemoryStream();
        ds.CopyTo(msO);

        return msO.ToArray();
    }

    /// <summary>
    /// Compresses the specified data using the Deflate algorithm and returns a custom-formatted byte array containing both compression metadata and the compressed data.
    /// </summary>
    /// <param name="decompressedData">The data to be compressed. Must not be empty. The span is read-only and will not be modified.</param>
    /// <returns>A byte array containing the compressed data and associated metadata. The format includes the compressed size, a version marker, the original and compressed lengths, followed by the compressed data block.</returns>
    public static byte[] CompressData(ReadOnlySpan<byte> decompressedData)
    {
        byte[] compressed;
        using (var ms = new MemoryStream())
        {
            using (var ds = new DeflateStream(ms, CompressionLevel.SmallestSize, true))
                ds.Write(decompressedData.ToArray(), 0, decompressedData.Length);
            compressed = ms.ToArray();
        }

        // Build compression block
        var compressedSize = (ulong)compressed.Length;
        using var msO = new MemoryStream();
        using (var bw = new BinaryWriter(msO))
        {
            bw.Write(compressedSize + 0x10);
            bw.Write((uint)1);
            bw.Write((uint)compressedSize);
            bw.Write((ulong)decompressedData.Length);
            bw.Write(compressed);
            bw.Flush();
        }

        return msO.ToArray();
    }
    
    /// <summary>
    /// Decrypts the file data for the specified user, optionally decompressing the data after decryption.
    /// </summary>
    /// <param name="userId">The unique identifier of the user for whom the file should be decrypted.</param>
    public void DecryptFile(ulong userId)
    {
        // Decrypt Data
        var decryptedData = new byte[(uint)Footer.DecryptedDataLength];
        Deencryptor.DecryptData(decryptedData, Data, userId);
        // Decompress Data if needed
        if (IsCompressionAllowed()) decryptedData = DecompressData(decryptedData);
        // Update Data and encryption state
        Data = decryptedData;
        IsEncrypted = false;
    }

    /// <summary>
    /// Encrypts the file data for the specified user, optionally compressing the data before encryption.
    /// </summary>
    /// <param name="userId">The unique identifier of the user for whom the file will be encrypted.</param>
    public void EncryptFile(ulong userId)
    {
        // Compress Data if needed
        var encryptedData = Data;
        if (IsCompressionAllowed()) encryptedData = CompressData(encryptedData);
        // Encrypt Data
        encryptedData = Deencryptor.EncryptData(encryptedData, userId);
        // Update Data and encryption state
        Data = encryptedData;
        IsEncrypted = true;
    }
    
    /// <summary>
    /// Computes a 32-bit Murmur3 hash for the specified sequence of unsigned integers.
    /// </summary>
    /// <param name="data">The input data to hash, represented as a read-only span of 32-bit unsigned integers.</param>
    /// <param name="seed">An optional seed value to initialize the hash computation. Using different seeds produces different hash results for the same input data.</param>
    /// <returns>A 32-bit unsigned integer containing the computed Murmur3 hash of the input data.</returns>
    private static uint Murmur3_32(ReadOnlySpan<uint> data, uint seed = 0)
    {
        const uint hash0 = 0x1B873593;
        const uint hash1 = 0xCC9E2D51;
        const uint hash2 = 0x052250EC;
        const uint hash3 = 0xC2B2AE35;
        const uint hash4 = 0x85EBCA6B;

        const byte rotation1 = 0xD;
        const byte rotation2 = 0xF;
        const byte shift1 = 0x10;

        var lengthInBytes = data.Length * sizeof(uint);

        foreach (var e in data)
            seed = 5 * (uint.RotateLeft((hash0 * uint.RotateLeft(hash1 * e, rotation2)) ^ seed, rotation1) - hash2);

        uint mod0 = 0;
        switch (lengthInBytes & 3)
        {
            case 3:
                mod0 = data[2] << shift1;
                goto case 2;
            case 2:
                mod0 ^= data[1] << 8;
                goto case 1;
            case 1:
                seed ^= hash0 * uint.RotateLeft(hash1 * (mod0 ^ data[0]), rotation2);
                break;
        }

        var basis = (uint)(lengthInBytes ^ seed);
        var hiWordOfBasis = (basis >> shift1) & 0xFFFF;

        return (hash3 * ((hash4 * (basis ^ hiWordOfBasis)) ^ ((hash4 * (basis ^ hiWordOfBasis)) >> rotation1))) ^ ((hash3 * ((hash4 * (basis ^ hiWordOfBasis)) ^ ((hash4 * (basis ^ hiWordOfBasis)) >> rotation1))) >> shift1);
    }

    /// <summary>
    /// Calculates and writes a Murmur3 hash signature to the end of the specified file data buffer.
    /// </summary>
    /// <remarks>Thanks to windwakr (https://github.com/windwakr) for identifying this hashing method as MurmurHash3_32.</remarks>
    /// <typeparam name="T">The value type of each element in the file data buffer.</typeparam>
    /// <param name="fileData">A span representing the file data to be signed. The signature will be written to the last element of this span.</param>
    private static void SignFile<T>(ref Span<T> fileData) where T : struct
    {
        var span = MemoryMarshal.Cast<T, uint>(fileData);
        span[^1] = Murmur3_32(span[..^1], 0xFFFFFFFF);
    }
}