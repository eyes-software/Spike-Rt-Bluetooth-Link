using System.Diagnostics;

namespace BluetoothLink.Core;

/// <summary>ある瞬間の統計。前回の取得からの差でレートを出す。</summary>
public readonly record struct StatsSnapshot(
    long RxMessages,
    long RxBytes,
    long TxMessages,
    long TxBytes,
    double RxMessageRate,
    double RxByteRate,
    double TxMessageRate,
    double TxByteRate)
{
    /// <summary>ステータスバーに出す 1 行。</summary>
    public string Describe() =>
        $"受信 {RxMessages:N0} 件 / {FormatBytes(RxBytes)}  ({RxMessageRate:N0} 件/秒, {FormatBytes((long)RxByteRate)}/秒)" +
        $"    送信 {TxMessages:N0} 件 / {FormatBytes(TxBytes)}  ({TxMessageRate:N0} 件/秒, {FormatBytes((long)TxByteRate)}/秒)";

    public static string FormatBytes(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => $"{bytes / 1024.0:N1} KB",
        _ => $"{bytes / (1024.0 * 1024.0):N1} MB",
    };
}

/// <summary>
/// 送受信の件数とバイト数。通信スレッドからは <see cref="Interlocked"/> で足すだけなので、
/// 高頻度に呼ばれてもロック待ちが発生しない。
/// </summary>
public sealed class TransferStats
{
    private long _rxMessages;
    private long _rxBytes;
    private long _txMessages;
    private long _txBytes;

    private long _lastRxMessages;
    private long _lastRxBytes;
    private long _lastTxMessages;
    private long _lastTxBytes;
    private long _lastTimestamp = Stopwatch.GetTimestamp();

    public void AddRx(int bytes)
    {
        Interlocked.Increment(ref _rxMessages);
        Interlocked.Add(ref _rxBytes, bytes);
    }

    public void AddTx(int bytes)
    {
        Interlocked.Increment(ref _txMessages);
        Interlocked.Add(ref _txBytes, bytes);
    }

    public void Reset()
    {
        Interlocked.Exchange(ref _rxMessages, 0);
        Interlocked.Exchange(ref _rxBytes, 0);
        Interlocked.Exchange(ref _txMessages, 0);
        Interlocked.Exchange(ref _txBytes, 0);
        Interlocked.Exchange(ref _lastRxMessages, 0);
        Interlocked.Exchange(ref _lastRxBytes, 0);
        Interlocked.Exchange(ref _lastTxMessages, 0);
        Interlocked.Exchange(ref _lastTxBytes, 0);
        Interlocked.Exchange(ref _lastTimestamp, Stopwatch.GetTimestamp());
    }

    /// <summary>現在値を取り、前回呼び出しからの経過でレートを計算する。UI スレッドから周期的に呼ぶ。</summary>
    public StatsSnapshot Take()
    {
        var rxMessages = Interlocked.Read(ref _rxMessages);
        var rxBytes = Interlocked.Read(ref _rxBytes);
        var txMessages = Interlocked.Read(ref _txMessages);
        var txBytes = Interlocked.Read(ref _txBytes);

        var now = Stopwatch.GetTimestamp();
        var seconds = (now - Interlocked.Exchange(ref _lastTimestamp, now)) / (double)Stopwatch.Frequency;

        double Rate(long current, ref long previous)
        {
            var delta = current - Interlocked.Exchange(ref previous, current);
            return seconds > 0 ? delta / seconds : 0;
        }

        return new StatsSnapshot(
            rxMessages,
            rxBytes,
            txMessages,
            txBytes,
            Rate(rxMessages, ref _lastRxMessages),
            Rate(rxBytes, ref _lastRxBytes),
            Rate(txMessages, ref _lastTxMessages),
            Rate(txBytes, ref _lastTxBytes));
    }
}
