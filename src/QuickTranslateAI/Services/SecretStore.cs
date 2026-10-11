using System;
using System.Runtime.InteropServices;
using System.Text;
using QuickTranslateAI.Models;

namespace QuickTranslateAI.Services;

/// <summary>
/// API anahtarlarını Windows Credential Manager'da (Genel kimlik bilgisi) saklar.
/// Anahtar ne ayar dosyasına ne de kaynak koda yazılır.
/// </summary>
public static class SecretStore
{
    private const int CRED_TYPE_GENERIC = 1;
    private const int CRED_PERSIST_LOCAL_MACHINE = 2;

    public static string TargetFor(ProviderKind provider) => provider switch
    {
        ProviderKind.Claude => "QuickTranslateAI:Anthropic",
        ProviderKind.OpenAi => "QuickTranslateAI:OpenAI",
        ProviderKind.Custom => "QuickTranslateAI:Custom",
        _ => "QuickTranslateAI:Unknown"
    };

    public static void Save(ProviderKind provider, string apiKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            Delete(provider);
            return;
        }

        var blob = Encoding.Unicode.GetBytes(apiKey.Trim());
        var blobHandle = Marshal.AllocHGlobal(blob.Length);
        try
        {
            Marshal.Copy(blob, 0, blobHandle, blob.Length);

            var credential = new CREDENTIAL
            {
                Type = CRED_TYPE_GENERIC,
                TargetName = TargetFor(provider),
                CredentialBlobSize = blob.Length,
                CredentialBlob = blobHandle,
                Persist = CRED_PERSIST_LOCAL_MACHINE,
                UserName = Environment.UserName,
                Comment = "QuickTranslate AI çeviri sağlayıcısı API anahtarı"
            };

            if (!CredWrite(ref credential, 0))
            {
                throw new InvalidOperationException(
                    $"API anahtarı Credential Manager'a yazılamadı (hata {Marshal.GetLastWin32Error()}).");
            }
        }
        finally
        {
            Marshal.FreeHGlobal(blobHandle);
        }
    }

    public static string? Read(ProviderKind provider)
    {
        if (!CredRead(TargetFor(provider), CRED_TYPE_GENERIC, 0, out var handle))
        {
            return null;
        }

        try
        {
            var credential = Marshal.PtrToStructure<CREDENTIAL>(handle);
            if (credential.CredentialBlobSize == 0 || credential.CredentialBlob == IntPtr.Zero)
            {
                return null;
            }

            return Marshal.PtrToStringUni(credential.CredentialBlob, credential.CredentialBlobSize / 2);
        }
        finally
        {
            CredFree(handle);
        }
    }

    public static bool Has(ProviderKind provider) => !string.IsNullOrWhiteSpace(Read(provider));

    public static void Delete(ProviderKind provider) => CredDelete(TargetFor(provider), CRED_TYPE_GENERIC, 0);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct CREDENTIAL
    {
        public int Flags;
        public int Type;
        [MarshalAs(UnmanagedType.LPWStr)] public string TargetName;
        [MarshalAs(UnmanagedType.LPWStr)] public string? Comment;
        public long LastWritten;
        public int CredentialBlobSize;
        public IntPtr CredentialBlob;
        public int Persist;
        public int AttributeCount;
        public IntPtr Attributes;
        [MarshalAs(UnmanagedType.LPWStr)] public string? TargetAlias;
        [MarshalAs(UnmanagedType.LPWStr)] public string? UserName;
    }

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true, EntryPoint = "CredWriteW")]
    private static extern bool CredWrite(ref CREDENTIAL credential, int flags);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true, EntryPoint = "CredReadW")]
    private static extern bool CredRead(string target, int type, int reservedFlag, out IntPtr credentialPtr);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true, EntryPoint = "CredDeleteW")]
    private static extern bool CredDelete(string target, int type, int flags);

    [DllImport("advapi32.dll")]
    private static extern void CredFree(IntPtr buffer);
}
