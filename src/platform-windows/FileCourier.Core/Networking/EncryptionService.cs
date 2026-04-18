namespace FileCourier.Core.Networking;

/// <summary>
/// Interface for the encryption layer. When IsEncrypted=false a no-op implementation is used.
/// Full implementation performs ECDH key exchange + AES-256-GCM per architecture.md.
/// </summary>
public interface IEncryptionService
{
    /// <summary>Perform the key exchange over an already-connected stream (called before sending/receiving headers).</summary>
    Task NegotiateAsSenderAsync(Stream stream, CancellationToken ct = default);
    Task NegotiateAsReceiverAsync(Stream stream, CancellationToken ct = default);

    byte[] Encrypt(byte[] plaintext);
    byte[] Decrypt(byte[] ciphertext);
}

/// <summary>
/// No-op encryption service used when IsEncrypted=false (Phase 1 default).
/// </summary>
public sealed class NoOpEncryptionService : IEncryptionService
{
    public Task NegotiateAsSenderAsync(Stream stream, CancellationToken ct = default) => Task.CompletedTask;
    public Task NegotiateAsReceiverAsync(Stream stream, CancellationToken ct = default) => Task.CompletedTask;
    public byte[] Encrypt(byte[] plaintext) => plaintext;
    public byte[] Decrypt(byte[] ciphertext) => ciphertext;
}

// TODO Phase 2: EcdhAesEncryptionService — ECDH key exchange over stream, then AES-256-GCM
