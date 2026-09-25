using System.Diagnostics;
using System.Text;
using System.Threading.Channels;

namespace BluetoothLink.Core;

/// <summary>ファイル書き出しの設定。</summary>
public sealed class DataLoggerOptions
{
    /// <summary>ログの出力先フォルダー。</summary>
    public string Directory { get; set; } =
        Path.Combine(AppContext.BaseDirectory, "logs");

    /// <summary>テキストログ (.log) を書き出すか。</summary>
    public bool WriteTextLog { get; set; } = true;

    /// <summary>送信/受信の生バイト列 (.bin) を書き出すか。</summary>
    public bool WriteRawBinary { get; set; } = true;

    /// <summary>テキストログ内でペイロードを文字列化するときのエンコーディング。</summary>
    public Encoding TextEncoding { get; set; } = Encoding.UTF8;

    /// <summary>書き出し待ち行列の上限。溢れた分は破棄し、件数を数える。</summary>
    public int QueueCapacity { get; set; } = 200_000;

    /// <summary>この量を書いたらフラッシュする。</summary>
    public int FlushBytes { get; set; } = 64 * 1024;

    /// <summary>連続受信中でも、この間隔では必ずフラッシュする。</summary>
    public int FlushIntervalMs { get; set; } = 200;
}

/// <summary>書き出し待ち行列に積む 1 件。</summary>
internal readonly struct LogEntry
{
    public required DateTime Timestamp { get; init; }

    public required TransferDirection Direction { get; init; }

    public byte[]? Payload { get; init; }

    public string? Message { get; init; }
}

/// <summary>
/// 送受信データをファイルへ書き出す。1 セッション = 1 組のファイル。
/// 呼び出し側 (通信スレッド) は待ち行列へ積むだけで、実際のディスク I/O は
/// 専用のバックグラウンドタスクがまとめて行う。
/// </summary>
public sealed class DataLogger : IDisposable
{
    private readonly DataLoggerOptions _options;
    private readonly object _gate = new();

    private LogSink? _sink;
    private long _lastDropped;
    private long _lastWriteErrors;
    private string? _lastWriteErrorMessage;
    private bool _disposed;

    public DataLogger(DataLoggerOptions options)
    {
        _options = options;
    }

    /// <summary>現在のテキストログのフルパス。未開始なら null。</summary>
    public string? TextLogPath { get; private set; }

    /// <summary>出力先フォルダーのフルパス。</summary>
    public string DirectoryPath => _options.Directory;

    /// <summary>書き出し中かどうか。</summary>
    public bool IsOpen => Volatile.Read(ref _sink) is not null;

    /// <summary>
    /// 待ち行列が溢れて記録できなかった件数。0 以外なら画面に出して知らせること。
    /// セッションを閉じたあとも、直前のセッションの件数を保持する。
    /// </summary>
    public long DroppedCount => Volatile.Read(ref _sink)?.Dropped ?? Interlocked.Read(ref _lastDropped);

    /// <summary>
    /// ディスクへ書けずに失われた件数。待ち行列のあふれとは別に数える。
    /// 0 以外なら画面に出して知らせること (黙って消えるのを防ぐ)。
    /// </summary>
    public long WriteErrorCount => Volatile.Read(ref _sink)?.WriteErrors ?? Interlocked.Read(ref _lastWriteErrors);

    /// <summary>直近の書き出しエラーの内容。原因を画面に出すために使う。</summary>
    public string? LastWriteError => Volatile.Read(ref _sink)?.LastWriteError ?? _lastWriteErrorMessage;

    /// <summary>記録できなかった合計件数 (待ち行列あふれ + 書き出し失敗)。</summary>
    public long LostCount => DroppedCount + WriteErrorCount;

    /// <summary>
    /// 新しいセッション用のファイルを開く。既に開いている場合は閉じてから開き直す。
    /// </summary>
    /// <param name="tag">ファイル名に含める識別子 (接続相手名など)。</param>
    /// <returns>テキストログのフルパス (テキストログ無効時は出力フォルダー)。</returns>
    public string Open(string tag)
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            CloseCore();

            Interlocked.Exchange(ref _lastDropped, 0);
            Interlocked.Exchange(ref _lastWriteErrors, 0);
            _lastWriteErrorMessage = null;

            var sink = new LogSink(_options, tag);
            Volatile.Write(ref _sink, sink);
            TextLogPath = sink.TextPath;

            return TextLogPath ?? _options.Directory;
        }
    }

    /// <summary>
    /// 送受信 1 件を待ち行列へ積む。ディスクへは触れないため、通信スレッドから直接呼んでよい。
    /// </summary>
    public void LogData(TransferDirection direction, byte[] payload)
    {
        Volatile.Read(ref _sink)?.Enqueue(new LogEntry
        {
            Timestamp = DateTime.Now,
            Direction = direction,
            Payload = payload,
        });
    }

    /// <summary>接続開始や切断などのイベントを待ち行列へ積む。</summary>
    public void LogEvent(TransferDirection direction, string message)
    {
        Volatile.Read(ref _sink)?.Enqueue(new LogEntry
        {
            Timestamp = DateTime.Now,
            Direction = direction,
            Message = message,
        });
    }

    /// <summary>現在のセッションのファイルを閉じる。書き残しは出し切る。</summary>
    public void Close()
    {
        lock (_gate)
        {
            CloseCore();
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            CloseCore();
            _disposed = true;
        }
    }

    private void CloseCore()
    {
        var sink = Volatile.Read(ref _sink);
        if (sink is null)
        {
            return;
        }

        Volatile.Write(ref _sink, null);
        sink.CompleteAndWait(TimeSpan.FromSeconds(10));

        // 閉じたあとも件数を参照できるようにしておく。
        Interlocked.Exchange(ref _lastDropped, sink.Dropped);
        Interlocked.Exchange(ref _lastWriteErrors, sink.WriteErrors);
        _lastWriteErrorMessage = sink.LastWriteError;
    }
}

/// <summary>1 セッション分のファイルと、その書き出しタスク。</summary>
internal sealed class LogSink
{
    private readonly DataLoggerOptions _options;
    private readonly Channel<LogEntry> _channel;
    private readonly Task _writerTask;

    private readonly StreamWriter? _textWriter;
    private readonly FileStream? _txRaw;
    private readonly FileStream? _rxRaw;

    private readonly Encoding _textEncoding;

    private long _dropped;
    private long _writeErrors;
    private string? _lastWriteError;
    private int _completed;

    public LogSink(DataLoggerOptions options, string tag)
    {
        _options = options;

        // 途中で設定を変えられてもファイル内で表記が揺れないよう、開いた時点の値を固定する。
        _textEncoding = options.TextEncoding;

        System.IO.Directory.CreateDirectory(options.Directory);

        var stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var safeTag = Sanitize(tag);
        var baseName = string.IsNullOrEmpty(safeTag) ? $"bt_{stamp}" : $"bt_{stamp}_{safeTag}";
        var basePath = Path.Combine(options.Directory, baseName);

        if (options.WriteTextLog)
        {
            TextPath = basePath + ".log";

            // AutoFlush は使わない。フラッシュのタイミングは書き出しタスクが決める。
            _textWriter = new StreamWriter(
                new FileStream(TextPath, FileMode.Create, FileAccess.Write, FileShare.Read, 64 * 1024),
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: true))
            {
                AutoFlush = false,
            };

            _textWriter.WriteLine("# BluetoothLink データログ");
            _textWriter.WriteLine($"# 開始: {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}");
            _textWriter.WriteLine($"# 相手: {tag}");
            _textWriter.WriteLine($"# 文字コード(表示用): {_textEncoding.WebName}");
            _textWriter.WriteLine("# 時刻\t方向\tバイト数\t16進\tテキスト");
        }

        if (options.WriteRawBinary)
        {
            _txRaw = new FileStream(basePath + "_tx.bin", FileMode.Create, FileAccess.Write, FileShare.Read, 64 * 1024);
            _rxRaw = new FileStream(basePath + "_rx.bin", FileMode.Create, FileAccess.Write, FileShare.Read, 64 * 1024);
        }

        _channel = Channel.CreateBounded<LogEntry>(new BoundedChannelOptions(options.QueueCapacity)
        {
            // 満杯でも書き込み側は待たせない。TryWrite が false を返すので、その件数を数える。
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false,
        });

        _writerTask = Task.Run(WriteLoopAsync);
    }

    public string? TextPath { get; }

    public long Dropped => Interlocked.Read(ref _dropped);

    public long WriteErrors => Interlocked.Read(ref _writeErrors);

    public string? LastWriteError => Volatile.Read(ref _lastWriteError);

    public void Enqueue(LogEntry entry)
    {
        if (Volatile.Read(ref _completed) != 0)
        {
            return;
        }

        if (!_channel.Writer.TryWrite(entry))
        {
            Interlocked.Increment(ref _dropped);
        }
    }

    /// <summary>受け付けを止め、書き残しを出し切ってからファイルを閉じる。</summary>
    public void CompleteAndWait(TimeSpan timeout)
    {
        if (Interlocked.Exchange(ref _completed, 1) != 0)
        {
            return;
        }

        _channel.Writer.TryComplete();

        try
        {
            _writerTask.Wait(timeout);
        }
        catch (AggregateException)
        {
            // 書き出しタスク内の例外は閉じる処理を妨げない。
        }

        CloseFiles();
    }

    private async Task WriteLoopAsync()
    {
        var reader = _channel.Reader;
        var sinceFlush = Stopwatch.StartNew();
        var pendingBytes = 0;

        while (await reader.WaitToReadAsync().ConfigureAwait(false))
        {
            while (reader.TryRead(out var entry))
            {
                pendingBytes += WriteOne(entry);

                // 受信が途切れない場合でも、量か時間でフラッシュする。
                if (pendingBytes >= _options.FlushBytes || sinceFlush.ElapsedMilliseconds >= _options.FlushIntervalMs)
                {
                    Flush();
                    pendingBytes = 0;
                    sinceFlush.Restart();
                }
            }

            // 待ち行列が空になった = 落ち着いたので、ここで確実にディスクへ出す。
            Flush();
            pendingBytes = 0;
            sinceFlush.Restart();
        }

        Flush();
    }

    private int WriteOne(LogEntry entry)
    {
        try
        {
            if (entry.Payload is { } payload)
            {
                if (_textWriter is not null)
                {
                    _textWriter.WriteLine(string.Join(
                        '\t',
                        entry.Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff"),
                        entry.Direction == TransferDirection.Tx ? "TX" : "RX",
                        payload.Length.ToString(),
                        HexUtil.ToHex(payload),
                        HexUtil.ToPrintable(SafeDecode(payload))));
                }

                var raw = entry.Direction == TransferDirection.Tx ? _txRaw : _rxRaw;
                raw?.Write(payload, 0, payload.Length);

                return payload.Length;
            }

            if (entry.Message is { } message && _textWriter is not null)
            {
                _textWriter.WriteLine(string.Join(
                    '\t',
                    entry.Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff"),
                    entry.Direction == TransferDirection.Error ? "ERR" : "INF",
                    "-",
                    "-",
                    HexUtil.ToPrintable(message)));

                return message.Length;
            }
        }
        catch (ObjectDisposedException)
        {
            // 閉じた直後に届いた分は捨ててよい。件数には数えない。
        }
        catch (Exception ex)
        {
            // ディスク満杯やドライブ切断など。通信は続行させるが、
            // 黙って消えると気づけないので必ず数えて記録する。
            Interlocked.Increment(ref _writeErrors);
            Volatile.Write(ref _lastWriteError, $"{ex.GetType().Name}: {ex.Message}");
        }

        return 0;
    }

    private void Flush()
    {
        try
        {
            _textWriter?.Flush();
            _txRaw?.Flush();
            _rxRaw?.Flush();
        }
        catch (Exception)
        {
            // フラッシュ失敗でタスクを落とさない。
        }
    }

    private void CloseFiles()
    {
        try
        {
            if (_textWriter is not null)
            {
                _textWriter.WriteLine($"# 終了: {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}");
                if (Dropped > 0)
                {
                    _textWriter.WriteLine($"# 待ち行列あふれで記録できなかった件数: {Dropped}");
                }

                if (WriteErrors > 0)
                {
                    _textWriter.WriteLine($"# 書き出し失敗で記録できなかった件数: {WriteErrors} ({LastWriteError})");
                }

                _textWriter.Dispose();
            }

            _txRaw?.Dispose();
            _rxRaw?.Dispose();
        }
        catch (Exception)
        {
            // 閉じる際の例外は無視してよい。
        }
    }

    private string SafeDecode(byte[] payload)
    {
        try
        {
            return _textEncoding.GetString(payload);
        }
        catch (DecoderFallbackException)
        {
            return "(デコード不可)";
        }
    }

    private static string Sanitize(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sb = new StringBuilder(value.Length);
        foreach (var c in value)
        {
            sb.Append(Array.IndexOf(invalid, c) >= 0 || c == ' ' ? '_' : c);
        }

        return sb.ToString().Trim('_');
    }
}
