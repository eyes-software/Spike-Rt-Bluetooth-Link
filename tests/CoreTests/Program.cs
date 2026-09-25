using System.Text;
using BluetoothLink.Core;

// Shift_JIS を使う検証があるため、アプリ本体と同じくコードページを有効化する。
Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

var failures = 0;

void Check(string name, bool ok, string? detail = null)
{
    Console.WriteLine($"{(ok ? "PASS" : "FAIL")}  {name}{(detail is null ? "" : "  -> " + detail)}");
    if (!ok) failures++;
}

static byte[][] Roundtrip(FramingMode mode, byte[][] messages, int chunkSize)
{
    var stream = new List<byte>();
    foreach (var m in messages) stream.AddRange(MessageAssembler.Frame(m, mode));

    var asm = new MessageAssembler(mode);
    var result = new List<byte[]>();
    for (var i = 0; i < stream.Count; i += chunkSize)
    {
        var n = Math.Min(chunkSize, stream.Count - i);
        var chunk = stream.GetRange(i, n).ToArray();
        result.AddRange(asm.Push(chunk, n));
    }
    var rest = asm.Flush();
    if (rest is { Length: > 0 }) result.Add(rest);
    return result.ToArray();
}

static bool Same(byte[][] a, byte[][] b)
{
    if (a.Length != b.Length) return false;
    for (var i = 0; i < a.Length; i++)
        if (!a[i].AsSpan().SequenceEqual(b[i])) return false;
    return true;
}

var binary = new[]
{
    Encoding.UTF8.GetBytes("Hello"),
    Encoding.UTF8.GetBytes("こんにちは、Bluetooth"),
    new byte[] { 0x00, 0x01, 0xFF, 0x7F },
};
var textMsgs = new[] { binary[0], binary[1], Encoding.UTF8.GetBytes("last") };

// ---------- 区切り ----------
foreach (var chunk in new[] { 1, 3, 7, 4096 })
{
    Check($"CRLF roundtrip (chunk={chunk})", Same(textMsgs, Roundtrip(FramingMode.CrLfDelimited, textMsgs, chunk)));
    Check($"CR roundtrip (chunk={chunk})", Same(textMsgs, Roundtrip(FramingMode.CrDelimited, textMsgs, chunk)));
    Check($"LengthPrefixed roundtrip (chunk={chunk})", Same(binary, Roundtrip(FramingMode.LengthPrefixed, binary, chunk)));
}

{
    var joined = Roundtrip(FramingMode.Raw, binary, 5).SelectMany(x => x).ToArray();
    Check("Raw preserves byte order", joined.AsSpan().SequenceEqual(binary.SelectMany(x => x).ToArray()));
}

// CR 区切りの送信は CR ちょうど 1 バイトで終端する (SPIKE Hub の JSON プロトコル)
{
    var framed = MessageAssembler.Frame("{\"i\":\"x\"}"u8.ToArray(), FramingMode.CrDelimited);
    Check("CR framing appends exactly 0x0D", framed[^1] == 0x0D && framed.Length == 9 + 1);
    Check("CR framing has no LF", !framed.Contains((byte)0x0A));
}

// SPIKE Hub 相当: CR だけで区切られた 2 件が、途中で分割されても復元できる
{
    var wire = Encoding.UTF8.GetBytes("{\"m\":0}\r{\"m\":1}\r");
    var asm = new MessageAssembler(FramingMode.CrDelimited);
    var got = new List<byte[]>();
    for (var i = 0; i < wire.Length; i += 3)
    {
        var n = Math.Min(3, wire.Length - i);
        got.AddRange(asm.Push(wire.Skip(i).Take(n).ToArray(), n));
    }
    Check("CR framing splits SPIKE-style JSON",
        got.Count == 2
        && Encoding.UTF8.GetString(got[0]) == "{\"m\":0}"
        && Encoding.UTF8.GetString(got[1]) == "{\"m\":1}");
}

// CR 区切りモードは LF を区切りとみなさない
{
    var asm = new MessageAssembler(FramingMode.CrDelimited);
    var data = Encoding.UTF8.GetBytes("a\nb\r");
    var got = asm.Push(data, data.Length).ToArray();
    Check("CR framing ignores LF", got.Length == 1 && Encoding.UTF8.GetString(got[0]) == "a\nb");
}

// CRLF 区切りは CR を取り除き、LF のみの終端も受け付ける
{
    var asm = new MessageAssembler(FramingMode.CrLfDelimited);
    var data = Encoding.UTF8.GetBytes("crlf\r\nlf\n");
    var got = asm.Push(data, data.Length).ToArray();
    Check("CRLF framing accepts both CRLF and LF",
        got.Length == 2
        && Encoding.UTF8.GetString(got[0]) == "crlf"
        && Encoding.UTF8.GetString(got[1]) == "lf");
}

{
    var asm = new MessageAssembler(FramingMode.CrDelimited);
    var partial = Encoding.UTF8.GetBytes("no terminator yet");
    Check("holds incomplete message", asm.Push(partial, partial.Length).ToArray().Length == 0);
    var flushed = asm.Flush();
    Check("Flush returns the leftover", flushed is not null && flushed.AsSpan().SequenceEqual(partial));
}

// ---------- BLE / Pybricks ----------
{
    // 受信: WRITE_STDOUT(0x01) のみがデータ
    Check("Pybricks decode: stdout payload",
        BleSession.DecodeNotification(new byte[] { 0x01, (byte)'h', (byte)'i' }, true)
            is { } p && p.AsSpan().SequenceEqual("hi"u8));
    Check("Pybricks decode: status report is not data",
        BleSession.DecodeNotification(new byte[] { 0x00, 0x12, 0x34 }, true) is null);
    Check("Pybricks decode: empty stdout ignored",
        BleSession.DecodeNotification(new byte[] { 0x01 }, true) is null);
    Check("Raw decode: passthrough (NUS 経由の旧方式)",
        BleSession.DecodeNotification(new byte[] { 0x01, 0x02 }, false)
            is { } r && r.AsSpan().SequenceEqual(new byte[] { 0x01, 0x02 }));
    Check("decode: null/empty", BleSession.DecodeNotification(null, false) is null
        && BleSession.DecodeNotification(Array.Empty<byte>(), false) is null);

    // 送信: MTU で分割し、各パケットに WRITE_STDIN(0x06) を付ける
    var framed = MessageAssembler.Frame(Encoding.ASCII.GetBytes("ABCDEFGHIJ"), FramingMode.CrLfDelimited); // 12 bytes
    var writes = BleSession.BuildWrites(framed, (byte)PybricksCommand.WriteStdin, 5); // 本体 4 bytes/回
    Check("Pybricks writes: chunk count", writes.Count == 3, string.Join(",", writes.Select(w => w.Length)));
    Check("Pybricks writes: every chunk prefixed 0x06", writes.All(w => w[0] == 0x06));
    Check("Pybricks writes: size within MTU", writes.All(w => w.Length <= 5));
    Check("Pybricks writes: payload reassembles",
        writes.SelectMany(w => w.Skip(1)).ToArray().AsSpan().SequenceEqual(framed));

    var rawWrites = BleSession.BuildWrites(framed, null, 5);
    Check("Raw writes: no prefix, reassembles",
        rawWrites.All(w => w.Length <= 5) && rawWrites.SelectMany(w => w).ToArray().AsSpan().SequenceEqual(framed));

    // 送信 -> 受信の一巡 (Pybricks stdio + CRLF 区切り)
    var asm = new MessageAssembler(FramingMode.CrLfDelimited);
    var received = new List<string>();
    foreach (var w in writes)
    {
        var echoed = new byte[w.Length];
        echoed[0] = (byte)PybricksEvent.WriteStdout;
        Array.Copy(w, 1, echoed, 1, w.Length - 1);

        var payload = BleSession.DecodeNotification(echoed, true)!;
        foreach (var m in asm.Push(payload, payload.Length)) received.Add(Encoding.ASCII.GetString(m));
    }
    Check("Pybricks stdio roundtrip", received.Count == 1 && received[0] == "ABCDEFGHIJ", string.Join("|", received));
}

// ---------- Pybricks プロトコル ----------
{
    Check("UUIDs", PybricksProtocol.Service == new Guid("c5f50001-8280-46da-89f4-6d8051e4aeef")
        && PybricksProtocol.CommandEvent == new Guid("c5f50002-8280-46da-89f4-6d8051e4aeef")
        && PybricksProtocol.HubCapabilities == new Guid("c5f50003-8280-46da-89f4-6d8051e4aeef"));
    Check("短縮UUIDの展開", PybricksProtocol.DeviceInformationService == new Guid("0000180A-0000-1000-8000-00805F9B34FB")
        && PybricksProtocol.SoftwareRevision == new Guid("00002A28-0000-1000-8000-00805F9B34FB")
        && PybricksProtocol.FirmwareRevision == new Guid("00002A26-0000-1000-8000-00805F9B34FB"));

    Check("バージョン解釈", PybricksProtocol.ParseVersion("1.3.0") == new Version(1, 3, 0)
        && PybricksProtocol.ParseVersion("1.2.0-beta.3") == new Version(1, 2, 0)
        && PybricksProtocol.ParseVersion("") is null
        && PybricksProtocol.ParseVersion(null) is null);
    Check("旧方式の判定", PybricksProtocol.ParseVersion("1.2.0")! < PybricksProtocol.StdioVersion
        && !(PybricksProtocol.ParseVersion("1.3.0")! < PybricksProtocol.StdioVersion)
        && !(PybricksProtocol.ParseVersion("1.5.0")! < PybricksProtocol.StdioVersion));

    // ステータスレポート: 0x00 + uint32 LE フラグ (+ v1.4.0 以降はプログラム番号)
    var running = PybricksProtocol.ParseStatusReport(new byte[] { 0x00, 0x40, 0x02, 0x00, 0x00 });
    Check("ステータス: プログラム実行中", running is { UserProgramRunning: true });
    Check("ステータス: ホスト接続済み", running is { BleHostConnected: true }, $"0x{(uint)running!.Flags:X8}");
    Check("ステータス: プログラム番号なし", running.RunningProgram is null);

    var idle = PybricksProtocol.ParseStatusReport(new byte[] { 0x00, 0x00, 0x02, 0x00, 0x00, 0x03 });
    Check("ステータス: 停止中", idle is { UserProgramRunning: false, BleHostConnected: true });
    Check("ステータス: プログラム番号", idle!.RunningProgram == 3);
    Check("ステータス: 説明文", idle.Describe().Contains("UserProgramRunning") == false
        && idle.Describe().Contains("BleHostConnected"), idle.Describe());

    Check("ステータス: stdout は対象外", PybricksProtocol.ParseStatusReport(new byte[] { 0x01, 0x41 }) is null);
    Check("ステータス: 短すぎる入力", PybricksProtocol.ParseStatusReport(new byte[] { 0x00, 0x01 }) is null);

    // Hub Capabilities: uint16 / uint32 / uint32 (+ uint8 スロット数)
    var caps = PybricksProtocol.ParseCapabilities(new byte[]
    {
        0x64, 0x00,                   // max write = 100
        0x01, 0x00, 0x00, 0x00,       // flags
        0x00, 0x40, 0x00, 0x00,       // max program = 16384
    });
    Check("Capabilities: 基本形", caps is { MaxWriteSize: 100, Flags: 1, MaxUserProgramSize: 16384, SlotCount: 0 });

    var capsWithSlots = PybricksProtocol.ParseCapabilities(new byte[]
    {
        0xF4, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x40, 0x00, 0x00, 0x05,
    });
    Check("Capabilities: スロット付き", capsWithSlots is { MaxWriteSize: 500, SlotCount: 5 });
    Check("Capabilities: 短すぎる入力", PybricksProtocol.ParseCapabilities(new byte[] { 0x01 }) is null);

    Check("コマンド組み立て",
        PybricksProtocol.BuildCommand(PybricksCommand.StartUserProgram).AsSpan().SequenceEqual(new byte[] { 0x01 })
        && PybricksProtocol.BuildCommand(PybricksCommand.StartUserProgram, 2).AsSpan().SequenceEqual(new byte[] { 0x01, 0x02 })
        && PybricksProtocol.BuildCommand(PybricksCommand.StopUserProgram).AsSpan().SequenceEqual(new byte[] { 0x00 })
        && PybricksProtocol.BuildCommand(PybricksCommand.StartRepl).AsSpan().SequenceEqual(new byte[] { 0x02 }));

    var legacy = PybricksProtocol.Describe(new Version(1, 2, 0), "3.2.0", null);
    Check("説明文: 旧方式を明示", legacy.Contains("NUS 経由"), legacy);
    var modern = PybricksProtocol.Describe(new Version(1, 5, 0), "3.6.0", caps);
    Check("説明文: 新方式を明示", modern.Contains("Pybricks 特性経由") && modern.Contains("100"), modern);
}

// ---------- プリセット ----------
{
    var pybricks = LinkPreset.All.Single(p => p.Label.Contains("Pybricks"));
    Check("Pybricks preset service uuid", pybricks.Service == new Guid("c5f50001-8280-46da-89f4-6d8051e4aeef"));
    Check("Pybricks preset char uuid",
        pybricks.WriteCharacteristic == new Guid("c5f50002-8280-46da-89f4-6d8051e4aeef")
        && pybricks.NotifyCharacteristic == pybricks.WriteCharacteristic);
    Check("Pybricks preset is BLE + stdio", pybricks.Kind == LinkKind.Ble && pybricks.Profile == BleProfile.PybricksStdio);
    Check("Pybricks preset が既定 + CR区切り",
        pybricks.IsDefault && pybricks.DefaultFraming == FramingMode.CrDelimited && ReferenceEquals(LinkPreset.Default, pybricks));

    var spike = LinkPreset.All.First(p => p.Label.Contains("SPIKE"));
    Check("SPIKE preset is Classic SPP + CR", spike.Kind == LinkKind.Classic
        && spike.Service == new Guid("00001101-0000-1000-8000-00805F9B34FB")
        && spike.DefaultFraming == FramingMode.CrDelimited);

    var nus = LinkPreset.All.Single(p => p.Label.Contains("Nordic"));
    Check("NUS preset uuids", nus.Service == new Guid("6e400001-b5a3-f393-e0a9-e50e24dcca9e")
        && nus.WriteCharacteristic == new Guid("6e400002-b5a3-f393-e0a9-e50e24dcca9e")
        && nus.NotifyCharacteristic == new Guid("6e400003-b5a3-f393-e0a9-e50e24dcca9e"));

    Check("all presets have hints", LinkPreset.All.All(p => !string.IsNullOrWhiteSpace(p.Hint)));
}

// ---------- HEX ----------
Check("FromHex accepts spaced/0x/comma forms",
    HexUtil.FromHex("48 65 6C").AsSpan().SequenceEqual(new byte[] { 0x48, 0x65, 0x6C })
    && HexUtil.FromHex("0x48,0x65").AsSpan().SequenceEqual(new byte[] { 0x48, 0x65 })
    && HexUtil.FromHex("48-65").AsSpan().SequenceEqual(new byte[] { 0x48, 0x65 }));

try { HexUtil.FromHex("4"); Check("FromHex rejects odd length", false); }
catch (FormatException) { Check("FromHex rejects odd length", true); }

try { HexUtil.FromHex("4G"); Check("FromHex rejects non-hex", false); }
catch (FormatException) { Check("FromHex rejects non-hex", true); }

Check("ToHex truncates with ellipsis", HexUtil.ToHex(new byte[] { 1, 2, 3, 4 }, 2) == "01 02 ...");

// ---------- ファイル書き出し ----------
{
    var dir = Path.Combine(Path.GetTempPath(), "btlink_test_" + Guid.NewGuid().ToString("N")[..8]);
    var options = new DataLoggerOptions { Directory = dir, TextEncoding = Encoding.UTF8 };
    using (var logger = new DataLogger(options))
    {
        var path = logger.Open("My Device (test)");
        logger.LogEvent(TransferDirection.Info, "接続しました");
        logger.LogData(TransferDirection.Tx, Encoding.UTF8.GetBytes("PING"));
        logger.LogData(TransferDirection.Rx, new byte[] { 0x50, 0x4F, 0x4E, 0x47 });
        logger.Close();

        var text = File.ReadAllText(path);
        Check("text log contains TX row", text.Contains("\tTX\t4\t50 49 4E 47\tPING"));
        Check("text log contains RX row", text.Contains("\tRX\t4\t50 4F 4E 47\tPONG"));
        Check("text log contains event", text.Contains("接続しました"));
        Check("filename sanitized", Path.GetFileName(path).Contains("My_Device_(test)"), Path.GetFileName(path));

        var baseName = path[..^4];
        Check("tx.bin written", File.ReadAllBytes(baseName + "_tx.bin").AsSpan().SequenceEqual("PING"u8));
        Check("rx.bin written", File.ReadAllBytes(baseName + "_rx.bin").AsSpan().SequenceEqual("PONG"u8));
    }

    using (var logger = new DataLogger(options))
    {
        var path = logger.Open("concurrent");
        Parallel.For(0, 200, i => logger.LogData(
            i % 2 == 0 ? TransferDirection.Tx : TransferDirection.Rx,
            Encoding.UTF8.GetBytes($"msg{i:D3}")));
        logger.Close();
        var lines = File.ReadAllLines(path).Where(l => l.Contains("\tTX\t") || l.Contains("\tRX\t")).ToArray();
        Check("concurrent writes: 200 rows", lines.Length == 200, lines.Length.ToString());
        Check("concurrent writes: all well-formed", lines.All(l => l.Split('\t').Length == 5));
    }

    // 欠落件数は閉じたあとも参照できる (セッション終了後に 0 へ戻らない)
    {
        using var logger = new DataLogger(new DataLoggerOptions { Directory = dir });
        logger.Open("counters");
        logger.LogData(TransferDirection.Rx, "x"u8.ToArray());
        logger.Close();

        Check("正常時は書き出しエラー 0",
            logger.DroppedCount == 0 && logger.WriteErrorCount == 0 && logger.LostCount == 0);
        Check("正常時はエラー内容なし", logger.LastWriteError is null);
    }

    // 文字コードは「開いた時点」の設定で固定される (途中で差し替えられても揺れない)
    {
        var encodingOptions = new DataLoggerOptions { Directory = dir, TextEncoding = Encoding.UTF8 };
        var payload = Encoding.GetEncoding("shift_jis").GetBytes("日本語");

        string path;
        using (var logger = new DataLogger(encodingOptions))
        {
            path = logger.Open("encoding");
            logger.LogData(TransferDirection.Rx, payload);

            // 途中で設定を差し替える
            encodingOptions.TextEncoding = Encoding.GetEncoding("shift_jis");
            logger.LogData(TransferDirection.Rx, payload);
            logger.Close();
        }

        var text = File.ReadAllText(path);
        Check("ヘッダの文字コードは開いた時点のもの", text.Contains("文字コード(表示用): utf-8"));

        var rows = File.ReadAllLines(path)
            .Where(l => l.Contains("\tRX\t"))
            .Select(l => l.Split('\t')[4])
            .ToArray();
        Check("途中で変更しても解釈は変わらない", rows.Length == 2 && rows[0] == rows[1],
            rows.Length == 2 ? $"'{rows[0]}' vs '{rows[1]}'" : $"{rows.Length} 行");
    }

    Directory.Delete(dir, true);
}

// ---------- 送信失敗の通知 ----------
{
    var inner = new IOException("接続が切れました");
    var failed = new SendFailedException([0x41, 0x42, 0x43], inner);
    Check("SendFailedException が送出済みバイトを持つ",
        failed.SentData.AsSpan().SequenceEqual("ABC"u8) && ReferenceEquals(failed.InnerException, inner));
    Check("SendFailedException のメッセージは原因を引き継ぐ", failed.Message == "接続が切れました");

    var unknown = new SendFailedException([], inner);
    Check("送出量が不明な場合は空", unknown.SentData.Length == 0);
}

Console.WriteLine(failures == 0 ? "\nALL PASS" : $"\n{failures} FAILED");
return failures;
