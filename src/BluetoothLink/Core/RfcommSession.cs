using InTheHand.Net;
using InTheHand.Net.Sockets;

namespace BluetoothLink.Core;

/// <summary>
/// Bluetooth Classic (RFCOMM) の接続 1 本。サーバー側・クライアント側のどちらでも同じものを使う。
/// 受信は専用のバックグラウンドタスクで回し続け、イベントで通知する。
/// </summary>
public sealed class RfcommSession : ILinkSession
{
    private const int ReceiveBufferSize = 8 * 1024;

    private readonly BluetoothClient _client;
    private readonly Stream _stream;
    private readonly MessageAssembler _assembler;
    private readonly CancellationTokenSource _cts = new();
    private readonly SemaphoreSlim _sendLock = new(1, 1);

    private Task? _receiveTask;
    private int _closed;

    public RfcommSession(BluetoothClient client, ConnectionRole role, FramingMode framing)
    {
        _client = client;
        _stream = client.GetStream();
        _assembler = new MessageAssembler(framing);
        Role = role;
        Framing = framing;
        RemoteName = ReadRemoteName(client);
        RemoteAddress = ReadRemoteAddress(client);
    }

    public event EventHandler<SessionDataReceivedEventArgs>? DataReceived;

    public event EventHandler<SessionClosedEventArgs>? Closed;

    public LinkKind Kind => LinkKind.Classic;

    public ConnectionRole Role { get; }

    public FramingMode Framing { get; }

    /// <summary>相手機器の表示名。取得できない場合はアドレス文字列。</summary>
    public string RemoteName { get; }

    /// <summary>相手機器の Bluetooth アドレス。取得できない場合は null。</summary>
    public BluetoothAddress? RemoteAddress { get; }

    public bool IsConnected => _closed == 0 && _client.Connected;

    public string DisplayName =>
        RemoteAddress is null ? RemoteName : $"{RemoteName} ({AddressUtil.Format(RemoteAddress)})";

    public void Start()
    {
        _receiveTask ??= Task.Run(ReceiveLoopAsync);
    }

    public async Task SendAsync(byte[] payload, CancellationToken cancellationToken = default)
    {
        if (!IsConnected)
        {
            throw new InvalidOperationException("接続されていません。");
        }

        var framed = MessageAssembler.Frame(payload, Framing);

        await _sendLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            try
            {
                await _stream.WriteAsync(framed, cancellationToken).ConfigureAwait(false);
                await _stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                // 1 回の書き込みなので、途中まで届いたかどうかは判別できない。
                throw new SendFailedException([], ex);
            }
        }
        finally
        {
            _sendLock.Release();
        }
    }

    public void Close(string reason = "切断しました。")
    {
        if (Interlocked.Exchange(ref _closed, 1) != 0)
        {
            return;
        }

        _cts.Cancel();
        SafeDispose();
        Closed?.Invoke(this, new SessionClosedEventArgs(this, reason, null));
    }

    public void Dispose()
    {
        Close();
        _cts.Dispose();
        _sendLock.Dispose();
    }

    private async Task ReceiveLoopAsync()
    {
        var buffer = new byte[ReceiveBufferSize];
        string reason = "相手側から切断されました。";
        Exception? error = null;

        try
        {
            while (!_cts.IsCancellationRequested)
            {
                var read = await _stream.ReadAsync(buffer.AsMemory(), _cts.Token).ConfigureAwait(false);
                if (read <= 0)
                {
                    break;
                }

                foreach (var message in _assembler.Push(buffer, read))
                {
                    DataReceived?.Invoke(this, new SessionDataReceivedEventArgs(this, message));
                }
            }
        }
        catch (OperationCanceledException)
        {
            reason = "切断しました。";
        }
        catch (Exception ex)
        {
            error = ex;
            reason = $"受信中にエラーが発生しました: {ex.Message}";
        }

        // 区切り待ちのまま残ったデータがあれば拾っておく。
        if (error is null)
        {
            var rest = _assembler.Flush();
            if (rest is { Length: > 0 })
            {
                DataReceived?.Invoke(this, new SessionDataReceivedEventArgs(this, rest));
            }
        }

        if (Interlocked.Exchange(ref _closed, 1) == 0)
        {
            SafeDispose();
            Closed?.Invoke(this, new SessionClosedEventArgs(this, reason, error));
        }
    }

    private void SafeDispose()
    {
        try
        {
            _stream.Dispose();
        }
        catch (Exception)
        {
            // 切断済みの場合は無視してよい。
        }

        try
        {
            _client.Dispose();
        }
        catch (Exception)
        {
            // 同上。
        }
    }

    private static string ReadRemoteName(BluetoothClient client)
    {
        try
        {
            var name = client.RemoteMachineName;
            if (!string.IsNullOrWhiteSpace(name))
            {
                return name;
            }
        }
        catch (Exception)
        {
            // 一部スタックでは接続直後に取得できない。
        }

        var address = ReadRemoteAddress(client);
        return address is null ? "(不明なデバイス)" : AddressUtil.Format(address);
    }

    private static BluetoothAddress? ReadRemoteAddress(BluetoothClient client)
    {
        try
        {
            if (client.Client?.RemoteEndPoint is BluetoothEndPoint endPoint)
            {
                return endPoint.Address;
            }
        }
        catch (Exception)
        {
            // 取得できないスタックもあるため無視する。
        }

        return null;
    }
}
