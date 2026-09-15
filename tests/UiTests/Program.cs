using System.Diagnostics;
using System.Reflection;
using System.Text;
using BluetoothLink;

internal static class UiStateTest
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
        Pump(TimeSpan.FromSeconds(1));

        // 起動時の paired 取得 (非同期) が終わるまで待つ
        var sw = Stopwatch.StartNew();
        while (!Get<Button>(form, "btnConnect").Enabled && sw.Elapsed < TimeSpan.FromSeconds(40))
        {
            Pump(TimeSpan.FromMilliseconds(200));
        }

        var preset = Get<ComboBox>(form, "cmbPreset");
        Check("プリセットが 6 件", preset.Items.Count == 6, preset.Items.Count.ToString());

        // ---- 起動時の既定 ----
        Check("既定プリセットが Pybricks", preset.Text == "BLE: Pybricks (stdio)", preset.Text);
        Check("既定でクライアント固定", Get<RadioButton>(form, "rdoClient").Checked);
        Check("既定の区切りが CR", Get<ComboBox>(form, "cmbFraming").Text.Contains("CR区切り"),
            Get<ComboBox>(form, "cmbFraming").Text);

        // ---- Classic: SPIKE Hub / SPP ----
        Select(form, preset, 0);
        Check("[0] ラベル", preset.Text.StartsWith("Classic: SPIKE"), preset.Text);
        Check("[0] サービスUUID が SPP",
            Get<TextBox>(form, "txtService").Text.Equals("00001101-0000-1000-8000-00805F9B34FB", StringComparison.OrdinalIgnoreCase),
            Get<TextBox>(form, "txtService").Text);
        Check("[0] 区切りが CR", Get<ComboBox>(form, "cmbFraming").Text.Contains("CR区切り"), Get<ComboBox>(form, "cmbFraming").Text);
        Check("[0] UUID は読み取り専用", Get<TextBox>(form, "txtService").ReadOnly);
        Check("[0] BLE 特性欄は無効", !Get<TextBox>(form, "txtWriteChar").Enabled && !Get<TextBox>(form, "txtNotifyChar").Enabled);
        Check("[0] サーバー/クライアントを選べる", Get<RadioButton>(form, "rdoServer").Enabled && Get<RadioButton>(form, "rdoClient").Enabled);
        Check("[0] ヒントが出ている", Get<Label>(form, "lblHint").Text.Length > 0);

        // サーバーを選ぶとサービス名が編集可能になる
        Get<RadioButton>(form, "rdoServer").Checked = true;
        Pump(TimeSpan.FromMilliseconds(100));
        Check("[0] サーバー時はサービス名が有効", Get<TextBox>(form, "txtServiceName").Enabled);
        Check("[0] サーバー時のボタン名", Get<Button>(form, "btnConnect").Text == "待ち受け開始", Get<Button>(form, "btnConnect").Text);

        // ---- Classic: カスタム ----
        Select(form, preset, 2);
        Check("[2] UUID を編集できる", !Get<TextBox>(form, "txtService").ReadOnly);

        // ---- BLE: Pybricks ----
        Select(form, preset, 3);
        Check("[3] ラベル", preset.Text.Contains("Pybricks"), preset.Text);
        Check("[3] サービス UUID",
            Get<TextBox>(form, "txtService").Text.Equals("C5F50001-8280-46DA-89F4-6D8051E4AEEF", StringComparison.OrdinalIgnoreCase),
            Get<TextBox>(form, "txtService").Text);
        Check("[3] 書込/通知特性が同じ Command/Event",
            Get<TextBox>(form, "txtWriteChar").Text.Equals("C5F50002-8280-46DA-89F4-6D8051E4AEEF", StringComparison.OrdinalIgnoreCase)
            && Get<TextBox>(form, "txtNotifyChar").Text == Get<TextBox>(form, "txtWriteChar").Text,
            Get<TextBox>(form, "txtWriteChar").Text);
        Check("[3] BLE 特性欄が有効", Get<TextBox>(form, "txtWriteChar").Enabled && Get<TextBox>(form, "txtNotifyChar").Enabled);
        Check("[3] クライアント固定", Get<RadioButton>(form, "rdoClient").Checked
            && !Get<RadioButton>(form, "rdoServer").Enabled && !Get<RadioButton>(form, "rdoClient").Enabled);
        Check("[3] サービス名は無効", !Get<TextBox>(form, "txtServiceName").Enabled);
        Check("[3] ボタン名が「接続」", Get<Button>(form, "btnConnect").Text == "接続", Get<Button>(form, "btnConnect").Text);
        Check("[3] 区切りが CR", Get<ComboBox>(form, "cmbFraming").Text.Contains("CR区切り"), Get<ComboBox>(form, "cmbFraming").Text);

        // ---- BLE: Nordic UART ----
        Select(form, preset, 4);
        Check("[4] 書込と通知が別 UUID",
            !Get<TextBox>(form, "txtWriteChar").Text.Equals(Get<TextBox>(form, "txtNotifyChar").Text, StringComparison.OrdinalIgnoreCase));

        // ---- BLE: カスタム ----
        Select(form, preset, 5);
        Check("[5] 特性 UUID を編集できる",
            !Get<TextBox>(form, "txtWriteChar").ReadOnly && !Get<TextBox>(form, "txtNotifyChar").ReadOnly);

        // ---- Pybricks 操作ボタン ----
        Check("未接続では Pybricks 操作が無効",
            !Get<Button>(form, "btnPyStart").Enabled
            && !Get<Button>(form, "btnPyStop").Enabled
            && !Get<Button>(form, "btnPyRepl").Enabled);
        Check("Pybricks 状態ラベルは空", Get<Label>(form, "lblPyStatus").Text.Length == 0);

        // ---- 接続していない状態では送信できない ----
        Check("未接続では送信ボタンが無効", !Get<Button>(form, "btnSend").Enabled);
        Check("未接続では切断ボタンが無効", !Get<Button>(form, "btnDisconnect").Enabled);
        Check("ステータスが未接続", Get<Label>(form, "lblStatus").Text == "未接続", Get<Label>(form, "lblStatus").Text);

        // ---- モニタ (仮想モード ListView) ----
        var monitorView = Get<ListView>(form, "lvMonitor");
        Check("モニタは仮想モード", monitorView.VirtualMode);
        Check("モニタの列が 6 つ", monitorView.Columns.Count == 6, monitorView.Columns.Count.ToString());
        Check("統計ラベルが出ている", Get<Label>(form, "lblStats").Text.Contains("受信"), Get<Label>(form, "lblStats").Text);

        // ---- レイアウトが画面内に収まっている ----
        foreach (var name in new[] { "grpDevice", "grpConnect", "grpIo" })
        {
            var box = Get<GroupBox>(form, name);
            Check($"{name} がフォーム内に収まる",
                box.Bottom <= form.ClientSize.Height && box.Right <= form.ClientSize.Width,
                $"bottom={box.Bottom} right={box.Right} client={form.ClientSize}");

            foreach (Control child in box.Controls)
            {
                Check($"{name}/{child.Name} が親内に収まる",
                    child.Bottom <= box.ClientSize.Height && child.Right <= box.ClientSize.Width && child.Top >= 0 && child.Left >= 0,
                    $"{child.Bounds} in {box.ClientSize}");
            }
        }

        // ---- リサイズしても破綻しない ----
        form.Size = new Size(1400, 1000);
        Pump(TimeSpan.FromMilliseconds(200));
        var io = Get<GroupBox>(form, "grpIo");
        Check("拡大後も grpIo がフォーム内", io.Bottom <= form.ClientSize.Height && io.Right <= form.ClientSize.Width,
            $"bottom={io.Bottom} right={io.Right} client={form.ClientSize}");
        Check("拡大後にモニタ欄が広がる", Get<ListView>(form, "lvMonitor").Height > 210,
            Get<ListView>(form, "lvMonitor").Height.ToString());

        form.Close();
        Pump(TimeSpan.FromMilliseconds(300));

        Console.WriteLine(_failures == 0 ? "\nALL PASS" : $"\n{_failures} FAILED");
        return _failures;
    }

    private static void Select(Form form, ComboBox combo, int index)
    {
        combo.SelectedIndex = index;
        Pump(TimeSpan.FromMilliseconds(150));
    }

    private static T Get<T>(Form form, string name) where T : Control
    {
        var field = typeof(MainForm).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"コントロール {name} が見つかりません。");
        return (T)field.GetValue(form)!;
    }

    private static void Pump(TimeSpan duration)
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
}
