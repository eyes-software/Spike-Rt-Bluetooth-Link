namespace BluetoothLink.Core;

/// <summary>受信メッセージ 1 件。</summary>
public sealed class SessionDataReceivedEventArgs(ILinkSession session, byte[] payload) : EventArgs
{
    public ILinkSession Session { get; } = session;

    public byte[] Payload { get; } = payload;
}

/// <summary>切断通知。</summary>
public sealed class SessionClosedEventArgs(ILinkSession session, string reason, Exception? error) : EventArgs
{
    public ILinkSession Session { get; } = session;

    public string Reason { get; } = reason;

    public Exception? Error { get; } = error;
}

/// <summary>
/// 確立済みの接続 1 本。RFCOMM (Classic) と GATT (BLE) の違いを画面側から隠す。
/// イベントはバックグラウンドスレッドで発生するため、UI からは必ずマーシャリングして使うこと。
/// </summary>
public interface ILinkSession : IDisposable
{
    LinkKind Kind { get; }

    ConnectionRole Role { get; }

    FramingMode Framing { get; }

    /// <summary>"名前 (アドレス)" 形式の表示用文字列。</summary>
    string DisplayName { get; }

    bool IsConnected { get; }

    /// <summary>受信メッセージが 1 件そろったときに発生する。</summary>
    event EventHandler<SessionDataReceivedEventArgs>? DataReceived;

    /// <summary>接続が切れたときに発生する。</summary>
    event EventHandler<SessionClosedEventArgs>? Closed;

    /// <summary>受信の待ち受けを開始する。</summary>
    void Start();

    /// <summary>データを送信する。区切り情報の付加は実装側で行う。</summary>
    Task SendAsync(byte[] payload, CancellationToken cancellationToken = default);

    /// <summary>接続を閉じる。</summary>
    void Close(string reason = "切断しました。");
}
