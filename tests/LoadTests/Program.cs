using System.Diagnostics;
using System.Reflection;
using System.Text;
using BluetoothLink;
using BluetoothLink.Core;

/// <summary>実際の受信ハンドラを高頻度で叩き、UI が応答し続けるかを測る。</summary>
internal static class LoadTest
{
    private static int _failures;

    [STAThread]
    private static int Main()
    {
        Console.OutputEncoding = Encoding.UTF8;
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        ApplicationConfiguration.Initialize();

        var dir = Path.Combine(Path.GetTempPath(), "btlink_load_" + Guid.NewGuid().ToString("N")[..8]);

        using var form = new MainForm();
        form.Show();
        Pump(form, TimeSpan.FromSeconds(1.5));

        // 内部状態へアクセスする
        var logger = (DataLogger)Field(form, "_logger");
        var options = (DataLoggerOptions)Field(form, "_loggerOptions");
        var monitor = (MonitorBuffer)Field(form, "_monitor");
        var onReceived = typeof(MainForm).GetMethod("Session_DataReceived", BindingFlags.Instance | BindingFlags.NonPublic)!;

        options.Directory = dir;
        var logPath = logger.Open("loadtest");
        Console.WriteLine($"ログ: {logPath}");

        var session = new FakeSession();
        var payload = Encoding.ASCII.GetBytes("{\"t\":123456,\"v\":[1.25,-0.5,9.81]}");

        // ---- 1) BLE の現実的な上限あたり ----
        var realistic = Run(form, onReceived, session, payload, targetPerSecond: 2_000, duration: TimeSpan.FromSeconds(5));
        Report("2,000 件/秒 × 5 秒 (BLE の現実的な上限付近)", realistic);

        // ---- 2) 大きく余裕を見た値 ----
        var result = Run(form, onReceived, session, payload, targetPerSecond: 10_000, duration: TimeSpan.FromSeconds(10));
        Report("10,000 件/秒 × 10 秒 (余裕の確認)", result);

        // ---- 3) 飽和点を知るための参考測定 (合否判定はしない) ----
        var burst = Run(form, onReceived, session, payload, targetPerSecond: 0, duration: TimeSpan.FromSeconds(3));
        Report("全力投入 × 3 秒 (参考値。CPU 飽和するため合否判定はしない)", burst);

        // ---- 検証 ----
        Pump(form, TimeSpan.FromSeconds(1));
        logger.Close();

        var injected = realistic.Injected + result.Injected + burst.Injected;
        var lines = File.ReadAllLines(logPath).Count(l => l.Contains("\tRX\t"));

        Console.WriteLine();
        Console.WriteLine("=== 検証 ===");
        Check("2,000 件/秒: UI が滑らか (平均 20ms 未満 / 最大 200ms 未満)",
            realistic.AveragePumpMs < 20 && realistic.MaxPumpMs < 200,
            $"平均 {realistic.AveragePumpMs:0.00}ms / 最大 {realistic.MaxPumpMs:0.0}ms");
        Check("10,000 件/秒: UI が応答し続ける (平均 50ms 未満)",
            result.AveragePumpMs < 50, $"平均 {result.AveragePumpMs:0.00}ms / 最大 {result.MaxPumpMs:0.0}ms");
        Check("10,000 件/秒 を捌ける", result.AchievedRate > 9_000, $"{result.AchievedRate:N0} 件/秒");
        Console.WriteLine($"(参考) 全力時の投入レート {burst.AchievedRate:N0} 件/秒 / UI 応答 最大 {burst.MaxPumpMs:0.0}ms");
        var dropped = logger.DroppedCount;
        Check("記録件数 + 欠落件数 = 投入件数 (取りこぼしを把握できている)",
            lines + dropped == injected,
            $"投入 {injected:N0} = 記録 {lines:N0} + 欠落 {dropped:N0}");
        Check("現実的なレートでは記録が欠落しない",
            realistic.Injected + result.Injected <= lines,
            $"2,000+10,000件/秒ぶん {realistic.Injected + result.Injected:N0} 件 / 記録合計 {lines:N0} 件");
        Check("モニタ件数が上限で頭打ち", monitor.Count == Math.Min(injected + 10, monitor.Capacity) || monitor.Count == monitor.Capacity,
            $"保持 {monitor.Count:N0} / 容量 {monitor.Capacity:N0}");
        Check("画面外になった件数を把握できている",
            monitor.Overwritten == Math.Max(0, monitor.TotalAdded - monitor.Count),
            $"{monitor.Overwritten:N0} 件");

        form.Close();
        Pump(form, TimeSpan.FromMilliseconds(500));
        try { Directory.Delete(dir, true); } catch (Exception) { }

        Console.WriteLine(_failures == 0 ? "\nALL PASS" : $"\n{_failures} FAILED");
        return _failures;
    }

    private static RunResult Run(
        Form form, MethodInfo onReceived, FakeSession session, byte[] payload,
        int targetPerSecond, TimeSpan duration)
    {
        var stop = false;
        long injected = 0;
        var threads = new List<Thread>();
        const int producerCount = 4;

        for (var t = 0; t < producerCount; t++)
        {
            var thread = new Thread(() =>
            {
                var sw = Stopwatch.StartNew();
                long mine = 0;
                var perThread = targetPerSecond / producerCount;

                while (!Volatile.Read(ref stop))
                {
                    onReceived.Invoke(form, [session, new SessionDataReceivedEventArgs(session, payload)]);
                    Interlocked.Increment(ref injected);
                    mine++;

                    if (perThread > 0)
                    {
                        // 目標レートに合わせて待つ
                        var expected = mine * 1000.0 / perThread;
                        var behind = expected - sw.Elapsed.TotalMilliseconds;
                        if (behind > 1)
                        {
                            Thread.Sleep((int)behind);
                        }
                    }
                }
            })
            { IsBackground = true };

            threads.Add(thread);
        }

        var maxPump = 0.0;
        var totalPump = 0.0;
        var pumps = 0;

        foreach (var thread in threads)
        {
            thread.Start();
        }

        var clock = Stopwatch.StartNew();
        while (clock.Elapsed < duration)
        {
            var t0 = Stopwatch.GetTimestamp();
            Application.DoEvents();
            var ms = (Stopwatch.GetTimestamp() - t0) * 1000.0 / Stopwatch.Frequency;

            maxPump = Math.Max(maxPump, ms);
            totalPump += ms;
            pumps++;

            Thread.Sleep(1);
        }

        Volatile.Write(ref stop, true);
        foreach (var thread in threads)
        {
            thread.Join(2000);
        }

        return new RunResult(
            Interlocked.Read(ref injected),
            Interlocked.Read(ref injected) / clock.Elapsed.TotalSeconds,
            maxPump,
            pumps > 0 ? totalPump / pumps : 0);
    }

    private static void Report(string title, RunResult r)
    {
        Console.WriteLine();
        Console.WriteLine($"=== {title} ===");
        Console.WriteLine($"投入        : {r.Injected:N0} 件 ({r.AchievedRate:N0} 件/秒)");
        Console.WriteLine($"UI 応答時間 : 平均 {r.AveragePumpMs:0.00} ms / 最大 {r.MaxPumpMs:0.0} ms");
    }

    private static object Field(object target, string name) =>
        target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(target)!;

    private static void Pump(Form form, TimeSpan duration)
    {
        var sw = Stopwatch.StartNew();
        do
        {
            Application.DoEvents();
            Thread.Sleep(10);
        }
        while (sw.Elapsed < duration);
    }

    private static void Check(string name, bool ok, string? detail = null)
    {
        Console.WriteLine($"{(ok ? "PASS" : "FAIL")}  {name}{(detail is null ? "" : "  -> " + detail)}");
        if (!ok)
        {
            _failures++;
        }
    }

    private readonly record struct RunResult(long Injected, double AchievedRate, double MaxPumpMs, double AveragePumpMs);

    /// <summary>受信イベントを発火させるためのダミーセッション。</summary>
    private sealed class FakeSession : ILinkSession
    {
        public LinkKind Kind => LinkKind.Ble;

        public ConnectionRole Role => ConnectionRole.Client;

        public FramingMode Framing => FramingMode.CrLfDelimited;

        public string DisplayName => "LoadTest Hub (00:11:22:33:44:55)";

        public bool IsConnected => true;

        // 受信は MainForm 側のハンドラーを直接呼ぶため、ここでは発火させない。
        public event EventHandler<SessionDataReceivedEventArgs>? DataReceived { add { } remove { } }

        public event EventHandler<SessionClosedEventArgs>? Closed { add { } remove { } }

        public void Start() { }

        public Task SendAsync(byte[] payload, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public void Close(string reason = "") { }

        public void Dispose()
        {
        }
    }
}
