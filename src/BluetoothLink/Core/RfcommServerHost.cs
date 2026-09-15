using InTheHand.Net.Sockets;

namespace BluetoothLink.Core;

/// <summary>
/// サーバー (待ち受け) 機能。指定したサービス UUID で RFCOMM の接続を待ち受け、
/// 接続してきたクライアントごとに <see cref="RfcommSession"/> を生成する。
/// </summary>
public sealed class RfcommServerHost : IDisposable
{
    private readonly object _gate = new();

    private BluetoothListener? _listener;
    private Thread? _acceptThread;
    private volatile bool _stopping;

    /// <summary>クライアントが接続したときに発生する (バックグラウンドスレッド)。</summary>
    public event EventHandler<ILinkSession>? ClientConnected;

    /// <summary>待ち受けが終了したときに発生する (バックグラウンドスレッド)。</summary>
    public event EventHandler<string>? ListenerStopped;

    public bool IsListening
    {
        get
        {
            lock (_gate)
            {
                return _listener is not null && !_stopping;
            }
        }
    }

    public Guid ServiceId { get; private set; }

    /// <summary>待ち受けを開始する。</summary>
    /// <param name="serviceId">公開するサービスの UUID。SPP は <c>BluetoothService.SerialPort</c>。</param>
    /// <param name="serviceName">SDP に登録するサービス名。</param>
    /// <param name="framing">受信データの区切り方。</param>
    public void Start(Guid serviceId, string serviceName, FramingMode framing)
    {
        lock (_gate)
        {
            if (_listener is not null)
            {
                throw new InvalidOperationException("すでに待ち受け中です。");
            }

            _stopping = false;
            ServiceId = serviceId;

            var listener = new BluetoothListener(serviceId)
            {
                ServiceName = string.IsNullOrWhiteSpace(serviceName) ? "BluetoothLink" : serviceName,
            };

            listener.Start();
            _listener = listener;

            _acceptThread = new Thread(() => AcceptLoop(listener, framing))
            {
                IsBackground = true,
                Name = "BluetoothAcceptLoop",
            };
            _acceptThread.Start();
        }
    }

    /// <summary>待ち受けを停止する。確立済みのセッションは個別に閉じること。</summary>
    public void Stop()
    {
        BluetoothListener? listener;
        lock (_gate)
        {
            listener = _listener;
            _listener = null;
            _stopping = true;
        }

        if (listener is null)
        {
            return;
        }

        try
        {
            // 停止すると AcceptBluetoothClient() が例外で抜ける。
            listener.Stop();
        }
        catch (Exception)
        {
            // 停止処理中の例外は無視してよい。
        }
    }

    public void Dispose() => Stop();

    private void AcceptLoop(BluetoothListener listener, FramingMode framing)
    {
        string reason = "待ち受けを停止しました。";

        try
        {
            while (!_stopping)
            {
                var client = listener.AcceptBluetoothClient();
                if (_stopping)
                {
                    client.Dispose();
                    break;
                }

                var session = new RfcommSession(client, ConnectionRole.Server, framing);
                ClientConnected?.Invoke(this, session);
            }
        }
        catch (Exception ex)
        {
            if (!_stopping)
            {
                reason = $"待ち受けが異常終了しました: {ex.Message}";
            }
        }

        ListenerStopped?.Invoke(this, reason);
    }
}
