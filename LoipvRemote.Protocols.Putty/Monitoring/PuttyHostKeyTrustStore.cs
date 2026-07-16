using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Text;
using Renci.SshNet;

namespace LoipvRemote.Protocols.Putty.Monitoring;

/// <summary>Registry-independent PuTTY host-key format and trust policy.</summary>
public sealed class PuttyHostKeyTrustStore(IPuttyHostKeyRegistry registry) : IPuttyHostKeyTrustStore
{
    private const string PuttyHostKeyRegistryPath = @"Software\SimonTatham\PuTTY\SshHostKeys";
    private static readonly BigInteger Ed25519Prime = (BigInteger.One << 255) - 19;
    private static readonly BigInteger Ed25519SqrtMinusOne = BigInteger.ModPow(2, (Ed25519Prime - 1) / 4, Ed25519Prime);
    private static readonly BigInteger Ed25519D = Mod(
        -new BigInteger(121665) * ModInverse(new BigInteger(121666), Ed25519Prime),
        Ed25519Prime);

    private readonly IPuttyHostKeyRegistry _registry = registry ?? throw new ArgumentNullException(nameof(registry));

    public bool IsTrusted(string hostname, int port, string hostKeyName, byte[] hostKey)
    {
        string? cacheKeyType;
        string? presentedKey;
        if (string.Equals(hostKeyName, "ssh-ed25519", StringComparison.Ordinal))
        {
            cacheKeyType = hostKeyName;
            presentedKey = BuildEd25519CacheValue(hostKeyName, hostKey);
        }
        else if (hostKeyName.StartsWith("rsa-sha2-", StringComparison.Ordinal) ||
                 string.Equals(hostKeyName, "ssh-rsa", StringComparison.Ordinal))
        {
            cacheKeyType = "rsa2";
            presentedKey = BuildRsaCacheValue(hostKey);
        }
        else
        {
            return false;
        }

        string? cachedKey = ReadCachedHostKey(hostname, port, cacheKeyType);
        return cachedKey is not null && presentedKey is not null &&
               string.Equals(cachedKey, presentedKey, StringComparison.OrdinalIgnoreCase);
    }

    public void PreferCachedHostKeyAlgorithms(ConnectionInfo connectionInfo, string hostname, int port)
    {
        ArgumentNullException.ThrowIfNull(connectionInfo);
        bool hasRsaKey = ReadCachedHostKey(hostname, port, "rsa2") is not null;
        bool hasEd25519Key = ReadCachedHostKey(hostname, port, "ssh-ed25519") is not null;
        if (!hasRsaKey || hasEd25519Key) return;

        foreach (string algorithm in connectionInfo.HostKeyAlgorithms.Keys.ToArray())
        {
            if (!algorithm.StartsWith("rsa-sha2-", StringComparison.Ordinal) &&
                !string.Equals(algorithm, "ssh-rsa", StringComparison.Ordinal))
                connectionInfo.HostKeyAlgorithms.Remove(algorithm);
        }
    }

    public static string? BuildEd25519CacheValue(string hostKeyName, byte[] hostKey)
    {
        if (!string.Equals(hostKeyName, "ssh-ed25519", StringComparison.Ordinal) ||
            !TryReadSshString(hostKey, 0, out byte[] algorithmBytes, out int publicKeyOffset) ||
            !string.Equals(Encoding.ASCII.GetString(algorithmBytes), hostKeyName, StringComparison.Ordinal) ||
            !TryReadSshString(hostKey, publicKeyOffset, out byte[] encodedPoint, out int finalOffset) ||
            finalOffset != hostKey.Length || encodedPoint.Length != 32)
            return null;

        byte[] yBytes = (byte[])encodedPoint.Clone();
        bool xIsOdd = (yBytes[^1] & 0x80) != 0;
        yBytes[^1] &= 0x7f;
        BigInteger y = new(yBytes, isUnsigned: true, isBigEndian: false);
        if (y >= Ed25519Prime) return null;

        BigInteger ySquared = Mod(y * y, Ed25519Prime);
        BigInteger xSquared = Mod((ySquared - 1) * ModInverse(Mod(Ed25519D * ySquared + 1, Ed25519Prime), Ed25519Prime), Ed25519Prime);
        BigInteger x = BigInteger.ModPow(xSquared, (Ed25519Prime + 3) / 8, Ed25519Prime);
        if (Mod(x * x, Ed25519Prime) != xSquared)
            x = Mod(x * Ed25519SqrtMinusOne, Ed25519Prime);
        if (Mod(x * x, Ed25519Prime) != xSquared || (x.IsEven == xIsOdd))
            x = Mod(Ed25519Prime - x, Ed25519Prime);
        if (Mod(x * x, Ed25519Prime) != xSquared || (x.IsEven == xIsOdd)) return null;

        return $"0x{FormatUnsignedHex(x)},0x{FormatUnsignedHex(y)}";
    }

    public static string? BuildRsaCacheValue(byte[] hostKey)
    {
        if (!TryReadSshString(hostKey, 0, out byte[] algorithmBytes, out int exponentOffset) ||
            !string.Equals(Encoding.ASCII.GetString(algorithmBytes), "ssh-rsa", StringComparison.Ordinal) ||
            !TryReadSshString(hostKey, exponentOffset, out byte[] exponentBytes, out int modulusOffset) ||
            !TryReadSshString(hostKey, modulusOffset, out byte[] modulusBytes, out int finalOffset) ||
            finalOffset != hostKey.Length || exponentBytes.Length == 0 || modulusBytes.Length == 0)
            return null;

        BigInteger exponent = new(TrimMpintPadding(exponentBytes), isUnsigned: true, isBigEndian: true);
        BigInteger modulus = new(TrimMpintPadding(modulusBytes), isUnsigned: true, isBigEndian: true);
        if (exponent <= 1 || modulus <= 1) return null;

        return $"0x{FormatUnsignedHex(exponent)},0x{FormatUnsignedHex(modulus)}";
    }

    private string? ReadCachedHostKey(string hostname, int port, string keyType) =>
        _registry.GetCurrentUserString(PuttyHostKeyRegistryPath, $"{keyType}@{port}:{EscapeRegistryKey(hostname)}");

    private static string FormatUnsignedHex(BigInteger value) =>
        value.ToString("x", CultureInfo.InvariantCulture).TrimStart('0') is { Length: > 0 } hex ? hex : "0";

    private static byte[] TrimMpintPadding(byte[] value)
    {
        int offset = 0;
        while (offset < value.Length - 1 && value[offset] == 0) offset++;
        if (offset == 0) return value;
        byte[] trimmed = new byte[value.Length - offset];
        Buffer.BlockCopy(value, offset, trimmed, 0, trimmed.Length);
        return trimmed;
    }

    private static string EscapeRegistryKey(string value)
    {
        StringBuilder escaped = new(value.Length);
        bool canWriteDot = false;
        foreach (char character in value)
        {
            if (character == ' ' || character == '\\' || character == '*' || character == '?' ||
                character == '%' || character < ' ' || character > '~' || (character == '.' && !canWriteDot))
                escaped.Append('%').Append(((int)character).ToString("X2", CultureInfo.InvariantCulture));
            else
                escaped.Append(character);
            canWriteDot = true;
        }
        return escaped.ToString();
    }

    private static bool TryReadSshString(byte[] data, int offset, out byte[] value, out int nextOffset)
    {
        value = [];
        nextOffset = offset;
        if (offset < 0 || data.Length - offset < 4) return false;
        int length = (data[offset] << 24) | (data[offset + 1] << 16) | (data[offset + 2] << 8) | data[offset + 3];
        if (length < 0 || data.Length - offset - 4 < length) return false;
        value = new byte[length];
        Buffer.BlockCopy(data, offset + 4, value, 0, length);
        nextOffset = offset + 4 + length;
        return true;
    }

    private static BigInteger Mod(BigInteger value, BigInteger modulus)
    {
        BigInteger result = value % modulus;
        return result.Sign < 0 ? result + modulus : result;
    }

    private static BigInteger ModInverse(BigInteger value, BigInteger modulus) => BigInteger.ModPow(value, modulus - 2, modulus);
}

public interface IPuttyHostKeyRegistry
{
    string? GetCurrentUserString(string subKeyPath, string valueName);
}
