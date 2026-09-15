using System.Diagnostics;
using System.Reflection;
using System.Text;
using BluetoothLink;
using BluetoothLink.Core;

internal static class ScanTest
{
    private static int _failures;

    [STAThread]
    private static int Main()
    {
        Console.OutputEncoding = Encoding.UTF8;
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        ApplicationConfiguration.Initialize();

        using var form = new MainForm();
        form.Show();
        Pump(form, TimeSpan.FromSeconds(1));

        var btnDiscover = Get<Button>(form, "btnDiscover");

        // 起動時の読み込みが走っている間はボタンが無効なことがあるので、使えるまで待つ。
        var ready = Stopwatch.StartNew();
        while (!btnDiscover.Enabled && ready.Elapsed < TimeSpan.FromSeconds(20))
        {
            Application.DoEvents();
            Thread.Sleep(20);
        }

        Check("起動後すぐ検索ボタンが使える (5 秒以内)", ready.Elapsed < TimeSpan.FromSeconds(5),
            $"{ready.Elapsed.TotalSeconds:0.00} 秒");
        var lvDevices = Get<ListView>(form, "lvDevices");
        var lblStatus = Get<Label>(form, "lblStatus");
        var preset = Get<ComboBox>(form, "cmbPreset");

        Check("既定プリセットが Pybricks", preset.Text == "BLE: Pybricks (stdio)", preset.Text);
        Check("既定の区切りが CR", Get<ComboBox>(form, "cmbFraming").Text.Contains("CR区切り"),
            Get<ComboBox>(form, "cmbFraming").Text);
        Check("検索ボタンの初期表示", btnDiscover.Text == "デバイス検索", btnDiscover.Text);

        // 検索開始
        var sw = Stopwatch.StartNew();
        btnDiscover.PerformClick();
        Pump(form, TimeSpan.FromMilliseconds(200));
        Check("押すと「検索停止」に変わる", btnDiscover.Text == "検索停止", btnDiscover.Text);

        // 最初の 1 件までの時間を測る
        double firstSeen = -1;
        while (sw.Elapsed < TimeSpan.FromSeconds(12))
        {
            Application.DoEvents();
            Thread.Sleep(10);
            if (firstSeen < 0 && lvDevices.Items.Count > 0)
            {
                firstSeen = sw.Elapsed.TotalSeconds;
            }
        }

        Console.WriteLine();
        Console.WriteLine($"最初の 1 件まで : {firstSeen:0.00} 秒");
        Console.WriteLine($"12 秒で検出     : {lvDevices.Items.Count} 台");
        Console.WriteLine($"ステータス      : {lblStatus.Text}");
        foreach (ListViewItem item in lvDevices.Items.Cast<ListViewItem>().Take(6))
        {
            Console.WriteLine($"  {item.SubItems[0].Text,-22} {item.SubItems[1].Text,-18} {item.SubItems[5].Text}");
        }

        Console.WriteLine();
        Check("2 秒以内に最初の 1 件が出る", firstSeen >= 0 && firstSeen < 2.0, $"{firstSeen:0.00} 秒");
        Check("検索中も UI が使える (ボタンが有効)", btnDiscover.Enabled && Get<Button>(form, "btnPaired").Enabled);
        Check("検索中のステータス表示", lblStatus.Text.Contains("スキャン中"), lblStatus.Text);
        Check("行に電波強度が出る",
            lvDevices.Items.Count > 0 && lvDevices.Items[0].SubItems[5].Text.Contains("dBm"),
            lvDevices.Items.Count > 0 ? lvDevices.Items[0].SubItems[5].Text : "(なし)");

        // 停止
        btnDiscover.PerformClick();
        Pump(form, TimeSpan.FromMilliseconds(300));
        Check("停止すると表示が戻る", btnDiscover.Text == "デバイス検索", btnDiscover.Text);

        var scanner = (BleScanner?)form.GetType().GetField("_bleScanner", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(form);
        Check("スキャナーが停止している", scanner is { IsScanning: false });

        form.Close();
        Pump(form, TimeSpan.FromMilliseconds(300));

        Console.WriteLine(_failures == 0 ? "\nALL PASS" : $"\n{_failures} FAILED");
        return _failures;
    }

    private static T Get<T>(Form form, string name) where T : Control =>
        (T)typeof(MainForm).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(form)!;

    private static void Pump(Form form, TimeSpan d)
    {
        var sw = Stopwatch.StartNew();
        do { Application.DoEvents(); Thread.Sleep(10); } while (sw.Elapsed < d);
    }

    private static void Check(string name, bool ok, string? detail = null)
    {
        Console.WriteLine($"{(ok ? "PASS" : "FAIL")}  {name}{(detail is null ? "" : "  -> " + detail)}");
        if (!ok) _failures++;
    }
}
