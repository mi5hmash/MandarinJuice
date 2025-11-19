using MandarinJuiceCore.Models.DSSS.Mandarin;
using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using System.Security.Cryptography;
using Aes = System.Runtime.Intrinsics.X86.Aes;
using AesNative = System.Security.Cryptography.Aes;

namespace MandarinJuiceCore.Helpers;

public class MandarinDeencryptor(ulong mandarinSeed = 0)
{
    #region CONSTANTS

    private const byte KeyType = 0x14;
    private const byte ContainerCapacityInUlongs = 0x20;

    private const byte HeaderDataKeyCount = 0x4;
    private const byte HeaderDataKeySizeInUlongs = 0x8;
    private const byte HeaderFooterSizeInBytes = 0x10;
    private const ushort HeaderDataSizeInBytes = 2 * HeaderDataKeyCount * HeaderDataKeySizeInUlongs * sizeof(ulong) + HeaderFooterSizeInBytes;
    private const byte ChecksumContainerCapacityInBytes = HeaderDataKeyCount * sizeof(ulong);

    private static readonly ulong[] PrivateKey1 = "8ztvuXKgtyUV5Fw5GCnhgq2Km9wKZNNETXnIEKuGNxc=".FromBase64<ulong>();
    private static readonly ulong[] PrivateKey2 = "+Z23XDnQ25IKcq4cjJRwwVbFTW4FsmmipjxkiFXDmws=".FromBase64<ulong>();
    private static readonly ulong[] PrivateKey3 = "5m9USvzOaMXvB7mgeyd1hTRKHbYTdugx9zufvV9E9xU=".FromBase64<ulong>();
    
    private static readonly ulong[] HeaderKey = CreateKey(KeyType);
    
    /// <summary>
    /// Represents the AES encryption platform currently supported by the environment.
    /// </summary>
    private static readonly AesEncryptionPlatform CurrentAesEncryptionPlatform = GetSupportedAesEncryption();

    #endregion

    #region PROPERTIES

    /// <summary>
    /// Gets or sets the seed value used for Mandarin-related randomization processes.
    /// </summary>
    public ulong MandarinSeed { get; set; } = mandarinSeed;
    
    #endregion

    #region AES_ENCRYPTION_PLATFORM

    /// <summary>
    /// Determines whether both AES and SSE2 hardware intrinsics are supported on the current platform.
    /// </summary>
    /// <returns><see langword="true"/> if both AES and SSE2 intrinsics are available; otherwise, <see langword="false"/>.</returns>
    public static bool IsIntrinsicsSupported()
        => Aes.IsSupported && Sse2.IsSupported;

    /// <summary>
    /// Determines whether software-based AES encryption is supported on the current platform.
    /// </summary>
    /// <returns><see langword="true"/> if software AES encryption is available; otherwise, <see langword="false"/>.</returns>
    public static bool IsSoftwareAesSupported()
    {
        try
        {
            using (AesNative.Create())
                return true;
        }
        catch (PlatformNotSupportedException)
        {
            return false;
        }
    }

    /// <summary>
    /// Specifies the platform used to perform AES encryption operations.
    /// </summary>
    public enum AesEncryptionPlatform
    {
        Hardware,
        Software
    }

    /// <summary>
    /// Determines the supported AES encryption platform. Performs hardware and software checks to identify if AES encryption is supported on the current platform and returns the corresponding platform type.
    /// </summary>
    /// <returns>AesEncryptionPlatform value indicating the supported platform.</returns>
    /// <exception cref="PlatformNotSupportedException"></exception>
    public static AesEncryptionPlatform GetSupportedAesEncryption()
    {
        // check for hardware support first
        if (IsIntrinsicsSupported())
            return AesEncryptionPlatform.Hardware;
        // check for software support next
        return IsSoftwareAesSupported() ? AesEncryptionPlatform.Software : throw new PlatformNotSupportedException();
    }

    /// <summary>
    /// Deencrypts the specified input data using the provided encryption key and the selected AES encryption platform.
    /// </summary>
    /// <param name="inputData">The span of bytes containing the data to be deencrypted.</param>
    /// <param name="encryptionKey">A span of bytes representing the encryption key and, for software mode, additional state information.</param>
    private static void DeencryptData(Span<byte> inputData, Span<byte> encryptionKey)
    {
        switch (CurrentAesEncryptionPlatform)
        {
            case AesEncryptionPlatform.Hardware:
                var dataAsVectors = MemoryMarshal.Cast<byte, Vector128<byte>>(inputData);
                var encryptionKeyAsVectors = MemoryMarshal.Cast<byte, Vector128<byte>>(encryptionKey);
                DeencryptIntrinsics(dataAsVectors, encryptionKeyAsVectors);
                return;
            default:
            case AesEncryptionPlatform.Software:
                var key = encryptionKey[..16].ToArray();
                var state = encryptionKey[16..];
                AesDeencryptSoftwareBased(inputData, key, state);
                return;
        }
    }

    #endregion

    #region HELPERS

    /// <summary>
    /// Advances the given state using the SplitMix64 algorithm, producing a new pseudo-random value.
    /// </summary>
    /// <param name="state">The state value to be updated. The value is modified in place to its next pseudo-random state.</param>
    private static void Splitmix64(ref ulong state)
    {
        state += 0x9E3779B97F4A7C15;
        state = (state ^ (state >> 0x1E)) * 0xBF58476D1CE4E5B9;
        state = (state ^ (state >> 0x1B)) * 0x94D049BB133111EB;
        state ^= state >> 0x1F;
    }

    /// <summary>
    /// Applies the SplitMix64 mixing function to the specified key a given number of times.
    /// </summary>
    /// <param name="key">The 64-bit unsigned integer to be mixed. The value is updated in place.</param>
    /// <param name="laps">The number of times to apply the mixing function.</param>
    private static void Splitmixer64(ref ulong key, long laps = 1)
    {
        for (var i = 0; i < laps; i++) Splitmix64(ref key);
    }

    /// <summary>
    /// Calculates the upper 64 bits of the 128-bit product of two 64-bit unsigned integers.
    /// </summary>
    /// <remarks>Based on: https://gist.github.com/cocowalla/6070a53445e872f2bb24304712a3e1d2.</remarks>
    /// <param name="left">The first 64-bit unsigned integer to multiply.</param>
    /// <param name="right">The second 64-bit unsigned integer to multiply.</param>
    /// <returns>The upper 64 bits of the 128-bit product of the specified operands.</returns>
    private static ulong MulHigh(ulong left, ulong right)
    {
        const byte shift = 0x20;

        ulong l0 = (uint)left;
        var l1 = left >> shift;
        ulong r0 = (uint)right;
        var r1 = right >> shift;

        var p11 = l1 * r1;
        var p01 = l0 * r1;
        var p10 = l1 * r0;
        var p00 = l0 * r0;

        // 64-bit product + two 32-bit values
        var middle = p10 + (p00 >> shift) + (uint)p01;

        // 64-bit product + two 32-bit values
        return p11 + (middle >> shift) + (p01 >> shift);
    }

    /// <summary>
    /// Calculates the lower 64 bits of the product of two unsigned 64-bit integers.
    /// </summary>
    /// <param name="left">The first unsigned 64-bit integer to multiply.</param>
    /// <param name="right">The second unsigned 64-bit integer to multiply.</param>
    /// <returns>The lower 64 bits of the product of the specified values.</returns>
    private static ulong MulLow(ulong left, ulong right)
        => left * right;

    /// <summary>
    /// Calculates the number of times the specified unsigned integer can be right-shifted by the given step before reaching zero.
    /// </summary>
    /// <param name="radicand">The unsigned integer value to be repeatedly right-shifted.</param>
    /// <param name="step">The number of bits to shift right in each iteration.</param>
    /// <returns>The number of iterations required to reduce the radicand to zero by right-shifting it by the specified step each time.</returns>
    private static int RootDegree(ulong radicand, int step)
    {
        var index = 0;
        do
        {
            index++;
            radicand >>= step;
        }
        while (radicand != 0);
        return index;
    }

    /// <summary>
    /// Determines whether the most significant bit of the specified 32-bit unsigned integer is set.
    /// </summary>
    /// <param name="number">The 32-bit unsigned integer to evaluate.</param>
    /// <returns><see langword="true"/> if the most significant bit of number is set; otherwise, <see langword="false"/>.</returns>
    private static bool IsMostSignificantBitSet(uint number)
        => (number & 0x80000000) != 0;

    /// <summary>
    /// Determines whether the most significant bit of the specified 64-bit unsigned integer is set.
    /// </summary>
    /// <param name="number">The 64-bit unsigned integer to evaluate.</param>
    /// <returns><see langword="true"/> if the most significant bit of number is set; otherwise, <see langword="false"/>.</returns>
    private static bool IsMostSignificantBitSet(ulong number)
        => (number & 0x8000000000000000) != 0;

    /// <summary>
    /// Fills the specified span of bytes with random values, starting at the given index.
    /// </summary>
    /// <param name="span">The span of bytes to populate with random values.</param>
    /// <param name="start">The zero-based index at which to begin randomizing the span.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="start"/> is less than 0 or greater than the length of <paramref name="span"/>.</exception>
    private static void RandomizeSpan(Span<byte> span, int start = 0)
    {
        if (start < 0 || start > span.Length)
            throw new ArgumentOutOfRangeException(nameof(start), "Start index is out of range for the span.");
        Random random = new();
        for (var i = start; i < span.Length; i++)
            span[i] = (byte)random.Next(byte.MaxValue + 1);
    }

    /// <summary>
    /// Computes the bitwise complement of the specified user identifier.
    /// </summary>
    /// <param name="userId">The user identifier to invert.</param>
    /// <returns>An unsigned 64-bit integer representing the bitwise complement of the input user ID.</returns>
    private static ulong NotUserId(ulong userId) => ~userId;

    /// <summary>
    /// Compares two containers in reverse lexicographical order to determine if <paramref name="containerA"/> is less than <paramref name="containerB"/>.
    /// Starting from the last element, it checks each element pair by pair until a difference is found or all elements have been compared.
    /// </summary>
    /// <param name="containerA">The first container to compare.</param>
    /// <param name="containerB">The second container to compare.</param>
    /// <returns>Returns <see langword="true"/> if <paramref name="containerA"/> is reverse-ordered less than <paramref name="containerB"/>; otherwise, returns <see langword="false"/>.</returns>
    /// <exception cref="ArgumentException"></exception>
    private static bool IsReverseOrderedLess(ReadOnlySpan<ulong> containerA, ReadOnlySpan<ulong> containerB)
    {
        if (containerA.Length != containerB.Length) throw new ArgumentException("The two containers must have the same length.");
        for (var i = containerA.Length; i > 0; i--)
        {
            // Check if nth element of containerA is less than containerB
            if (containerA[i - 1] < containerB[i - 1]) return true;
            // Check if nth element of containerA is greater than containerB
            if (containerA[i - 1] > containerB[i - 1]) return false;
        }
        return false;
    }

    /// <summary>
    /// Finds the zero-based index of the last element in the span that is not equal to the default value of type <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The value type of the elements in the span. Must implement <see cref="IEquatable{T}"/> to support equality comparison with the default value.</typeparam>
    /// <param name="span">The read-only span of value type elements to search.</param>
    /// <returns>The zero-based index of the last non-default element in the span; returns 0 if all elements are equal to the default value.</returns>
    private static int LastNonZeroIndexZeroBased<T>(ReadOnlySpan<T> span) where T : struct, IEquatable<T>
    {
        for (var i = span.Length; i > 0; i--)
            if (!span[i - 1].Equals(default))
                return i;
        return 0;
    }

    /// <summary>
    /// Initializes the specified container by setting the element at the given position to the provided cargo value and clearing all other elements.
    /// </summary>
    /// <typeparam name="T">The value type of the elements contained in the span.</typeparam>
    /// <param name="container">The span to be initialized. All elements except the one at the specified position will be cleared.</param>
    /// <param name="cargo">The value to assign to the element at the specified position within the container.</param>
    /// <param name="position">The zero-based index at which to place the cargo value. Defaults to 0.</param>
    private static void SetupContainer<T>(Span<T> container, T cargo, int position = 0) where T : struct
    {
        container[position] = cargo;
        container[..position].Clear();
        container[(position + 1)..].Clear();
    }

    /// <summary>
    /// Initializes a segment of the specified container span with the contents of the cargo span, starting at the given position, and clears all other elements in the container.
    /// </summary>
    /// <typeparam name="T">The value type of the elements contained in the spans.</typeparam>
    /// <param name="container">The span to be initialized and cleared. Elements outside the cargo segment will be set to their default value.</param>
    /// <param name="cargo">The read-only span whose contents are copied into the container starting at the specified position.</param>
    /// <param name="position">The zero-based index in the container at which to begin copying the cargo. Defaults to 0.</param>
    private static void SetupContainer<T>(Span<T> container, ReadOnlySpan<T> cargo, int position = 0) where T : struct
    {
        cargo.CopyTo(container[position..]);
        container[..position].Clear();
        container[(position + cargo.Length)..].Clear();
    }

    /// <summary>
    /// Initializes the specified container by placing the provided cargo at the given position asynchronously.
    /// </summary>
    /// <typeparam name="T">The value type of the elements contained in the memory buffer.</typeparam>
    /// <param name="container">The memory buffer to be initialized. Represents a contiguous region of memory for value type elements.</param>
    /// <param name="cargo">The value to be placed into the container at the specified position.</param>
    /// <param name="position">The zero-based index within the container at which to place the cargo. Defaults to 0.</param>
    /// <returns>A task that represents the asynchronous initialization operation.</returns>
    private static async Task SetupContainerAsync<T>(Memory<T> container, T cargo, int position = 0) where T : struct
        => await Task.Run(() => SetupContainer(container.Span, cargo, position));

    /// <summary>
    /// Initializes the specified container with the provided cargo, starting at the given position asynchronously.
    /// </summary>
    /// <typeparam name="T">The value type of the elements contained in both the container and cargo memories.</typeparam>
    /// <param name="container">The memory region to be set up with the cargo data. Must be large enough to accommodate the cargo at the specified position.</param>
    /// <param name="cargo">The read-only memory containing the data to be placed into the container.</param>
    /// <param name="position">The zero-based index in the container at which to begin placing the cargo. Must be within the bounds of the container.</param>
    /// <returns>A task that represents the asynchronous setup operation.</returns>
    private static async Task SetupContainerAsync<T>(Memory<T> container, ReadOnlyMemory<T> cargo, int position = 0) where T : struct
        => await Task.Run(() => SetupContainer(container.Span, cargo.Span, position));

    /// <summary>
    /// Calculates and returns a queue of slice capacities for partitioning data of the specified length.
    /// </summary>
    /// <param name="state">A reference to the state value used for slice capacity calculation. The value is updated during the operation.</param>
    /// <param name="dataLength">The total length of the data to be partitioned into slices. Must be a non-negative value.</param>
    /// <returns>A SlicesQueue containing the capacities of each slice determined for the given data length.</returns>
    private static MandarinSlicesQueue CalculateSlicesQueue(ref ulong state, uint dataLength)
    {
        const byte shift = 0xE;
        var mandarinQueue = new MandarinSlicesQueue(shift);

        var laps = (dataLength >> shift) + 1;

        for (var i = 0; i < laps; i++)
        {
            if (dataLength > 0)
            {
                var sliceCapacity = (byte)(((byte)state & 7) + 1);
                mandarinQueue.Enqueue(sliceCapacity);
                var sliceLength = (uint)(sliceCapacity << shift);
                dataLength = dataLength >= sliceLength ? dataLength - sliceLength : 0;
            }
            Splitmix64(ref state);
        }

        return mandarinQueue;
    }

    #endregion

    #region ENCRYPTION METHODS

    /// <summary>
    /// Generates an encryption key based on the specified seed and returns a sequence of unsigned 64-bit integers representing the key.
    /// </summary>
    /// <param name="seed">The input value used as the basis for key generation.</param>
    /// <param name="length">The number of elements to include in the returned key. If set to 0, the method automatically determines the length based on the generated data.</param>
    /// <returns>An array of unsigned 64-bit integers containing the generated encryption key.</returns>
    private static ulong[] CreateKey(ulong seed, int length = 0)
    {
        // Create localContainerA
        Span<ulong> localContainerA = stackalloc ulong[ContainerCapacityInUlongs];
        SetupContainer(localContainerA, seed);
        // Create resultContainer
        Span<ulong> resultContainer = stackalloc ulong[ContainerCapacityInUlongs];
        SetupContainer(resultContainer, PrivateKey2);
        // Create localContainerB
        Span<ulong> localContainerB = stackalloc ulong[ContainerCapacityInUlongs];
        // Execute a set of encryption methods
        SetOfEncryptionMethods(localContainerA, resultContainer, localContainerB);
        // Prepare resultContainer
        SetupContainer(resultContainer, PrivateKey3);
        // Prepare localContainerB
        SetupContainer(localContainerB, PrivateKey1);
        // Calculate a key and return it
        Limegator(resultContainer, localContainerA, localContainerB);
        length = length == 0 ? LastNonZeroIndexZeroBased(resultContainer) : length;
        return resultContainer[..length].ToArray();
    }

    /// <summary>
    /// Subtracts two containers from one another.
    /// </summary>
    /// <param name="containerA">The container from which a <paramref name="containerB"/> will be deducted.</param>
    /// <param name="containerB">The container which will be deducted from the <paramref name="containerA"/></param>
    /// <returns>Modifies <paramref name="containerA"/>.</returns>
    private static void SubtractContainers(Span<ulong> containerA, ReadOnlySpan<ulong> containerB)
    {
        byte testA = 0;
        byte testB = 0;
        for (var i = 0; i < containerA.Length; i++)
        {
            var test0 = Convert.ToByte(testA | testB);
            var newValue = containerA[i] - containerB[i] - test0;
            testA = containerA[i] == newValue ? test0 : (byte)0;
            testB = Convert.ToByte(containerA[i] < newValue);
            containerA[i] = newValue;
        }
    }

    /// <summary>
    /// Adds two containers to one another.
    /// </summary>
    /// <param name="containerA">The first container to add.</param>
    /// <param name="containerB">The second container to add.</param>
    /// <returns>Modifies <paramref name="containerA"/>.</returns>
    private static void AddContainers(Span<ulong> containerA, ReadOnlySpan<ulong> containerB)
    {
        byte testA = 0;
        byte testB = 0;
        for (var i = 0; i < containerA.Length; i++)
        {
            var test0 = Convert.ToByte(testA | testB);
            var newValue = containerA[i] + containerB[i] + test0;
            testA = containerB[i] == newValue ? test0 : (byte)0;
            testB = Convert.ToByte(newValue < containerB[i]);
            containerA[i] = newValue;
        }
    }

    /// <summary>
    /// Performs AES-based decryption on a span of 128-bit data blocks using the specified key schedule. The operation modifies the input data in place.
    /// </summary>
    /// <param name="data">A span of 128-bit vectors representing the data blocks to be decrypted. The contents of this span will be overwritten with the decrypted results.</param>
    /// <param name="key">A read-only span of 128-bit vectors representing the AES key schedule. Must contain at least one element for key expansion.</param>
    public static void DeencryptIntrinsics(Span<Vector128<byte>> data, ReadOnlySpan<Vector128<byte>> key)
    {
        const byte rounds = 0xA;
        const int shift = 4;
        Span<Vector128<byte>> aesRoundKeys = stackalloc Vector128<byte>[rounds + 1];

        //// AES KEYGEN
        // Build the first block (Expand AES-128 key)
        aesRoundKeys[0] = key[0];
        for (var i = 0; i < rounds; i++)
        {
            var innerRoundKey = i switch
            {
                // AES-128(128 - bit key): 10 rounds
                0 => Aes.KeygenAssist(aesRoundKeys[i], 0x01),
                1 => Aes.KeygenAssist(aesRoundKeys[i], 0x02),
                2 => Aes.KeygenAssist(aesRoundKeys[i], 0x04),
                3 => Aes.KeygenAssist(aesRoundKeys[i], 0x08),
                4 => Aes.KeygenAssist(aesRoundKeys[i], 0x10),
                5 => Aes.KeygenAssist(aesRoundKeys[i], 0x20),
                6 => Aes.KeygenAssist(aesRoundKeys[i], 0x40),
                7 => Aes.KeygenAssist(aesRoundKeys[i], 0x80),
                8 => Aes.KeygenAssist(aesRoundKeys[i], 0x1B),
                9 => Aes.KeygenAssist(aesRoundKeys[i], 0x36),
                _ => Aes.KeygenAssist(aesRoundKeys[i], 0x8D)
            };
            // Shift xmm2 left by 4 bytes
            var shift1 = Sse2.ShiftLeftLogical128BitLane(aesRoundKeys[i].AsUInt32(), shift).AsInt32();
            // Shift shift1 left by 4 bytes
            var shift2 = Sse2.ShiftLeftLogical128BitLane(shift1.AsUInt32(), shift).AsInt32();
            // Shift shift2 left by 4 bytes
            var shift3 = Sse2.ShiftLeftLogical128BitLane(shift2, shift);
            // Compute the final result using shuffle and XOR instructions
            var shuffle1 = Sse2.Shuffle(innerRoundKey.AsInt32(), 255);
            var xor1 = Sse2.Xor(shift1, aesRoundKeys[i].AsInt32());
            var xor2 = Sse2.Xor(shift2, xor1);
            var xor3 = Sse2.Xor(xor2, shift3);
            var xor4 = Sse2.Xor(xor3, shuffle1);
            // Add key to the aesRoundKeys
            aesRoundKeys[i + 1] = xor4.AsByte();
        }

        //// AES ENCRYPT
        var state = key[^1];
        for (var i = 0; i < data.Length; i++)
        {
            state = Sse2.Xor(state, aesRoundKeys[0]);
            for (var y = 1; y < rounds; y++)
                state = Aes.Encrypt(state, aesRoundKeys[y]);
            state = Aes.EncryptLast(state, aesRoundKeys[rounds]);
            // Decrypt row of input data
            data[i] = Sse2.Xor(data[i], state);
        }
    }

    /// <summary>
    /// Performs AES encryption-based transformation on the specified data buffer using the provided key and state. The operation modifies the input data in place.
    /// </summary>
    /// <param name="data">The buffer containing the data to be transformed. The contents of this span will be updated in place.</param>
    /// <param name="key">The AES key used for encryption. Must be a valid key length supported by AES (typically 16, 24, or 32 bytes).</param>
    /// <param name="state">A buffer representing the current state for the transformation. The contents of this span will be updated during the operation.</param>
    public static void AesDeencryptSoftwareBased(Span<byte> data, byte[] key, Span<byte> state)
    {
        using var aes = AesNative.Create();
        aes.Key = key;
        aes.Mode = CipherMode.ECB;
        aes.Padding = PaddingMode.Zeros;
        using var encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
        for (var i = 0; i < data.Length; i += 16)
        {
            using (var ms = new MemoryStream())
            {
                using var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write);
                cs.Write(state);
                cs.FlushFinalBlock();
                ms.ToArray().CopyTo(state);
            }
            // Decrypt row of input data
            for (var j = 0; j < state.Length; j++)
                data[i + j] ^= state[j];
        }
    }

    /// <summary>
    /// Executes set of encryption methods on provided containers.
    /// </summary>
    /// <param name="containerA">The first container to use.</param>
    /// <param name="containerB">The second container to use.</param>
    /// <param name="localContainer">The third container to use.</param>
    /// <returns>Modifies <paramref name="containerA"/>.</returns>
    private static void SetOfEncryptionMethods(Span<ulong> containerA, ReadOnlySpan<ulong> containerB, Span<ulong> localContainer)
    {
        // prepare localContainer
        containerA.CopyTo(localContainer);
        // execute set of encryption methods
        Limeghetti(localContainer, containerB);
        EncryptionFirst(localContainer, containerB);
        SubtractContainers(containerA, localContainer);
    }

    /// <summary>
    /// Handles overflow in the provided <paramref name="containerA"/>.
    /// </summary>
    /// <param name="containerA"></param>
    /// <param name="containerB"></param>
    /// <returns>Modifies <paramref name="containerA"/>.</returns>
    private static void HandleOverflow(Span<ulong> containerA, Span<ulong> containerB)
    {
        // Setup containerB
        SetupContainer(containerB, (ulong)1);

        SubtractContainers(containerA, containerB);
        const byte x = 0x2;
        for (var i = 0; i < ContainerCapacityInUlongs - x; i++)
            containerA[i] = ~containerA[i] & 0xFFFFFFFFFFFFFFFF;
        for (var i = ContainerCapacityInUlongs - x; i < ContainerCapacityInUlongs; i++)
            containerA[i] = ~containerA[i];
    }

    /// <summary>
    /// Prepare a delicious knot of Limeghetti.
    /// </summary>
    /// <param name="containerA">The first container to use.</param>
    /// <param name="containerB">The second container to use.</param>
    /// <returns>Modifies <paramref name="containerA"/>.</returns>
    private static void Limeghetti(Span<ulong> containerA, ReadOnlySpan<ulong> containerB)
    {
        // Check for empty containers
        var containerALength = LastNonZeroIndexZeroBased(containerA);
        if (containerALength == 0) return;
        var containerBLength = LastNonZeroIndexZeroBased(containerB);
        if (containerBLength == 0)
        {
            // ORDER_66
            // Set all the containerA elements to 0
            containerA.Clear();
            return;
        }

        // Create a localContainerB
        Span<ulong> localContainerB = stackalloc ulong[ContainerCapacityInUlongs];
        containerB.CopyTo(localContainerB);

        // Create other localContainers
        Span<ulong> localContainerC = stackalloc ulong[ContainerCapacityInUlongs];
        Span<ulong> localContainerD = stackalloc ulong[ContainerCapacityInUlongs];
        Span<ulong> localContainerE = stackalloc ulong[ContainerCapacityInUlongs];
        Span<ulong> resultContainer = stackalloc ulong[ContainerCapacityInUlongs];
        // Clear resultContainer
        resultContainer.Clear();

        while (true)
        {
            // Detect overflow in...
            // ... localContainerA
            if (IsMostSignificantBitSet(containerA[^1]))
                HandleOverflow(containerA, localContainerC);

            // ... localContainerB
            if (IsMostSignificantBitSet(localContainerB[^1]))
                HandleOverflow(localContainerB, localContainerC);

            if (IsMostSignificantBitSet(containerA[^1]))
            {
                if (!IsMostSignificantBitSet(localContainerB[^1])) break; // ORDER_66
                if (IsReverseOrderedLess(containerA, localContainerB)) break; // ORDER_66
            }
            else if (!IsMostSignificantBitSet(localContainerB[^1]))
            {
                if (IsReverseOrderedLess(containerA, localContainerB)) break; // ORDER_66
            }

            // Check container length
            var localContainerBLength = LastNonZeroIndexZeroBased(localContainerB);
            int localContainerALength;

            // Calculate bits
            var rootDegree = localContainerBLength == 0 ? 0 : RootDegree(localContainerB[localContainerBLength - 1], 1);
            var bits = 32 - (rootDegree & 0x1F);

            // Perform EncryptionSecond on both localContainers
            EncryptionSecond(containerA, bits);
            EncryptionSecond(localContainerB, bits);

            // Re-check container length
            localContainerBLength = LastNonZeroIndexZeroBased(localContainerB);

            // Remember the last element of containerA
            var lastElementA = containerA[^1];
            var lastElementB = containerA[^1];

            if (localContainerBLength > 0)
            {
                var tinyHashesB = 2 * localContainerBLength - 1;
                var lastQueueElemB = localContainerB[localContainerBLength - 1] >> 32;
                if (lastQueueElemB == 0)
                {
                    tinyHashesB--;
                    lastQueueElemB = localContainerB[localContainerBLength - 1];
                }

                while (true)
                {
                    //LOOP_BREAKER
                    lastElementA = containerA[^1];
                    if (IsMostSignificantBitSet(containerA[^1]))
                    {
                        if (!IsMostSignificantBitSet(lastElementB)) break; // ESCAPE
                        if (IsReverseOrderedLess(containerA, localContainerB)) break; // ESCAPE
                    }
                    else if (!IsMostSignificantBitSet(lastElementB) && IsReverseOrderedLess(containerA, localContainerB)) break; // ESCAPE

                    // Re-check container length
                    localContainerALength = LastNonZeroIndexZeroBased(containerA);

                    if (localContainerALength == 0) break; // ESCAPE
                    if (localContainerALength < 2) continue;
                    var tinyHashesA = 2 * localContainerALength - 2;
                    var lastQueueElemA = containerA[localContainerALength - 1];
                    if (lastQueueElemA >> 32 == 0)
                    {
                        tinyHashesA--;
                        lastQueueElemA = (containerA[localContainerALength - 2] >> 32) + (lastQueueElemA << 32);
                    }

                    var hashesGap = tinyHashesA - tinyHashesB;
                    var lastQueueElemDiv = lastQueueElemA / lastQueueElemB;

                    // Copy localContainerB into localContainerC
                    localContainerB.CopyTo(localContainerC);

                    if (tinyHashesA >= tinyHashesB)
                    {
                        if (lastQueueElemDiv >> 32 != 0) lastQueueElemDiv = 0xFFFFFFFF;
                        EncryptionSecond(localContainerC, 32 * hashesGap);
                        // Prepare localContainerD
                        localContainerD[0] = lastQueueElemDiv;
                        localContainerD[1..].Clear();
                        EncryptionFirst(localContainerC, localContainerD);
                    }
                    else
                    {
                        hashesGap = 0;
                        lastQueueElemDiv = 1;
                    }

                    while (true)
                    {
                        if (!IsMostSignificantBitSet(lastElementA))
                        {
                            if (IsMostSignificantBitSet(localContainerC[^1])) break;
                        }
                        else if (!IsMostSignificantBitSet(localContainerC[^1]))
                        {
                            LocalSubtractionProcess(ref lastQueueElemDiv, localContainerB, localContainerC, localContainerD, hashesGap);
                            continue;
                        }

                        if (!IsReverseOrderedLess(containerA, localContainerC)) break;
                        LocalSubtractionProcess(ref lastQueueElemDiv, localContainerB, localContainerC, localContainerD, hashesGap);
                    }

                    // Prepare localContainerD
                    localContainerD.Clear();
                    // Prepare localContainerE
                    localContainerE[0] = lastQueueElemDiv;
                    localContainerE[1..].Clear();

                    AddContainers(localContainerD, localContainerE);
                    EncryptionSecond(localContainerD, 32 * hashesGap);
                    lastElementB = localContainerB[^1];

                    // Calculate resultContainer
                    AddContainers(resultContainer, localContainerD);

                    // Prepare localContainerD
                    localContainerC.CopyTo(localContainerD);
                    // Update containerA
                    SubtractContainers(containerA, localContainerD);
                }
            }
            // ESCAPE
            localContainerALength = LastNonZeroIndexZeroBased(containerA);
            localContainerBLength = LastNonZeroIndexZeroBased(localContainerB);

            while (true)
            {
                if ((localContainerBLength > 0 && localContainerALength == 0) || localContainerB.SequenceEqual(containerA))
                {
                    LocalAdditionProcess(containerA, localContainerB, localContainerE, resultContainer);
                    break;
                }

                if ((IsMostSignificantBitSet(lastElementB) && IsMostSignificantBitSet(lastElementA)) || !IsMostSignificantBitSet(lastElementA))
                {
                    if (!IsReverseOrderedLess(localContainerB, containerA)) break;
                    LocalAdditionProcess(containerA, localContainerB, localContainerE, resultContainer);
                }
                break;
            }
            // RETURNAL
            resultContainer.CopyTo(containerA);
            return;
        }
        // ORDER_66
        // Set all the containerA elements to 0
        containerA.Clear();
        return;

        static void LocalSubtractionProcess(ref ulong lastQueueElemDiv, ReadOnlySpan<ulong> localContainerB, Span<ulong> localContainerC, Span<ulong> localContainerD, int hashesGap)
        {
            localContainerB.CopyTo(localContainerD);
            EncryptionSecond(localContainerD, 32 * hashesGap);
            SubtractContainers(localContainerC, localContainerD);
            lastQueueElemDiv--;
        }

        static void LocalAdditionProcess(ReadOnlySpan<ulong> localContainerA, ReadOnlySpan<ulong> localContainerB, Span<ulong> localContainerE, Span<ulong> resultContainer)
        {
            SetupContainer(localContainerE, localContainerA[0] / localContainerB[0]);
            AddContainers(resultContainer, localContainerE);
        }
    }

    /// <summary>
    /// Watch out for its sharp teeth!
    /// </summary>
    /// <param name="containerA">The first container to use.</param>
    /// <param name="containerB">The second container to use.</param>
    /// <param name="containerC">The third container to use.</param>
    /// <returns>Modifies <paramref name="containerA"/></returns>
    private static void Limegator(Span<ulong> containerA, ReadOnlySpan<ulong> containerB, ReadOnlySpan<ulong> containerC)
    {
        // Create resultContainer
        Span<ulong> resultContainer = stackalloc ulong[ContainerCapacityInUlongs];
        SetupContainer<ulong>(resultContainer, 1);

        // Create localContainerB
        Span<ulong> localContainerB = stackalloc ulong[ContainerCapacityInUlongs];
        containerB.CopyTo(localContainerB);

        // Create other localContainers
        Span<ulong> localContainerA = stackalloc ulong[ContainerCapacityInUlongs];
        Span<ulong> localContainerC = stackalloc ulong[ContainerCapacityInUlongs];

        while (true)
        {
            localContainerA.Clear();
            if (LoopBreaker(localContainerB, localContainerA)) break;

            if (((byte)localContainerB[0] & 1) != 0)
            {
                resultContainer.CopyTo(localContainerA);
                EncryptionFirst(localContainerA, containerA);
                SetOfEncryptionMethods(localContainerA, containerC, localContainerC);
                localContainerA.CopyTo(resultContainer);
            }
            containerA.CopyTo(localContainerA);
            containerA.CopyTo(localContainerC);
            EncryptionFirst(localContainerA, localContainerC);
            SetOfEncryptionMethods(localContainerA, containerC, localContainerC);
            localContainerA.CopyTo(containerA);

            for (var i = 0; i < ContainerCapacityInUlongs - 1; i++)
            {
                localContainerB[i] >>= 1;
                localContainerB[i] |= localContainerB[i + 1] << 63;
            }
            localContainerB[^1] >>= 1;
        }

        resultContainer.CopyTo(containerA);
        return;

        static bool LoopBreaker(ReadOnlySpan<ulong> containerLeft, ReadOnlySpan<ulong> containerRight)
            => LastNonZeroIndexZeroBased(containerLeft) == 0 || IsReverseOrderedLess(containerLeft, containerRight);
    }

    /// <summary>
    /// First type of encryption.
    /// </summary>
    /// <param name="containerA">The first container to use.</param>
    /// <param name="containerB">The second container to use.</param>
    /// <returns>Modifies <paramref name="containerA"/>.</returns>
    private static void EncryptionFirst(Span<ulong> containerA, ReadOnlySpan<ulong> containerB)
    {
        // Check for empty containers
        var containerALength = LastNonZeroIndexZeroBased(containerA);
        if (containerALength == 0) return;
        var containerBLength = LastNonZeroIndexZeroBased(containerB);
        if (containerBLength == 0)
        {
            // Set all the containerA elements to 0
            containerA.Clear();
            return;
        }

        // Create a resultContainer
        Span<ulong> resultContainer = stackalloc ulong[ContainerCapacityInUlongs];
        // Create a localContainerB
        Span<ulong> localContainerB = stackalloc ulong[ContainerCapacityInUlongs];
        containerB.CopyTo(localContainerB);

        while (true)
        {
            // Detect overflow in...
            var overflowSwitch = false;
            // ... LocalContainerA
            if (IsMostSignificantBitSet(containerA[^1]))
            {
                overflowSwitch ^= true;
                HandleOverflow(containerA, resultContainer);
                // Re-check container length
                containerALength = LastNonZeroIndexZeroBased(containerA);
            }

            // ... LocalContainerB
            if (IsMostSignificantBitSet(localContainerB[^1]))
            {
                overflowSwitch ^= true;
                HandleOverflow(localContainerB, resultContainer);
                // Re-check container length
                containerBLength = LastNonZeroIndexZeroBased(localContainerB);
                if (containerBLength == 0) break;
            }

            // Prepare resultContainer
            resultContainer.Clear();

            // Manipulate bytes in both containers
            if (containerALength > 0)
            {
                for (var y = 0; y < containerALength; y++)
                {
                    ulong salt = 0;
                    if (containerBLength > 0)
                    {
                        var firstPart = containerA[y];
                        for (var i = 0; i < containerBLength; i++)
                        {
                            var lowBytes = MulLow(firstPart, localContainerB[i]);
                            var highBytes = MulHigh(firstPart, localContainerB[i]);
                            var basis = lowBytes + salt;
                            if (basis < salt) highBytes++;
                            var result = basis + resultContainer[y + i];
                            if (result < basis) highBytes++;
                            resultContainer[y + i] = result;
                            salt = highBytes;
                        }
                    }
                    resultContainer[containerBLength + y] = salt;
                }
            }

            // if there was only one overflow then pick an alternative route
            if (overflowSwitch) break;

            // Update referenced containerA
            resultContainer.CopyTo(containerA);
            return;
        }

        containerA.Clear();
        SubtractContainers(containerA, resultContainer);
    }

    /// <summary>
    /// Second type of encryption.
    /// </summary>
    /// <param name="dataContainer"></param>
    /// <param name="bits"></param>
    /// <returns>Modifies <paramref name="dataContainer"/>.</returns>
    private static void EncryptionSecond(Span<ulong> dataContainer, int bits)
    {
        var division = bits >> 6; // Division by 64
        var reminder = bits & 0x3F; // Division reminder

        if (reminder != 0)
        {
            if (division != ContainerCapacityInUlongs && division != ContainerCapacityInUlongs - 1)
            {
                var curElement = ContainerCapacityInUlongs;
                for (var i = ContainerCapacityInUlongs - division; i > 1; i--, curElement--)
                {
                    dataContainer[curElement - 1] = dataContainer[i - 1] << reminder;
                    dataContainer[curElement - 1] |= dataContainer[i - 2] >> (0x40 - reminder);
                }
            }
            dataContainer[division] = dataContainer[0] << reminder;
        }
        else if (division != 0)
        {
            var curElement = ContainerCapacityInUlongs - division;
            if (division != ContainerCapacityInUlongs)
            {
                for (var i = ContainerCapacityInUlongs; curElement > 0; i--, curElement--)
                    dataContainer[i - 1] = dataContainer[curElement - 1];
            }
        }

        if (division == 0) return;
        for (var i = division; i > 0; i--)
            dataContainer[division - i] = 0;
    }

    /// <summary>
    /// Calculates a 64-bit checksum for the specified slice of byte data using a custom mixing algorithm.
    /// </summary>
    /// <param name="sliceData">The read-only span of bytes representing the data to compute the checksum for.</param>
    /// <returns>A 64-bit unsigned integer representing the computed checksum value for the provided data slice.</returns>
    private static ulong CalculateSliceDataChecksum(ReadOnlySpan<byte> sliceData)
    {
        const byte shift = 0x2F;
        const byte rotation0 = 0x12;
        const byte rotation1 = 0x14;
        const byte rotation2 = 0x15;
        const byte rotation3 = 0x16;
        const byte rotation4 = 0x19;
        const byte rotation5 = 0x1B;
        const byte rotation6 = 0x1E;
        const byte rotation7 = 0x1F;
        const ulong salt0 = 0xB492B66FBE98F273;
        const ulong salt1 = 0x9AE16A3B2F90404F;

        byte batchSizeInBytes;
        ulong multiplier0;
        ulong multiplier;
        ulong checksum;
        ReadOnlySpan<ulong> lastBatch;

        var sliceDataAsUlongs = MemoryMarshal.Cast<byte, ulong>(sliceData);
        var contentLength = sliceData.Length;

        switch (contentLength)
        {
            case > 64:
                batchSizeInBytes = 64;
                multiplier = 0x9DDFEA08EB382D69;

                // Create combos from the last batch
                lastBatch = MemoryMarshal.Cast<byte, ulong>(sliceData.Slice(contentLength - batchSizeInBytes, batchSizeInBytes));
                // create comboA
                var xor0 = (lastBatch[^6] + (ulong)contentLength) ^ lastBatch[^3];
                var xor = ((xor0 * multiplier) >> shift) ^ (xor0 * multiplier) ^ lastBatch[^3];
                var comboA = (((xor * multiplier) >> shift) ^ (xor * multiplier)) * multiplier;
                // create comboB
                var sum0 = (ulong)contentLength + lastBatch[^8];
                var sum = sum0 + lastBatch[^7] + lastBatch[^6];
                var rotationLeft = ulong.RotateLeft(sum, rotation1);
                sum = sum0 + lastBatch[^5] + comboA;
                var rotationRight = ulong.RotateRight(sum, rotation2);
                var comboB = sum0 + rotationLeft + rotationRight;
                // create comboC
                sum0 = salt0 + lastBatch[^7] + lastBatch[^4] + lastBatch[^2];
                sum = sum0 + lastBatch[^3] + lastBatch[^2];
                rotationLeft = ulong.RotateLeft(sum, rotation1);
                sum = sum0 + lastBatch[^5] + lastBatch[^1];
                rotationRight = ulong.RotateRight(sum, rotation2);
                var comboC = sum0 + rotationLeft + rotationRight;
                // create combos: D, E, F, G, H;
                var comboD = sum0 + lastBatch[^3] + lastBatch[^2] + lastBatch[^1];
                var comboE = (ulong)contentLength + lastBatch[^8] + lastBatch[^7] + lastBatch[^6] + lastBatch[^5];
                var comboF = salt0 * lastBatch[^5] + sliceDataAsUlongs[0];
                var comboG = lastBatch[^7] + lastBatch[^2];
                ulong comboH = 0;

                // Mix combos with data from the other batches
                const byte batchSizeInUlongs = 0x8;
                var laps = sliceDataAsUlongs.Length / batchSizeInUlongs;
                laps -= sliceDataAsUlongs.Length % batchSizeInUlongs == 0 ? 1 : 0;
                for (var i = 0; i < laps; i++)
                {
                    var position = i * batchSizeInUlongs;

                    // perform comboH
                    sum = comboE + comboF + comboG + sliceDataAsUlongs[position + 1];
                    rotationLeft = ulong.RotateLeft(sum, rotation5);
                    comboH = (salt0 * rotationLeft) ^ comboC;

                    // perform comboG
                    sum = comboB + comboG + sliceDataAsUlongs[position + 6];
                    rotationLeft = ulong.RotateLeft(sum, rotation3);
                    comboG = salt0 * rotationLeft + sliceDataAsUlongs[position + 5] + comboE;

                    // perform comboF
                    sum = comboA + comboD;
                    rotationLeft = ulong.RotateLeft(sum, rotation7);
                    comboF = salt0 * rotationLeft;

                    // perform comboE
                    var mul = salt0 * comboB;
                    sum = sliceDataAsUlongs[position + 2] + sliceDataAsUlongs[position + 1] + sliceDataAsUlongs[position];
                    comboE = mul + sliceDataAsUlongs[position + 3] + sum;

                    // perform comboB
                    rotationLeft = ulong.RotateLeft(mul + sum, rotation1);
                    rotationRight = ulong.RotateRight(mul + sliceDataAsUlongs[position + 3] + sliceDataAsUlongs[position] + comboD + comboH, rotation2);
                    comboB = mul + rotationLeft + rotationRight + sliceDataAsUlongs[position];

                    // perform comboD
                    comboC += sliceDataAsUlongs[position + 4];
                    sum0 = comboC + comboF;
                    comboD = sliceDataAsUlongs[position + 7] + sliceDataAsUlongs[position + 6] + sliceDataAsUlongs[position + 5] + sum0;

                    // perform comboC
                    sum = sliceDataAsUlongs[position + 6] + sliceDataAsUlongs[position + 5] + sum0;
                    rotationLeft = ulong.RotateLeft(sum, rotation1);
                    sum = sliceDataAsUlongs[position + 7] + sliceDataAsUlongs[position + 2] + sum0 + comboG;
                    rotationRight = ulong.RotateRight(sum, rotation2);
                    comboC = rotationLeft + rotationRight + sum0;

                    // assign comboH to comboA
                    comboA = comboH;
                }
                // Finalize mixin
                comboA = (comboB ^ comboC) * multiplier;
                comboB = ((comboA >> shift) ^ comboA ^ comboC) * multiplier;

                comboA = (comboD ^ comboE) * multiplier;
                comboC = ((comboA >> shift) ^ comboA ^ comboD) * multiplier;

                comboA = ((comboG >> shift) ^ comboG) * salt0 + comboH;
                comboD = ((comboB >> shift) ^ comboB) * multiplier + comboF;

                comboB = ((((comboC >> shift) ^ comboC) * multiplier + comboA) ^ comboD) * multiplier;
                comboA = ((comboB >> shift) ^ comboB ^ comboD) * multiplier;
                checksum = ((comboA >> shift) ^ comboA) * multiplier;
                break;
            case > 32:
                batchSizeInBytes = 32;
                multiplier0 = 9;
                multiplier = (ulong)contentLength * 2 + salt1;
                // Create combos from the last batch
                lastBatch = MemoryMarshal.Cast<byte, ulong>(sliceData.Slice(contentLength - batchSizeInBytes, batchSizeInBytes));
                // create combos: A, B, C, D, E;
                comboA = sliceDataAsUlongs[3] * multiplier0 + salt1 * sliceDataAsUlongs[2];
                comboB = ulong.RotateLeft(comboA, rotation3) + lastBatch[^3];
                comboC = comboA + lastBatch[^3];
                comboD = salt1 * sliceDataAsUlongs[0] + lastBatch[^1];
                comboE = sliceDataAsUlongs[3] * multiplier0 + (comboD ^ lastBatch[^4]) + 1;

                var byteSwap = BinaryPrimitives.ReverseEndianness(multiplier * (ulong.RotateLeft(comboD, rotation2) + (ulong.RotateRight(sliceDataAsUlongs[1], rotation6) + lastBatch[^3]) * multiplier0 + comboE));
                byteSwap = BinaryPrimitives.ReverseEndianness(multiplier * (byteSwap + comboE + multiplier * lastBatch[^2]));
                byteSwap = BinaryPrimitives.ReverseEndianness(multiplier * (byteSwap + comboB + comboC + lastBatch[^1]));
                byteSwap = multiplier * (byteSwap + comboC + sliceDataAsUlongs[1]) + multiplier * lastBatch[^2] + lastBatch[^4];
                checksum = multiplier * ((byteSwap >> shift) ^ byteSwap) + comboB;
                break;
            case > 16:
                batchSizeInBytes = 16;
                multiplier0 = 0xB492B66FBE98F273;
                multiplier = (ulong)contentLength * 2 + salt1;
                // Create combos from the last batch
                lastBatch = MemoryMarshal.Cast<byte, ulong>(sliceData.Slice(contentLength - batchSizeInBytes, batchSizeInBytes));
                // create combos: A, B, C;
                comboA = ulong.RotateRight(sliceDataAsUlongs[1] + salt1, rotation0) + multiplier * lastBatch[^1] + sliceDataAsUlongs[0] * multiplier0;
                comboB = multiplier * ((ulong.RotateLeft(sliceDataAsUlongs[1] + sliceDataAsUlongs[0] * multiplier0, rotation2) + ulong.RotateRight(multiplier * lastBatch[^1], rotation6) + salt1 * lastBatch[^2]) ^ comboA);
                comboC = multiplier * ((comboB >> shift) ^ comboA ^ comboB);
                checksum = ((comboC >> shift) ^ comboC) * multiplier;
                break;
            case > 8:
                batchSizeInBytes = 8;
                multiplier = (ulong)contentLength * 2 + salt1;
                // Create combos from the last batch
                lastBatch = MemoryMarshal.Cast<byte, ulong>(sliceData.Slice(contentLength - batchSizeInBytes, batchSizeInBytes));
                // create combos: A, B, C, D;
                comboA = sliceDataAsUlongs[0] - 0x651E95C4D06FBFB1;
                comboB = multiplier * (ulong.RotateRight(comboA, rotation4) + lastBatch[^1]);
                comboC = multiplier * ((multiplier * ulong.RotateLeft(lastBatch[^1], rotation5) + comboA) ^ comboB);
                comboD = multiplier * ((comboC >> shift) ^ comboC ^ comboB);
                checksum = ((comboD >> shift) ^ comboD) * multiplier;
                break;
            case > 4:
                batchSizeInBytes = 4;
                multiplier = (ulong)contentLength * 2 + salt1;
                var sliceDataAsUints = MemoryMarshal.Cast<byte, uint>(sliceData);
                // Create combos from the last batch
                var lastBatch0 = MemoryMarshal.Cast<byte, uint>(sliceData.Slice(contentLength - batchSizeInBytes, batchSizeInBytes));
                // create combos: A, B;
                comboA = multiplier * (((ulong)sliceDataAsUints[0] * 8 + (ulong)contentLength) ^ lastBatch0[^1]);
                comboB = multiplier * ((comboA >> shift) ^ comboA ^ lastBatch0[^1]);
                checksum = multiplier * ((comboB >> shift) ^ comboB);
                break;
            case > 0:
                multiplier0 = 0xC3A5C85C97CB3127;
                multiplier = 0xE16A3B2F90404F00;
                // create comboA
                comboA = (multiplier * sliceData[contentLength >> 1] + salt1 * sliceData[0]) ^ (multiplier0 * ((ulong)contentLength + (ulong)sliceData[contentLength - 1] * 4));
                checksum = ((comboA >> shift) ^ comboA) * salt1;
                break;
            default:
                checksum = salt1;
                break;
        }
        return checksum;
    }
    
    /// <summary>
    /// Splits the provided data encryption key into multiple parts and writes them into the specified header data buffer using the given personal key.
    /// </summary>
    /// <param name="headerData">A span of unsigned long integers that receives the split parts of the encryption key.</param>
    /// <param name="encryptionKey">A read-only span of unsigned long integers representing the data encryption key to be split and stored in the header data.</param>
    /// <param name="personalKey">A read-only span of unsigned long integers used as a personal key to influence the splitting process.</param>
    private static void SplitSliceDataEncryptionKey(Span<ulong> headerData, ReadOnlySpan<ulong> encryptionKey, ReadOnlySpan<ulong> personalKey)
    {
        // create localContainerA
        Span<ulong> localContainerA = stackalloc ulong[ContainerCapacityInUlongs];
        SetupContainer(localContainerA, personalKey);
        // create localContainerB
        Span<ulong> localContainerB = stackalloc ulong[ContainerCapacityInUlongs];
        SetupContainer(localContainerB, KeyType);
        // create localContainerC
        Span<ulong> localContainerC = stackalloc ulong[ContainerCapacityInUlongs];
        SetupContainer(localContainerC, PrivateKey1);
        // calculate personal seed
        Limegator(localContainerA, localContainerB, localContainerC);
        // split encryption key and put its parts in the headerData
        for (var i = 0; i < encryptionKey.Length; i++)
        {
            var position = i * 2 * HeaderDataKeySizeInUlongs;
            SetupContainer(headerData.Slice(position, HeaderDataKeySizeInUlongs), HeaderKey);
            // prepare localContainerB
            SetupContainer(localContainerB, encryptionKey[i]);
            // prepare localContainerC
            localContainerA.CopyTo(localContainerC);
            // calculate key and add it to the list
            EncryptionFirst(localContainerC, localContainerB);
            localContainerC[..HeaderDataKeySizeInUlongs].CopyTo(headerData.Slice(position + HeaderDataKeySizeInUlongs, HeaderDataKeySizeInUlongs));
        }
    }

    /// <summary>
    /// Merges header data and user-specific information into the provided encryption key buffer using a deterministic algorithm.
    /// </summary>
    /// <param name="encryptionKey">A span of unsigned 64-bit integers that will be populated with the resulting merged encryption key data.</param>
    /// <param name="headerData">A read-only span of unsigned 64-bit integers containing the header data to be merged into the encryption key.</param>
    /// <param name="userId">The user identifier used to personalize the encryption key merging process.</param>
    private static void MergeSliceDataEncryptionKey(Span<ulong> encryptionKey, ReadOnlySpan<ulong> headerData, ulong userId)
    {
        // create localContainerA
        Span<ulong> localContainerA = stackalloc ulong[ContainerCapacityInUlongs];
        localContainerA.Clear();

        // create localContainerB
        Span<ulong> localContainerB = stackalloc ulong[ContainerCapacityInUlongs];
        SetupContainer(localContainerB, userId);

        // create localContainerC
        Span<ulong> localContainerC = stackalloc ulong[ContainerCapacityInUlongs];
        SetupContainer(localContainerC, PrivateKey1);

        // create localContainerD
        Span<ulong> localContainerD = stackalloc ulong[ContainerCapacityInUlongs];
        localContainerD.Clear();

        // calculate checksum parts
        for (var i = 0; i < encryptionKey.Length; i++)
        {
            var position = i * 2 * HeaderDataKeySizeInUlongs;
            headerData.Slice(position, HeaderDataKeySizeInUlongs).CopyTo(localContainerA);
            Limegator(localContainerA, localContainerB, localContainerC);
            headerData.Slice(position + HeaderDataKeySizeInUlongs, HeaderDataKeySizeInUlongs).CopyTo(localContainerD);
            Limeghetti(localContainerD, localContainerA);
            encryptionKey[i] = localContainerD[0];
        }
    }

    /// <summary>
    /// Deencrypts the specified data slice header in place using the provided state value.
    /// </summary>
    /// <param name="state">A reference to the state value used for decryption. The state is updated during the operation.</param>
    /// <param name="data">The span of bytes representing the data slice header to be deencrypted. The data is modified in place.</param>
    private static void DeencryptSliceHeader(ref ulong state, Span<byte> data)
    {
        // decode the data
        for (var i = 0; i < data.Length; i++)
        {
            Splitmix64(ref state);
            data[i] ^= (byte)state;
        }
    }

    /// <summary>
    /// Calculates and writes a header checksum into the specified buffer using the provided state value.
    /// </summary>
    /// <param name="state">A reference to the current state value used for checksum generation. The value is updated during the calculation.</param>
    /// <param name="checksum">A buffer that receives the generated header checksum.</param>
    private static void CalculateHeaderChecksum(ref ulong state, Span<byte> checksum)
    {
        const byte rows = 0x2;
        var slotsInRow = checksum.Length / rows;
        for (var i = 0; i < slotsInRow; i++)
        {
            Splitmix64(ref state);
            checksum[i] = (byte)state;
            checksum[slotsInRow + i] = (byte)(state >> 8);
        }
    }

    /// <summary>
    /// Updates the header encryption key state by adding the specified value.
    /// </summary>
    /// <param name="state">A reference to the current header encryption key state to be updated.</param>
    /// <param name="notUserId">The value to add to the header encryption key state.</param>
    private static void CalculateHeaderEncryptionKeyState(ref ulong state, ulong notUserId)
        => state += notUserId;

    /// <summary>
    /// Decrypts the specified encrypted data into the provided buffer using the given user identifier.
    /// </summary>
    /// <param name="decryptedData">A writable span of bytes that receives the decrypted output. The length must match the expected decrypted data size.</param>
    /// <param name="encryptedData">A span of bytes containing the encrypted input data to be decrypted.</param>
    /// <param name="userId">The user identifier used to derive decryption keys and validate data integrity.</param>
    /// <exception cref="InvalidDataException">Thrown if the checksum validation of the decrypted data segment fails, indicating possible corruption or tampering.</exception>
    public void DecryptData(Span<byte> decryptedData, Span<byte> encryptedData, ulong userId)
    {
        // Calculate Slices Queue
        var state = MandarinSeed;
        var slicesQueue = CalculateSlicesQueue(ref state, (uint)decryptedData.Length);
        // Calculate the state of first header
        CalculateHeaderEncryptionKeyState(ref state, userId);

        Span<byte> headerChecksumA = stackalloc byte[ChecksumContainerCapacityInBytes];
        Span<byte> headerChecksumB = stackalloc byte[ChecksumContainerCapacityInBytes];
        var headerChecksumBSpanAsUlongs = MemoryMarshal.Cast<byte, ulong>(headerChecksumB);
        var remainingBytes = decryptedData.Length;
        var positionD = 0;
        var positionE = 0;
        while (remainingBytes > 0)
        {
            // Get length of current slice data
            var currentSliceDataLength = slicesQueue.DequeueShifted();
            // Get length of current slice data with its header
            var currentSliceLength = currentSliceDataLength + HeaderDataSizeInBytes;
            // Get current slice span
            var currentSlice = encryptedData.Slice(positionD, currentSliceLength);

            // Calculate encryption key for the header of the current slice
            CalculateHeaderChecksum(ref state, headerChecksumA);

            // Decrypt current slice header
            var currentSliceHeader = currentSlice[..HeaderDataSizeInBytes];
            DeencryptSliceHeader(ref state, currentSliceHeader);

            // Calculate the checksum of the current header data
            var currentSliceHeaderDataAsUlongs = MemoryMarshal.Cast<byte, ulong>(currentSliceHeader[..^HeaderFooterSizeInBytes]);
            MergeSliceDataEncryptionKey(headerChecksumBSpanAsUlongs, currentSliceHeaderDataAsUlongs, userId);

            // Decrypt current slice data
            var currentSliceData = currentSlice[HeaderDataSizeInBytes..];
            DeencryptData(currentSliceData, headerChecksumB);

            // Validate segment checksum for the first slice only
            if (currentSliceDataLength > remainingBytes) currentSliceDataLength = remainingBytes;
            if (positionD == 0)
            {
                // Calculate the checksum of the current slice data
                var sliceChecksum = CalculateSliceDataChecksum(currentSliceData[..currentSliceDataLength]);
                // Validate slice checksum
                var currentSliceChecksum = MemoryMarshal.Cast<byte, ulong>(currentSliceHeader)[^2];
                if (sliceChecksum != currentSliceChecksum) 
                    throw new InvalidDataException("Mandarin Data Segment checksum validation failed.");
            }

            // Collect slice data
            currentSlice.Slice(HeaderDataSizeInBytes, currentSliceDataLength).CopyTo(decryptedData.Slice(positionE, currentSliceDataLength));

            // Update positions
            positionD += currentSliceLength;
            positionE += currentSliceDataLength;
            remainingBytes -= currentSliceDataLength;
        }
    }

    /// <summary>
    /// Encrypts the specified decrypted data using a user-specific key and outputs the resulting encrypted data.
    /// </summary>
    /// <param name="decryptedData">A read-only span of bytes containing the data to be encrypted.</param>
    /// <param name="userId">The unique identifier of the user whose key is used for encryption.</param>
    /// <returns>A byte array containing the encrypted representation of the input data, including necessary headers for decryption.</returns>
    public byte[] EncryptData(ReadOnlySpan<byte> decryptedData, ulong userId)
    {
        // Calculate Slices Queue
        var state = MandarinSeed;
        var slicesQueue = CalculateSlicesQueue(ref state, (uint)decryptedData.Length);
        // Create a span for the encrypted data
        var encryptedData = new byte[slicesQueue.TotalLengthShifted + slicesQueue.Count * HeaderDataSizeInBytes];
        var encryptedDataSpan = encryptedData.AsSpan();
        // Calculate the state of first header
        CalculateHeaderEncryptionKeyState(ref state, userId);
        // Calculate Personal Key
        var personalKey = CreateKey(userId);

        Span<byte> headerChecksum = stackalloc byte[ChecksumContainerCapacityInBytes];
        var remainingBytes = decryptedData.Length;
        var positionE = 0;
        var positionD = 0;
        while (remainingBytes > 0)
        {
            // Get length of current slice data
            var currentSliceDataLength = slicesQueue.DequeueShifted();
            // Get length of current slice data with its header
            var currentSliceLength = currentSliceDataLength + HeaderDataSizeInBytes;
            // Get current slice span
            var currentSlice = encryptedDataSpan.Slice(positionE, currentSliceLength);

            // Cut the header of the current slice
            var currentSliceHeader = currentSlice[..HeaderDataSizeInBytes];
            var currentSliceHeaderAsUlongs = MemoryMarshal.Cast<byte, ulong>(currentSliceHeader);

            // Calculate encryption key for the header of the current slice
            CalculateHeaderChecksum(ref state, headerChecksum);

            // Split slice data encryption key and put its parts in the header
            var headerChecksumAsUlongs = MemoryMarshal.Cast<byte, ulong>(headerChecksum);
            SplitSliceDataEncryptionKey(currentSliceHeaderAsUlongs, headerChecksumAsUlongs, personalKey);

            // Copy decrypted data into the encrypted data span
            var currentSliceData = currentSlice[HeaderDataSizeInBytes..];
            currentSliceDataLength = currentSliceData.Length > remainingBytes ? remainingBytes : currentSliceData.Length;
            decryptedData.Slice(positionD, currentSliceDataLength).CopyTo(currentSliceData);
            
            // Calculate checksum of the current slice data
            var sliceChecksum = CalculateSliceDataChecksum(currentSliceData[..currentSliceDataLength]);
            currentSliceHeaderAsUlongs[^2] = sliceChecksum;
            currentSliceHeaderAsUlongs[^1] = (ulong)remainingBytes;
            Splitmixer64(ref currentSliceHeaderAsUlongs[^1]);
            
            // Encrypt current slice header
            DeencryptSliceHeader(ref state, currentSliceHeader);

            // Encrypt current slice data
            DeencryptData(currentSliceData, headerChecksum);

            // Update positions
            positionE += currentSliceLength;
            positionD += currentSliceDataLength;
            remainingBytes -= currentSliceDataLength;
        }
        return encryptedData;
    }

    #endregion
}