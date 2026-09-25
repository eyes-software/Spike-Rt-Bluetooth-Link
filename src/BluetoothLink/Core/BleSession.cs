using InTheHand.Bluetooth;
using BleDevice = InTheHand.Bluetooth.BluetoothDevice;

namespace BluetoothLink.Core;

/// <summary>ハブから届いたステータスレポート。</summary>
public sealed class PybricksStatusEventArgs(PybricksStatusReport report) : EventArgs
{
    public PybricksStatusReport Report { get; } = report;
}

/// <summary>通知を受ける特性 1 つと、その中身の解釈方法。</summary>
public sealed class NotifySource
{
    public required GattCharacteristic Characteristic { get; init; }

    /// <summary>true なら先頭バイトが Pybricks のイベント種別。</summary>
    public required bool IsPybricksEvent { get; init; }

    /// <summary>ログに出す名前。</summary>
    public required string Label { get; init; }
}

/// <summary>BLE 接続 1 本を組み立てるための材料。<see cref="BleConnector"/> が用意する。</summary>
public sealed class BleLinkSetup
{
    public required BleDevice Device { get; init; }

    /// <summary>データ (stdin) の書き込み先。</summary>
    public required GattCharacteristic WriteCharacteristic { get; init; }

    /// <summary>書き込み時に先頭へ付けるバイト。Pybricks の新方式なら WRITE_STDIN。</summary>
    public byte? WritePrefix { get; init; }

    /// <summary>通知を受ける特性。Pybricks では Command/Event と NUS TX の 2 本になることがある。</summary>
    public required IReadOnlyList<NotifySource> NotifySources { get; init; }

    /// <summary>Pybricks の制御コマンド送信先 (Command/Event 特性)。</summary>
    public GattCharacteristic? ControlCharacteristic { get; init; }

    public required BleProfile Profile { get; init; }

    public required FramingMode Framing { get; init; }

    /// <summary>1 回の書き込みで送れる最大バイト数。0 なら MTU から求める。</summary>
    public int MaxWriteSize { get; init; }
}

/// <summary>
/// BLE (GATT) の接続 1 本。書き込み用特性へ送信し、通知用特性から受信する。
/// PC 側は常にセントラル (クライアント) なので、サーバー役はハブ側になる。
/// </summary>
public sealed class BleSession : ILinkSession
{
    /// <summary>MTU が取得できない場合の保守的な既定値 (ATT_MTU 23 - 3)。</summary>
    private const int DefaultMaxWrite = 20;

    private readonly BleDevice _device;
    private readonly GattCharacteristic _writeCharacteristic;
    private readonly byte? _writePrefix;
    private readonly IReadOnlyList<NotifySource> _notifySources;
    private readonly GattCharacteristic? _controlCharacteristic;
    private readonly MessageAssembler _assembler;

    // 受信通知スレッドと、切断を指示する UI スレッドの両方から触るため保護する。
    private readonly object _assemblerGate = new();

    private readonly SemaphoreSlim _sendLock = new(1, 1);
    private readonly int _negotiatedMaxWrite;

    private bool _started;
    private int _closed;

    public BleSession(BleLinkSetup setup)
    {
        _device = setup.Device;
        _writeCharacteristic = setup.WriteCharacteristic;
        _writePrefix = setup.WritePrefix;
        _notifySources = setup.NotifySources;
        _controlCharacteristic = setup.ControlCharacteristic;
        _negotiatedMaxWrite = setup.MaxWriteSize;
        _assembler = new MessageAssembler(setup.Framing);
        Profile = setup.Profile;
        Framing = setup.Framing;
        DisplayName = BuildDisplayName(setup.Device);
    }

    public event EventHandler<SessionDataReceivedEventArgs>? DataReceived;

    public event EventHandler<SessionClosedEventArgs>? Closed;

    /// <summary>Pybricks のステータスレポートを受け取ったときに発生する。</summary>
    public event EventHandler<PybricksStatusEventArgs>? StatusReported;

    public LinkKind Kind => LinkKind.Ble;

    public ConnectionRole Role => ConnectionRole.Client;

    public FramingMode Framing { get; }

    public BleProfile Profile { get; }

    public string DisplayName { get; }

    public bool IsConnected => _closed == 0 && (_device.Gatt?.IsConnected ?? false);

    /// <summary>Pybricks の制御コマンド (プログラム開始/停止など) を送れるか。</summary>
    public bool SupportsControl => _controlCharacteristic is not null;

    /// <summary>最後に受け取ったステータスレポート。</summary>
    public PybricksStatusReport? LastStatus { get; private set; }

    /// <summary>1 回の書き込みで送れる最大バイト数。</summary>
    public int MaxWriteLength
    {
        get
        {
            if (_negotiatedMaxWrite > 0)
            {
                return _negotiatedMaxWrite;
            }

            try
            {
                var mtu = _device.Gatt?.Mtu ?? 0;
                return mtu > 3 ? mtu - 3 : DefaultMaxWrite;
            }
            catch (Exception)
            {
                return DefaultMaxWrite;
            }
        }
    }

    public void Start()
    {
        if (_started)
        {
            return;
        }

        _started = true;

        foreach (var source in _notifySources)
        {
            source.Characteristic.CharacteristicValueChanged += OnValueChanged;
        }

        _device.GattServerDisconnected += OnGattServerDisconnected;
    }

    public async Task SendAsync(byte[] payload, CancellationToken cancellationToken = default)
    {
        if (!IsConnected)
        {
            throw new InvalidOperationException("接続されていません。");
        }

        var framed = MessageAssembler.Frame(payload, Framing);
        var writes = BuildWrites(framed, _writePrefix, MaxWriteLength);

        await _sendLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            foreach (var buffer in writes)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await WriteAsync(_writeCharacteristic, buffer).ConfigureAwait(false);
            }
        }
        finally
        {
            _sendLock.Release();
        }
    }

    /// <summary>
    /// Pybricks の制御コマンドを送る。プログラムを開始しないと stdin / stdout は流れない。
    /// </summary>
    public async Task SendControlAsync(PybricksCommand command, byte? parameter = null)
    {
        if (_controlCharacteristic is null)
        {
            throw new InvalidOperationException("この接続では制御コマンドを送れません。");
        }

        if (!IsConnected)
        {
            throw new InvalidOperationException("接続されていません。");
        }

        var buffer = PybricksProtocol.BuildCommand(command, parameter);

        await _sendLock.WaitAsync().ConfigureAwait(false);
        try
        {
            // 制御コマンドはハブからの応答を待つ (pybricksdev も response=True で送る)。
            await _controlCharacteristic.WriteValueWithResponseAsync(buffer).ConfigureAwait(false);
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

        Detach();

        // 区切り待ちのまま残っているデータを捨てない (RfcommSession と挙動を揃える)。
        FlushPending();

        SafeDisconnect();
        Closed?.Invoke(this, new SessionClosedEventArgs(this, reason, null));
    }

    public void Dispose()
    {
        Close();
        _sendLock.Dispose();
    }

    /// <summary>
    /// 通知で届いた 1 パケットから実データを取り出す。データでない場合は null。
    /// 実機なしで検証できるよう純粋関数として切り出している。
    /// </summary>
    public static byte[]? DecodeNotification(byte[]? value, bool isPybricksEvent)
    {
        if (value is null || value.Length == 0)
        {
            return null;
        }

        if (!isPybricksEvent)
        {
            return value;
        }

        // 先頭バイトがイベント種別。stdout 以外 (ステータス通知など) はデータではない。
        if (value[0] != (byte)PybricksEvent.WriteStdout)
        {
            return null;
        }

        return value.Length > 1 ? value[1..] : null;
    }

    /// <summary>
    /// 送信データを 1 回の書き込みに収まるサイズへ分割する。
    /// prefix があれば分割後の各パケットの先頭に付ける。
    /// </summary>
    public static List<byte[]> BuildWrites(byte[] framed, byte? prefix, int maxWriteLength)
    {
        var prefixLength = prefix is null ? 0 : 1;
        var chunkSize = Math.Max(1, maxWriteLength - prefixLength);
        var writes = new List<byte[]>();

        for (var offset = 0; offset < framed.Length; offset += chunkSize)
        {
            var length = Math.Min(chunkSize, framed.Length - offset);
            var buffer = new byte[prefixLength + length];
            if (prefix is not null)
            {
                buffer[0] = prefix.Value;
            }

            Array.Copy(framed, offset, buffer, prefixLength, length);
            writes.Add(buffer);
        }

        return writes;
    }

    private Task WriteAsync(GattCharacteristic characteristic, byte[] buffer)
    {
        return characteristic.Properties.HasFlag(GattCharacteristicProperties.WriteWithoutResponse)
            ? characteristic.WriteValueWithoutResponseAsync(buffer)
            : characteristic.WriteValueWithResponseAsync(buffer);
    }

    private void OnValueChanged(object? sender, GattCharacteristicValueChangedEventArgs e)
    {
        if (e.Error is not null)
        {
            return;
        }

        var source = _notifySources.FirstOrDefault(s => ReferenceEquals(s.Characteristic, sender))
            ?? _notifySources[0];

        if (source.IsPybricksEvent)
        {
            // ステータスレポートは画面へ通知する。これが届いていれば通信路は生きている。
            var status = PybricksProtocol.ParseStatusReport(e.Value);
            if (status is not null)
            {
                var previous = LastStatus;
                LastStatus = status;
                StatusReported?.Invoke(this, new PybricksStatusEventArgs(status));

                // プログラムが止まると stdout も止まる。接続は生きたままなので切断契機が来ず、
                // 区切り待ちの最後の 1 行が取り残される。ここで吐き出しておく。
                if (previous is { UserProgramRunning: true } && !status.UserProgramRunning)
                {
                    FlushPending();
                }

                return;
            }
        }

        var value = DecodeNotification(e.Value, source.IsPybricksEvent);
        if (value is null)
        {
            return;
        }

        List<byte[]> messages;
        try
        {
            lock (_assemblerGate)
            {
                messages = _assembler.Push(value, value.Length).ToList();
            }
        }
        catch (InvalidDataException ex)
        {
            CloseWithError($"受信データの組み立てに失敗しました: {ex.Message}", ex);
            return;
        }

        foreach (var message in messages)
        {
            DataReceived?.Invoke(this, new SessionDataReceivedEventArgs(this, message));
        }
    }

    /// <summary>区切り待ちのまま残っているデータがあれば 1 件として取り出す。</summary>
    private void FlushPending()
    {
        byte[]? rest;
        lock (_assemblerGate)
        {
            rest = _assembler.Flush();
        }

        if (rest is { Length: > 0 })
        {
            DataReceived?.Invoke(this, new SessionDataReceivedEventArgs(this, rest));
        }
    }

    private void OnGattServerDisconnected(object? sender, EventArgs e)
    {
        if (Interlocked.Exchange(ref _closed, 1) != 0)
        {
            return;
        }

        Detach();
        FlushPending();
        Closed?.Invoke(this, new SessionClosedEventArgs(this, "相手側から切断されました。", null));
    }

    private void CloseWithError(string reason, Exception error)
    {
        if (Interlocked.Exchange(ref _closed, 1) != 0)
        {
            return;
        }

        // ここへ来るのは組み立てに失敗した場合なので、残りは信用できない。あえて吐き出さない。
        Detach();
        SafeDisconnect();
        Closed?.Invoke(this, new SessionClosedEventArgs(this, reason, error));
    }

    private void Detach()
    {
        try
        {
            foreach (var source in _notifySources)
            {
                source.Characteristic.CharacteristicValueChanged -= OnValueChanged;
            }

            _device.GattServerDisconnected -= OnGattServerDisconnected;
        }
        catch (Exception)
        {
            // 切断済みの場合は無視してよい。
        }
    }

    private void SafeDisconnect()
    {
        try
        {
            _device.Gatt?.Disconnect();
        }
        catch (Exception)
        {
            // 同上。
        }
    }

    private static string BuildDisplayName(BleDevice device)
    {
        var name = string.IsNullOrWhiteSpace(device.Name) ? "(名前なし)" : device.Name;
        var address = AddressUtil.TryParse(device.Id);
        return address is null ? $"{name} [{device.Id}]" : $"{name} ({AddressUtil.Format(address)})";
    }
}
