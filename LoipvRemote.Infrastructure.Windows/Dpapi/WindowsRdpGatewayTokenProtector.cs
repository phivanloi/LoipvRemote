using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;

namespace LoipvRemote.Infrastructure.Windows.Dpapi;

/// <summary>Protects the RDP gateway authentication cookie with the Windows DPAPI API expected by MSTSC.</summary>
[SupportedOSPlatform("windows")]
public static class WindowsRdpGatewayTokenProtector
{
    public static string EncryptAuthCookieString(string cookie)
    {
        ArgumentNullException.ThrowIfNull(cookie);
        byte[]? protectedBytes = Protect(Encoding.Unicode.GetBytes(cookie + '\0'));
        return protectedBytes is null
            ? throw new InvalidOperationException("Windows could not protect the RDP gateway authentication cookie.")
            : Convert.ToBase64String(protectedBytes);
    }

    private static byte[]? Protect(byte[] input)
    {
        IntPtr inputPointer = Marshal.AllocHGlobal(input.Length);
        try
        {
            Marshal.Copy(input, 0, inputPointer, input.Length);
            var inputBlob = new DataBlob { Size = input.Length, Data = inputPointer };
            if (!CryptProtectData(ref inputBlob, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, CryptProtectUiForbidden, out DataBlob outputBlob))
                return null;

            try
            {
                var output = new byte[outputBlob.Size];
                Marshal.Copy(outputBlob.Data, output, 0, outputBlob.Size);
                return output;
            }
            finally
            {
                if (outputBlob.Data != IntPtr.Zero)
                    Marshal.FreeHGlobal(outputBlob.Data);
            }
        }
        finally { Marshal.FreeHGlobal(inputPointer); }
    }

    private const int CryptProtectUiForbidden = 0x00000001;
    [StructLayout(LayoutKind.Sequential)] private struct DataBlob { public int Size; public IntPtr Data; }
    [DllImport("crypt32.dll", CharSet = CharSet.Unicode)]
    private static extern bool CryptProtectData(ref DataBlob input, IntPtr description, IntPtr entropy, IntPtr reserved, IntPtr prompt, int flags, out DataBlob output);
}
