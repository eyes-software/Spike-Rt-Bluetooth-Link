namespace BluetoothLink.Core;

/// <summary>モニタに表示する 1 件。表示用の文字列化は描画直前まで行わない。</summary>
public sealed class MonitorRecord
{
    /// <summary>通し番号。バッファから溢れた件数を知るのに使う。</summary>
    public required long Sequence { get; init; }

    public required DateTime Timestamp { get; init; }

    public required TransferDirection Direction { get; init; }

    /// <summary>相手の表示名。情報/エラー行では空。</summary>
    public string Peer { get; init; } = string.Empty;

    /// <summary>送受信データ。情報/エラー行では null。</summary>
    public byte[]? Payload { get; init; }

    /// <summary>情報/エラー行の本文。送受信行では null。</summary>
    public string? Message { get; init; }

    public bool IsData => Payload is not null;
}

/// <summary>
/// 受信/送信スレッドが投入し、UI スレッドがタイマー駆動で読み出す固定長リングバッファ。
/// 投入側のロック保持時間を最小にすることで、通信スレッドを止めないことを狙う。
/// </summary>
public sealed class MonitorBuffer
{
    private readonly object _gate = new();
    private readonly MonitorRecord[] _items;

    private int _start;
    private int _count;
    private long _sequence;
    private long _discardBase;

    public MonitorBuffer(int capacity)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1);
        _items = new MonitorRecord[capacity];
    }

    public int Capacity => _items.Length;

    /// <summary>いま保持している件数 (最大 <see cref="Capacity"/>)。</summary>
    public int Count
    {
        get
        {
            lock (_gate)
            {
                return _count;
            }
        }
    }

    /// <summary>投入された総件数。<see cref="Count"/> を超えた分は古いものから捨てられている。</summary>
    public long TotalAdded
    {
        get
        {
            lock (_gate)
            {
                return _sequence;
            }
        }
    }

    /// <summary>容量超過で画面から消えた件数 (ファイルには残っている)。</summary>
    public long Overwritten
    {
        get
        {
            lock (_gate)
            {
                return _sequence - _count - _discardBase;
            }
        }
    }

    /// <summary>送受信データを 1 件積む。どのスレッドから呼んでもよい。</summary>
    public void AddData(TransferDirection direction, string peer, byte[] payload) =>
        Add(direction, peer, payload, null);

    /// <summary>情報/エラー行を 1 件積む。どのスレッドから呼んでもよい。</summary>
    public void AddMessage(TransferDirection direction, string message) =>
        Add(direction, string.Empty, null, message);

    private void Add(TransferDirection direction, string peer, byte[]? payload, string? message)
    {
        lock (_gate)
        {
            var record = new MonitorRecord
            {
                Sequence = _sequence++,
                Timestamp = DateTime.Now,
                Direction = direction,
                Peer = peer,
                Payload = payload,
                Message = message,
            };

            var index = (_start + _count) % _items.Length;
            _items[index] = record;

            if (_count < _items.Length)
            {
                _count++;
            }
            else
            {
                // 満杯なら最古を押し出す。
                _start = (_start + 1) % _items.Length;
            }
        }
    }

    /// <summary>0 を最古として 1 件取り出す。範囲外なら null。</summary>
    public MonitorRecord? Get(int index)
    {
        lock (_gate)
        {
            if (index < 0 || index >= _count)
            {
                return null;
            }

            return _items[(_start + index) % _items.Length];
        }
    }

    public void Clear()
    {
        lock (_gate)
        {
            Array.Clear(_items);
            _start = 0;
            _count = 0;

            // 通し番号は維持しつつ、「表示外になった件数」の基準だけずらす。
            // 手動クリアは容量あふれとは別扱いにする。
            _discardBase = _sequence;
        }
    }
}
