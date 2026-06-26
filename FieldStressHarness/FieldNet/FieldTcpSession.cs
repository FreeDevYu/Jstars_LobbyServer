using System.Collections.Concurrent;
using System.Net.Sockets;
using protocol;

namespace FieldStressHarness.FieldNet;

public sealed class FieldTcpSession : IAsyncDisposable
{
    private readonly ConcurrentQueue<FieldMessage> _incoming = new();
    private readonly object _sendLock = new();
    private TcpClient? _tcpClient;
    private NetworkStream? _stream;
    private MessageBuilder? _messageBuilder;
    private CancellationTokenSource? _receiveCts;
    private Task? _receiveTask;
    private volatile bool _connected;

    public int SentPackets { get; private set; }
    public int ReceivedPackets { get; private set; }

    public async Task ConnectAsync(string ip, int port, CancellationToken cancellationToken = default)
    {
        Disconnect();

        _tcpClient = new TcpClient();
        await _tcpClient.ConnectAsync(ip, port, cancellationToken);

        _tcpClient.ReceiveTimeout = 1000;
        _tcpClient.SendTimeout = 5000;
        _stream = _tcpClient.GetStream();
        _messageBuilder = new MessageBuilder();
        _messageBuilder.Initialize(NetworkDefine.NetworkBufferSize);

        _connected = true;
        _receiveCts = new CancellationTokenSource();
        _receiveTask = Task.Run(() => ReceiveLoop(_receiveCts.Token), CancellationToken.None);
    }

    public void Send(byte[] messageBytes)
    {
        if (!_connected || _stream == null)
        {
            throw new InvalidOperationException("Field TCP is not connected.");
        }

        lock (_sendLock)
        {
            _stream.Write(messageBytes, 0, messageBytes.Length);
            _stream.Flush();
            SentPackets++;
        }
    }

    public async Task<FieldMessage> WaitForContentAsync(
        Content contentType,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);

        while (!timeoutCts.IsCancellationRequested)
        {
            if (TryTake(contentType, out var message) && message != null)
            {
                return message;
            }

            if (_receiveTask?.IsCompleted == true && _incoming.IsEmpty)
            {
                throw new IOException("Field TCP connection closed while waiting for packet.");
            }

            await Task.Delay(50, timeoutCts.Token);
        }

        throw new TimeoutException($"Timed out waiting for {contentType}.");
    }

    public void DrainIncoming()
    {
        while (_incoming.TryDequeue(out _))
        {
        }
    }

    public void Disconnect()
    {
        _connected = false;
        _receiveCts?.Cancel();

        try
        {
            _receiveTask?.Wait(TimeSpan.FromSeconds(2));
        }
        catch
        {
            // ignore shutdown races
        }

        _stream?.Close();
        _tcpClient?.Close();
        _stream = null;
        _tcpClient = null;
        _receiveTask = null;
        _receiveCts?.Dispose();
        _receiveCts = null;
        DrainIncoming();
    }

    public bool TryTake(Content contentType, out FieldMessage? message)
    {
        var retained = new List<FieldMessage>();
        try
        {
            while (_incoming.TryDequeue(out var candidate))
            {
                if (candidate.ContentType == contentType)
                {
                    message = candidate;
                    foreach (var kept in retained)
                    {
                        _incoming.Enqueue(kept);
                    }

                    return true;
                }

                retained.Add(candidate);
            }
        }
        catch
        {
            foreach (var kept in retained)
            {
                _incoming.Enqueue(kept);
            }

            throw;
        }

        message = null;
        return false;
    }

    private void ReceiveLoop(CancellationToken cancellationToken)
    {
        var receiveBuffer = new byte[NetworkDefine.NetworkBufferSize];

        try
        {
            while (_connected && !cancellationToken.IsCancellationRequested && _stream != null && _messageBuilder != null)
            {
                int bytesRead;
                try
                {
                    bytesRead = _stream.Read(receiveBuffer, 0, receiveBuffer.Length);
                }
                catch (IOException ex) when (IsTransient(ex))
                {
                    Thread.Sleep(2);
                    continue;
                }

                if (bytesRead <= 0)
                {
                    break;
                }

                _messageBuilder.InsertMessage(receiveBuffer, 0, bytesRead);
                while (_messageBuilder.PopCompleteMessage(out var completeMessage) && completeMessage != null)
                {
                    var header = new MessageHeader(completeMessage.AsSpan(0, NetworkDefine.NetworkHeaderSize));
                    var body = new byte[header.BodySize];
                    Array.Copy(
                        completeMessage,
                        NetworkDefine.NetworkHeaderSize,
                        body,
                        0,
                        (int)header.BodySize);

                    _incoming.Enqueue(new FieldMessage
                    {
                        Header = header,
                        Body = body
                    });
                    ReceivedPackets++;
                }
            }
        }
        catch (Exception) when (cancellationToken.IsCancellationRequested)
        {
            // graceful shutdown
        }
        finally
        {
            _connected = false;
        }
    }

    private static bool IsTransient(IOException ex)
    {
        return ex.InnerException is SocketException socketEx
            && (socketEx.SocketErrorCode == SocketError.TimedOut
                || socketEx.SocketErrorCode == SocketError.WouldBlock);
    }

    public async ValueTask DisposeAsync()
    {
        Disconnect();
        await Task.CompletedTask;
    }
}
