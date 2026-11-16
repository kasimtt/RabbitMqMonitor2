using System.Security.Cryptography;
using System.Text;

namespace RabbitMqMonitor.Services;

/// <summary>
/// Encrypts and decrypts credentials using Windows DPAPI (ProtectedData)
/// scoped to DataProtectionScope.CurrentUser.
/// </summary>
public sealed class CredentialEncryptionService
{
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("RabbitMqMonitor.DPAPI.v1.Salt");

    public string Encrypt(string plainText)
    {
        if (string.IsNullOrEmpty(plainText))
            return string.Empty;

        byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
        byte[] encryptedBytes = ProtectedData.Protect(
            plainBytes,
            Entropy,
            DataProtectionScope.CurrentUser);

        return Convert.ToBase64String(encryptedBytes);
    }

    public string Decrypt(string encryptedBase64)
    {
        if (string.IsNullOrWhiteSpace(encryptedBase64))
            return string.Empty;

        try
        {
            byte[] encryptedBytes = Convert.FromBase64String(encryptedBase64);
            byte[] plainBytes = ProtectedData.Unprotect(
                encryptedBytes,
                Entropy,
                DataProtectionScope.CurrentUser);

            return Encoding.UTF8.GetString(plainBytes);
        }
        catch (Exception)
        {
            // If moved across user accounts or corrupted, fail gracefully
            return string.Empty;
        }
    }
}

