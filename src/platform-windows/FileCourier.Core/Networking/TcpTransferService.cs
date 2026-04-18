using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FileCourier.Core.Models;

namespace FileCourier.Core.Networking;

// ── Event args ────────────────────────────────────────────────────────────────

public class TransferProgressEventArgs : EventArgs
{
    public Guid TransferId { get; init; }
    public string CurrentFileName { get; init; } = string.Empty;
    public long BytesTransferred { get; init; }
    public long TotalBytes { get; init; }
    public double SpeedBytesPerSecond { get; init; }
    public double ProgressPercent => TotalBytes > 0 ? (double)BytesTransferred / TotalBytes * 100 : 0;
    public TimeSpan? EstimatedRemaining { get; init; }
}

public class IncomingTransferEventArgs : EventArgs
{
    public Guid TransferId { get; init; }
    public required TransferRequestHeader Header { get; init; }
    public required string SenderIp { get; init; }
    public long TotalBytes => Header.Files.Sum(f => f.FileSize);
    
    // Handler must set these and then call SetDecision()
    public bool Accepted { get; private set; }
    public string SaveDirectory { get; set; } = string.Empty;

    private readonly TaskCompletionSource<bool> _decisionTcs = new();
    public Task<bool> DecisionTask => _decisionTcs.Task;

    public void SetDecision(bool accepted)
    {
        Accepted = accepted;
        _decisionTcs.TrySetResult(accepted);
    }
}

// ── Service ───────────────────────────────────────────────────────────────────

/// <summary>
/// Manages all TCP file transfer sessions (sender and receiver roles).
/// Wire protocol: [4-byte Int32 header length] + [JSON header] + [4MB chunks w/ SHA-256 prefix]
/// </summary>
public sealed class TcpTransferService : IDisposable
{
    public const int DefaultTcpPort = 45455;
    public const int ChunkSize = 4 * 1024 * 1024; // 4 MB

    public event EventHandler<IncomingTransferEventArgs>? IncomingTransferRequested;
    public event EventHandler<TransferProgressEventArgs>? TransferProgressChanged;
    public event EventHandler<Guid>? TransferCompleted;
    public event EventHandler<(Guid TransferId, string Error)>? TransferFailed;
    public event EventHandler<(Guid TransferId, string FileName, string Error)>? FileTransferFailed;
    public event EventHandler<(Guid TransferId, string FileName)>? FileTransferCompleted;

    private readonly int _tcpPort;
    private long _maxBytesPerSecond; // 0 = unlimited
    private TcpListener? _listener;
    private CancellationTokenSource? _cts;
    private bool _disposed;

    public TcpTransferService(int tcpPort = DefaultTcpPort) => _tcpPort = tcpPort;

    public void SetBandwidthLimit(long bytesPerSecond) => _maxBytesPerSecond = bytesPerSecond;

    public void Start()
    {
        _cts = new CancellationTokenSource();
        _listener = new TcpListener(IPAddress.Any, _tcpPort);
        _listener.Start();
        _ = AcceptLoopAsync(_cts.Token);
    }

    // ── Receive side ──────────────────────────────────────────────────────

    private async Task AcceptLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var client = await _listener!.AcceptTcpClientAsync(ct);
                _ = HandleIncomingClientAsync(client, ct);
            }
            catch (OperationCanceledException) { break; }
            catch { /* listener error — keep running */ }
        }
    }

    private async Task HandleIncomingClientAsync(TcpClient client, CancellationToken ct)
    {
        var transferId = Guid.NewGuid();
        using var _ = client;
        await using var stream = client.GetStream();
        var senderIp = ((IPEndPoint)client.Client.RemoteEndPoint!).Address.ToString();

        try
        {
            // 1. Read request header
            var header = await ReadJsonAsync<TransferRequestHeader>(stream, ct);
            if (header is null) return;

            // 2. Pre-flight: raise event so UI can ask the user
            var args = new IncomingTransferEventArgs
            {
                TransferId = transferId,
                Header = header,
                SenderIp = senderIp,
                SaveDirectory = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    "Downloads", "FileCourier")
            };
            IncomingTransferRequested?.Invoke(this, args);
            
            // Wait for handler to set decision (up to 60s timeout for safety)
            var accepted = await Task.WhenAny(args.DecisionTask, Task.Delay(60000, ct)) == args.DecisionTask 
                           ? await args.DecisionTask 
                           : false;

            // 3. Send response
            var response = new TransferResponseHeader
            {
                Status = accepted ? TransferResponseStatus.Accepted : TransferResponseStatus.Rejected,
                Reason = accepted ? string.Empty : "User declined or timeout"
            };
            await WriteJsonAsync(stream, response, ct);

            if (!args.Accepted) return;

            // 4. Receive file chunks
            Directory.CreateDirectory(args.SaveDirectory);
            long totalBytes = header.Files.Sum(f => f.FileSize);
            long received = 0;
            var started = DateTime.UtcNow;

            foreach (var fileInfo in header.Files)
            {
                var destPath = Path.Combine(args.SaveDirectory, fileInfo.RelativePath.Replace('/', Path.DirectorySeparatorChar));
                
                FileStream? fs = null;
                bool skipWriting = false;
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);
                    fs = new FileStream(destPath, fileInfo.ByteOffset > 0 ? FileMode.Append : FileMode.Create, FileAccess.Write);
                }
                catch (Exception ex)
                {
                    skipWriting = true;
                    FileTransferFailed?.Invoke(this, (transferId, fileInfo.FileName, ex.Message));
                }

                long fileReceived = fileInfo.ByteOffset;
                while (fileReceived < fileInfo.FileSize)
                {
                    // Read chunk header: [4-byte chunk size]
                    var chunkSizeBytes = await ReadExactAsync(stream, 4, ct);
                    int chunkLen = BitConverter.ToInt32(chunkSizeBytes);

                    if (chunkLen == -1)
                    {
                        if (fs != null) { await fs.DisposeAsync(); File.Delete(destPath); fs = null; }
                        received += (fileInfo.FileSize - fileReceived);
                        FileTransferFailed?.Invoke(this, (transferId, fileInfo.FileName, "Sender aborted file transfer."));
                        break;
                    }

                    // [32-byte SHA-256]
                    var expectedHash = await ReadExactAsync(stream, 32, ct);
                    var chunkData = await ReadExactAsync(stream, chunkLen, ct);

                    // Verify integrity
                    var actualHash = SHA256.HashData(chunkData);
                    if (!actualHash.SequenceEqual(expectedHash))
                        throw new InvalidDataException($"Checksum mismatch for chunk in {fileInfo.FileName}");

                    if (!skipWriting && fs != null)
                    {
                        try 
                        {
                            await fs.WriteAsync(chunkData, ct);
                        }
                        catch (Exception ex)
                        {
                            skipWriting = true;
                            await fs.DisposeAsync();
                            fs = null;
                            FileTransferFailed?.Invoke(this, (transferId, fileInfo.FileName, ex.Message));
                        }
                    }

                    fileReceived += chunkLen;
                    received += chunkLen;

                    var elapsed = (DateTime.UtcNow - started).TotalSeconds;
                    var speed = elapsed > 0 ? received / elapsed : 0;
                    var remaining = speed > 0 ? TimeSpan.FromSeconds((totalBytes - received) / speed) : (TimeSpan?)null;

                    TransferProgressChanged?.Invoke(this, new TransferProgressEventArgs
                    {
                        TransferId = transferId,
                        CurrentFileName = fileInfo.FileName,
                        BytesTransferred = received,
                        TotalBytes = totalBytes,
                        SpeedBytesPerSecond = speed,
                        EstimatedRemaining = remaining
                    });

                    if (_maxBytesPerSecond > 0)
                        await ThrottleAsync(chunkLen, _maxBytesPerSecond, ct);
                }

                if (fs != null) await fs.DisposeAsync();
                
                if (!skipWriting)
                {
                    FileTransferCompleted?.Invoke(this, (transferId, fileInfo.FileName));
                }
            }

            TransferCompleted?.Invoke(this, transferId);
        }
        catch (Exception ex)
        {
            TransferFailed?.Invoke(this, (transferId, ex.Message));
        }
    }

    // ── Send side ─────────────────────────────────────────────────────────

    public async Task<Guid> SendAsync(
        SystemDevice target,
        TransferRequestHeader header,
        IReadOnlyList<string> filePaths,
        Guid? transferId = null,
        CancellationToken ct = default)
    {
        var tid = transferId ?? Guid.NewGuid();
        using var client = new TcpClient();
        await client.ConnectAsync(target.IPAddress, target.TcpPort, ct);
        await using var stream = client.GetStream();

        // 1. Send request header
        await WriteJsonAsync(stream, header, ct);

        // 2. Read response
        var response = await ReadJsonAsync<TransferResponseHeader>(stream, ct);
        if (response?.Status != TransferResponseStatus.Accepted)
        {
            throw new Exception($"Transfer rejected: {response?.Reason ?? "Unknown reason"}");
        }

        // 3. Stream file chunks
        long totalBytes = header.Files.Sum(f => f.FileSize);
        long sent = 0;
        var started = DateTime.UtcNow;

        for (int i = 0; i < header.Files.Count; i++)
        {
            var fileInfo = header.Files[i];
            var filePath = filePaths[i];

            long sentForThisFile = 0;
            try
            {
                await using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read);
                if (fileInfo.ByteOffset > 0) fs.Seek(fileInfo.ByteOffset, SeekOrigin.Begin);

                var buffer = new byte[ChunkSize];
                int bytesRead;
                while ((bytesRead = await fs.ReadAsync(buffer, ct)) > 0)
                {
                    var chunk = buffer[..bytesRead];
                    var hash = SHA256.HashData(chunk);

                    await stream.WriteAsync(BitConverter.GetBytes(bytesRead), ct);
                    await stream.WriteAsync(hash, ct);
                    await stream.WriteAsync(chunk, ct);

                    sentForThisFile += bytesRead;
                    sent += bytesRead;

                    var elapsed = (DateTime.UtcNow - started).TotalSeconds;
                    var speed = elapsed > 0 ? sent / elapsed : 0;
                    var remaining = speed > 0 ? TimeSpan.FromSeconds((totalBytes - sent) / speed) : (TimeSpan?)null;

                    TransferProgressChanged?.Invoke(this, new TransferProgressEventArgs
                    {
                        TransferId = tid,
                        CurrentFileName = fileInfo.FileName,
                        BytesTransferred = sent,
                        TotalBytes = totalBytes,
                        SpeedBytesPerSecond = speed,
                        EstimatedRemaining = remaining
                    });

                    if (_maxBytesPerSecond > 0)
                        await ThrottleAsync(bytesRead, _maxBytesPerSecond, ct);
                }

                FileTransferCompleted?.Invoke(this, (tid, fileInfo.FileName));
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                await stream.WriteAsync(BitConverter.GetBytes(-1), ct);
                
                long remainder = fileInfo.FileSize - sentForThisFile - fileInfo.ByteOffset;
                if (remainder > 0) sent += remainder;

                FileTransferFailed?.Invoke(this, (tid, fileInfo.FileName, ex.Message));
            }
        }

        TransferCompleted?.Invoke(this, tid);
        return tid;
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private static async Task WriteJsonAsync<T>(Stream stream, T obj, CancellationToken ct)
    {
        var json = JsonSerializer.SerializeToUtf8Bytes(obj);
        var lenBytes = BitConverter.GetBytes(json.Length);
        await stream.WriteAsync(lenBytes, ct);
        await stream.WriteAsync(json, ct);
    }

    private static async Task<T?> ReadJsonAsync<T>(Stream stream, CancellationToken ct)
    {
        var lenBytes = await ReadExactAsync(stream, 4, ct);
        int len = BitConverter.ToInt32(lenBytes);
        var jsonBytes = await ReadExactAsync(stream, len, ct);
        return JsonSerializer.Deserialize<T>(jsonBytes);
    }

    private static async Task<byte[]> ReadExactAsync(Stream stream, int count, CancellationToken ct)
    {
        var buf = new byte[count];
        int offset = 0;
        while (offset < count)
        {
            int read = await stream.ReadAsync(buf.AsMemory(offset, count - offset), ct);
            if (read == 0) throw new EndOfStreamException("Connection closed prematurely.");
            offset += read;
        }
        return buf;
    }

    private static async Task ThrottleAsync(int bytesSent, long maxBytesPerSecond, CancellationToken ct)
    {
        double targetMs = bytesSent / (double)maxBytesPerSecond * 1000;
        if (targetMs > 1) await Task.Delay((int)targetMs, ct);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _cts?.Cancel();
        _listener?.Stop();
        _cts?.Dispose();
    }
}
