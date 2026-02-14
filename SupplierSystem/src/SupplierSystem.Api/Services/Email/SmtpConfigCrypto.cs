using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace SupplierSystem.Api.Services.Email;

public static class SmtpConfigCrypto
{
    private const string EncryptionKeyEnv = "CONFIG_ENCRYPTION_KEY";
    private const string DefaultEncryptionKey = "default-32-char-encryption-key!!";
    private const int ScryptCost = 16384;
    private const int ScryptBlockSize = 8;
    private const int ScryptParallel = 1;
    private const int ScryptKeyLength = 32;
    private static readonly Lazy<byte[]> DerivedKey = new(CreateDerivedKey);

    public static string? Encrypt(string? plainText)
    {
        if (string.IsNullOrWhiteSpace(plainText))
        {
            return null;
        }

        try
        {
            var iv = RandomNumberGenerator.GetBytes(16);
            using var aes = Aes.Create();
            aes.KeySize = 256;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;
            aes.Key = DerivedKey.Value;
            aes.IV = iv;

            using var encryptor = aes.CreateEncryptor();
            var plainBytes = Encoding.UTF8.GetBytes(plainText);
            var cipherBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);
            return $"{ToHex(iv)}:{ToHex(cipherBytes)}";
        }
        catch
        {
            return null;
        }
    }

    public static string? Decrypt(string? cipherText)
    {
        if (string.IsNullOrWhiteSpace(cipherText))
        {
            return null;
        }

        if (!cipherText.Contains(':'))
        {
            return cipherText;
        }

        var parts = cipherText.Split(':', 2, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2)
        {
            return null;
        }

        try
        {
            var iv = FromHex(parts[0]);
            var cipherBytes = FromHex(parts[1]);

            using var aes = Aes.Create();
            aes.KeySize = 256;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;
            aes.Key = DerivedKey.Value;
            aes.IV = iv;

            using var decryptor = aes.CreateDecryptor();
            var plainBytes = decryptor.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);
            return Encoding.UTF8.GetString(plainBytes);
        }
        catch
        {
            return null;
        }
    }

    // Match Node's crypto.scryptSync(CONFIG_ENCRYPTION_KEY, "salt", 32).
    private static byte[] CreateDerivedKey()
    {
        var rawKey = Environment.GetEnvironmentVariable(EncryptionKeyEnv);
        if (string.IsNullOrWhiteSpace(rawKey))
        {
            rawKey = DefaultEncryptionKey;
        }

        return Scrypt(
            Encoding.UTF8.GetBytes(rawKey),
            Encoding.UTF8.GetBytes("salt"),
            ScryptCost,
            ScryptBlockSize,
            ScryptParallel,
            ScryptKeyLength);
    }

    private static byte[] Scrypt(byte[] password, byte[] salt, int cost, int blockSize, int parallel, int derivedKeyLength)
    {
        if (cost <= 1 || (cost & (cost - 1)) != 0)
        {
            throw new ArgumentException("Cost must be a power of two.", nameof(cost));
        }

        var blockLength = 128 * blockSize;
        var buffer = Pbkdf2(password, salt, parallel * blockLength);

        for (var i = 0; i < parallel; i++)
        {
            SMix(buffer, i * blockLength, blockSize, cost);
        }

        return Pbkdf2(password, buffer, derivedKeyLength);
    }

    private static void SMix(byte[] buffer, int offset, int blockSize, int cost)
    {
        var blockLength = 128 * blockSize;
        var x = new byte[blockLength];
        Buffer.BlockCopy(buffer, offset, x, 0, blockLength);

        var v = new byte[cost * blockLength];
        var y = new byte[blockLength];
        var scratch = new byte[64];

        for (var i = 0; i < cost; i++)
        {
            Buffer.BlockCopy(x, 0, v, i * blockLength, blockLength);
            BlockMix(x, blockSize, y, scratch);
            Swap(ref x, ref y);
        }

        for (var i = 0; i < cost; i++)
        {
            var j = (int)(Integerify(x, blockSize) & (uint)(cost - 1));
            Xor(x, v, j * blockLength, blockLength);
            BlockMix(x, blockSize, y, scratch);
            Swap(ref x, ref y);
        }

        Buffer.BlockCopy(x, 0, buffer, offset, blockLength);
    }

    private static void BlockMix(byte[] buffer, int blockSize, byte[] output, byte[] scratch)
    {
        const int blockLength = 64;
        Buffer.BlockCopy(buffer, (2 * blockSize - 1) * blockLength, scratch, 0, blockLength);

        for (var i = 0; i < 2 * blockSize; i++)
        {
            Xor(scratch, buffer, i * blockLength, blockLength);
            Salsa208(scratch);
            Buffer.BlockCopy(scratch, 0, output, i * blockLength, blockLength);
        }

        var outIndex = 0;
        for (var i = 0; i < blockSize; i++)
        {
            Buffer.BlockCopy(output, i * 2 * blockLength, buffer, outIndex, blockLength);
            outIndex += blockLength;
        }

        for (var i = 0; i < blockSize; i++)
        {
            Buffer.BlockCopy(output, (i * 2 + 1) * blockLength, buffer, outIndex, blockLength);
            outIndex += blockLength;
        }
    }

    private static void Salsa208(byte[] block)
    {
        Span<uint> x = stackalloc uint[16];
        for (var i = 0; i < 16; i++)
        {
            x[i] = BinaryPrimitives.ReadUInt32LittleEndian(block.AsSpan(i * 4, 4));
        }

        Span<uint> z = stackalloc uint[16];
        x.CopyTo(z);

        for (var i = 0; i < 8; i += 2)
        {
            x[4] ^= RotateLeft(x[0] + x[12], 7);
            x[8] ^= RotateLeft(x[4] + x[0], 9);
            x[12] ^= RotateLeft(x[8] + x[4], 13);
            x[0] ^= RotateLeft(x[12] + x[8], 18);

            x[9] ^= RotateLeft(x[5] + x[1], 7);
            x[13] ^= RotateLeft(x[9] + x[5], 9);
            x[1] ^= RotateLeft(x[13] + x[9], 13);
            x[5] ^= RotateLeft(x[1] + x[13], 18);

            x[14] ^= RotateLeft(x[10] + x[6], 7);
            x[2] ^= RotateLeft(x[14] + x[10], 9);
            x[6] ^= RotateLeft(x[2] + x[14], 13);
            x[10] ^= RotateLeft(x[6] + x[2], 18);

            x[3] ^= RotateLeft(x[15] + x[11], 7);
            x[7] ^= RotateLeft(x[3] + x[15], 9);
            x[11] ^= RotateLeft(x[7] + x[3], 13);
            x[15] ^= RotateLeft(x[11] + x[7], 18);

            x[1] ^= RotateLeft(x[0] + x[3], 7);
            x[2] ^= RotateLeft(x[1] + x[0], 9);
            x[3] ^= RotateLeft(x[2] + x[1], 13);
            x[0] ^= RotateLeft(x[3] + x[2], 18);

            x[6] ^= RotateLeft(x[5] + x[4], 7);
            x[7] ^= RotateLeft(x[6] + x[5], 9);
            x[4] ^= RotateLeft(x[7] + x[6], 13);
            x[5] ^= RotateLeft(x[4] + x[7], 18);

            x[11] ^= RotateLeft(x[10] + x[9], 7);
            x[8] ^= RotateLeft(x[11] + x[10], 9);
            x[9] ^= RotateLeft(x[8] + x[11], 13);
            x[10] ^= RotateLeft(x[9] + x[8], 18);

            x[12] ^= RotateLeft(x[15] + x[14], 7);
            x[13] ^= RotateLeft(x[12] + x[15], 9);
            x[14] ^= RotateLeft(x[13] + x[12], 13);
            x[15] ^= RotateLeft(x[14] + x[13], 18);
        }

        for (var i = 0; i < 16; i++)
        {
            x[i] += z[i];
        }

        for (var i = 0; i < 16; i++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(block.AsSpan(i * 4, 4), x[i]);
        }
    }

    private static uint RotateLeft(uint value, int shift)
    {
        return (value << shift) | (value >> (32 - shift));
    }

    private static ulong Integerify(byte[] buffer, int blockSize)
    {
        var index = (2 * blockSize - 1) * 64;
        return BinaryPrimitives.ReadUInt64LittleEndian(buffer.AsSpan(index, 8));
    }

    private static void Xor(byte[] target, byte[] source, int sourceOffset, int count)
    {
        for (var i = 0; i < count; i++)
        {
            target[i] ^= source[sourceOffset + i];
        }
    }

    private static void Swap(ref byte[] first, ref byte[] second)
    {
        var temp = first;
        first = second;
        second = temp;
    }

    private static byte[] Pbkdf2(byte[] password, byte[] salt, int length)
    {
        using var pbkdf2 = new Rfc2898DeriveBytes(password, salt, 1, HashAlgorithmName.SHA256);
        return pbkdf2.GetBytes(length);
    }

    private static string ToHex(byte[] bytes)
    {
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static byte[] FromHex(string hex)
    {
        return Convert.FromHexString(hex);
    }
}
