using System.Diagnostics;
using System.Reflection;
using System.Text;
using BluetoothLink.Core;

namespace BluetoothLink;

public partial class MainForm : Form
{
    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(20);

    /// <summary>画面に保持するモニタ行数。これを超えた分もファイルには残る。</summary>
    private const int MonitorCapacity = 200_000;

    /// <summary>画面の更新周期。受信レートとは切り離す。</summary>
    private const int UiRefreshIntervalMs = 100;

    private readonly DataLoggerOptions _loggerOptions = new();
    private readonly DataLogger _logger;
    private readonly RfcommServerHost _server = new();
    private readonly List<ILinkSession> _sessions = new();

    // 通信スレッドはここへ積むだけ。UI へは触れない。
    private readonly MonitorBuffer _monitor = new(MonitorCapacity);
    private readonly TransferStats _stats = new();
    private readonly System.Windows.Forms.Timer _uiTimer = new();

    private BleScanner? _bleScanner;
    private long _renderedScanVersion = -1;
    private bool _renderedScanning;
    private bool _reportedWriteError;
    private LinkKind _listedKind = LinkKind.Classic;
    private string _lastStatusText = string.Empty;
    private PybricksStatusReport? _latestStatus;
    private long _renderedTotal = -1;
    private bool _busy;
    private bool _closing;

    public MainForm()
    {
        InitializeComponent();

        _logger = new DataLogger(_loggerOptions);
        _server.ClientConnected += Server_ClientConnected;
        _server.ListenerStopped += Server_ListenerStopped;

        _uiTimer.Interval = UiRefreshIntervalMs;
        _uiTimer.Tick += UiTimer_Tick;
    }

    /// <summary>いま選ばれているプリセット。</summary>
    private LinkPreset CurrentPreset => (LinkPreset)cmbPreset.SelectedItem!;

    /// <summary>csproj の Version から取り出した製品バージョン。</summary>
    private static string AppVersion
    {
        get
        {
            var informational = System.Reflection.Assembly.GetExecutingAssembly()
                .GetCustomAttribute<System.Reflection.AssemblyInformationalVersionAttribute>()?.InformationalVersion;

            if (!string.IsNullOrWhiteSpace(informational))
            {
                // "1.0.0+<コミットハッシュ>" の形になることがあるので手前だけ使う。
                return informational.Split('+')[0];
            }

            var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            return version is null ? "1.0.0" : $"{version.Major}.{version.Minor}.{version.Build}";
        }
    }

    // ------------------------------------------------------------------
    // 初期化 / 終了
    // ------------------------------------------------------------------

    private void MainForm_Load(object? sender, EventArgs e)
    {
        foreach (var preset in LinkPreset.All)
        {
            cmbPreset.Items.Add(preset);
        }

        cmbFraming.Items.AddRange(new object[]
        {
            new ComboItem<FramingMode>("区切りなし (raw)", FramingMode.Raw),
            new ComboItem<FramingMode>("改行区切り (CRLF)", FramingMode.CrLfDelimited),
            new ComboItem<FramingMode>("CR区切り (0x0D)", FramingMode.CrDelimited),
            new ComboItem<FramingMode>("長さ付き (4byte BE)", FramingMode.LengthPrefixed),
        });

        cmbFormat.Items.AddRange(new object[]
        {
            new ComboItem<PayloadFormat>("テキスト", PayloadFormat.Text),
            new ComboItem<PayloadFormat>("16進 (HEX)", PayloadFormat.Hex),
        });
        cmbFormat.SelectedIndex = 0;

        foreach (var item in BuildEncodingItems())
        {
            cmbEncoding.Items.Add(item);
        }

        cmbEncoding.SelectedIndex = 0;

        // プリセットを選ぶと UUID・区切りなどが埋まる。
        cmbPreset.SelectedItem = LinkPreset.Default;

        Text = $"BluetoothLink {AppVersion} - Bluetooth データ送受信ツール";
        lblRadio.Text = DeviceService.DescribeRadio();
        lblLogPath.Text = $"出力先: {_loggerOptions.Directory}";

        if (!DeviceService.IsRadioAvailable())
        {
            AppendLog(TransferDirection.Error, "Bluetooth アダプターが見つかりません。アダプターの接続と有効化を確認してください。");
        }
        else
        {
            AppendLog(TransferDirection.Info, "準備完了。プリセットを選び、「ペアリング済みを表示」または「デバイス検索」を実行してください。");
            _ = LoadPairedDevicesAsync(quiet: true);
        }

        UpdateUiState();
        _uiTimer.Start();
    }

    private void MainForm_FormClosing(object? sender, FormClosingEventArgs e)
    {
        _closing = true;
        _uiTimer.Stop();
        _uiTimer.Dispose();
        _bleScanner?.Dispose();
        _server.Stop();

        foreach (var session in _sessions.ToArray())
        {
            session.Close("アプリ終了により切断しました。");
            session.Dispose();
        }

        _sessions.Clear();
        _logger.Dispose();
    }

    // ------------------------------------------------------------------
    // 1. デバイス検索 / ペアリング
    // ------------------------------------------------------------------

    private async void btnDiscover_Click(object? sender, EventArgs e)
    {
        if (CurrentPreset.Kind == LinkKind.Ble)
        {
            // BLE はアドバタイズを受け続ける方式。待たされないので busy にしない。
            ToggleBleScan();
            return;
        }

        if (_busy)
        {
            return;
        }

        // Classic の問い合わせは Windows 側で約 30 秒かかる。
        // 待っている間に一覧が空にならないよう、まずペアリング済みを出しておく。
        try
        {
            FillDeviceList(await DeviceService.GetPairedAsync(), LinkKind.Classic);
        }
        catch (Exception)
        {
            // 取得できなくても検索は続行する。
        }

        SetBusy(true, "Classic デバイスを検索しています... (Windows の仕様で約 30 秒かかります)");
        AppendLog(TransferDirection.Info, "Classic の検索を開始しました。完了まで約 30 秒かかります。");

        try
        {
            var devices = await DeviceService.DiscoverAsync();
            FillDeviceList(devices, LinkKind.Classic);
            AppendLog(TransferDirection.Info, $"検索完了: {devices.Count} 台見つかりました。");
        }
        catch (Exception ex)
        {
            ShowError("デバイス検索に失敗しました。", ex);
        }
        finally
        {
            SetBusy(false);
        }
    }

    /// <summary>BLE スキャンの開始 / 停止。結果は画面更新タイマーが反映する。</summary>
    private void ToggleBleScan()
    {
        _bleScanner ??= new BleScanner();

        try
        {
            if (_bleScanner.IsScanning)
            {
                _bleScanner.Stop();
                AppendLog(TransferDirection.Info, $"BLE スキャンを停止しました ({_bleScanner.Count} 台)。");
            }
            else
            {
                _renderedScanVersion = -1;
                _bleScanner.Start();
                AppendLog(TransferDirection.Info, "BLE スキャンを開始しました。見つかった順に一覧へ追加されます。");
            }
        }
        catch (Exception ex)
        {
            ShowError("BLE スキャンを開始できませんでした。", ex);
        }

        UpdateUiState();
    }

    private void StopBleScan()
    {
        if (_bleScanner is { IsScanning: true })
        {
            _bleScanner.Stop();
            UpdateUiState();
        }
    }

    private async void btnPaired_Click(object? sender, EventArgs e)
    {
        await LoadPairedDevicesAsync(quiet: false);
    }

    /// <summary>
    /// ペアリング済みデバイスを一覧へ読み込む。
    /// </summary>
    /// <param name="quiet">
    /// 起動時の自動読み込み。ボタンを塞ぐと起動直後に操作できなくなるため、
    /// この場合は busy 状態にせず、失敗しても黙って諦める。
    /// </param>
    private async Task LoadPairedDevicesAsync(bool quiet)
    {
        if (_busy)
        {
            return;
        }

        var kind = CurrentPreset.Kind;
        if (!quiet)
        {
            SetBusy(true, "ペアリング済みデバイスを取得しています...");
        }

        try
        {
            var devices = kind == LinkKind.Classic
                ? await DeviceService.GetPairedAsync()
                : await BleService.GetPairedAsync();

            // 起動時の読み込みが遅れている間に検索が始まっていたら、その結果を上書きしない。
            if (quiet && (_bleScanner is { IsScanning: true } || _busy))
            {
                return;
            }

            FillDeviceList(devices, kind);
            if (!quiet)
            {
                AppendLog(TransferDirection.Info, $"ペアリング済み: {devices.Count} 台。");
            }
        }
        catch (Exception ex)
        {
            if (!quiet)
            {
                ShowError("ペアリング済みデバイスの取得に失敗しました。", ex);
            }
        }
        finally
        {
            if (!quiet)
            {
                SetBusy(false);
            }
        }
    }

    private async void btnPair_Click(object? sender, EventArgs e)
    {
        var entry = GetSelectedDevice();
        if (entry is null || _busy)
        {
            return;
        }

        var pin = txtPin.Text.Trim();
        SetBusy(true, $"{entry.Name} とペアリングしています...");
        try
        {
            bool ok;
            if (entry.Kind == LinkKind.Classic)
            {
                if (entry.HardwareAddress is null)
                {
                    ShowWarning("このデバイスのアドレスを取得できないため、ペアリングできません。");
                    return;
                }

                ok = await DeviceService.PairAsync(entry.HardwareAddress, pin);
            }
            else
            {
                var device = await entry.ResolveBleDeviceAsync();
                if (device is null)
                {
                    ShowWarning("この BLE デバイスを取得できませんでした。電波が届いているか確認してください。");
                    return;
                }

                ok = await BleService.PairAsync(device, pin);
            }

            entry.Refresh();
            UpdateRow(entry);

            if (ok)
            {
                AppendLog(TransferDirection.Info, $"ペアリングに成功しました: {Describe(entry)}");
            }
            else
            {
                AppendLog(
                    TransferDirection.Error,
                    $"ペアリングに失敗しました: {Describe(entry)} (PIN の誤り、相手側がペアリングモードでない、などが考えられます)");
            }
        }
        catch (Exception ex)
        {
            ShowError("ペアリング処理でエラーが発生しました。", ex);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async void btnUnpair_Click(object? sender, EventArgs e)
    {
        var entry = GetSelectedDevice();
        if (entry is null || _busy)
        {
            return;
        }

        if (entry.HardwareAddress is null)
        {
            ShowWarning("このデバイスのアドレスを取得できません。Windows の設定から解除してください。");
            return;
        }

        var answer = MessageBox.Show(
            this,
            $"{Describe(entry)} のペアリングを解除します。よろしいですか?",
            "ペアリング解除",
            MessageBoxButtons.OKCancel,
            MessageBoxIcon.Warning);

        if (answer != DialogResult.OK)
        {
            return;
        }

        SetBusy(true, "ペアリングを解除しています...");
        try
        {
            var ok = await DeviceService.UnpairAsync(entry.HardwareAddress);
            entry.Refresh();
            UpdateRow(entry);
            AppendLog(
                ok ? TransferDirection.Info : TransferDirection.Error,
                ok ? $"ペアリングを解除しました: {Describe(entry)}" : $"ペアリング解除に失敗しました: {Describe(entry)}");
        }
        catch (Exception ex)
        {
            ShowError("ペアリング解除でエラーが発生しました。", ex);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void FillDeviceList(IEnumerable<DeviceEntry> devices, LinkKind kind)
    {
        _listedKind = kind;

        lvDevices.BeginUpdate();
        try
        {
            lvDevices.Items.Clear();
            foreach (var device in devices)
            {
                lvDevices.Items.Add(CreateRow(device));
            }
        }
        finally
        {
            lvDevices.EndUpdate();
        }
    }

    private static ListViewItem CreateRow(DeviceEntry device)
    {
        var item = new ListViewItem(device.Name);
        item.SubItems.Add(device.Address);
        item.SubItems.Add(device.Kind == LinkKind.Classic ? "Classic" : "BLE");
        item.SubItems.Add(device.Paired ? "済" : "未");
        item.SubItems.Add(device.Connected ? "○" : "-");
        item.SubItems.Add(device.Category);
        item.Tag = device;
        return item;
    }

    private void UpdateRow(DeviceEntry device)
    {
        foreach (ListViewItem item in lvDevices.Items)
        {
            if (ReferenceEquals(item.Tag, device))
            {
                item.SubItems[3].Text = device.Paired ? "済" : "未";
                item.SubItems[4].Text = device.Connected ? "○" : "-";
                return;
            }
        }
    }

    private DeviceEntry? GetSelectedDevice()
    {
        if (lvDevices.SelectedItems.Count == 0)
        {
            ShowWarning("一覧からデバイスを選択してください。");
            return null;
        }

        return lvDevices.SelectedItems[0].Tag as DeviceEntry;
    }

    private static string Describe(DeviceEntry device) => $"{device.Name} ({device.Address})";

    // ------------------------------------------------------------------
    // 2. 接続
    // ------------------------------------------------------------------

    private void cmbPreset_SelectedIndexChanged(object? sender, EventArgs e)
    {
        var preset = CurrentPreset;

        txtService.Text = preset.Service.ToString("D").ToUpperInvariant();
        txtWriteChar.Text = preset.WriteCharacteristic == Guid.Empty
            ? string.Empty
            : preset.WriteCharacteristic.ToString("D").ToUpperInvariant();
        txtNotifyChar.Text = preset.NotifyCharacteristic == Guid.Empty
            ? string.Empty
            : preset.NotifyCharacteristic.ToString("D").ToUpperInvariant();
        txtServiceName.Text = preset.ServiceName;
        lblHint.Text = preset.Hint;

        SelectFraming(preset.DefaultFraming);

        if (preset.Kind == LinkKind.Ble)
        {
            // BLE では PC 側は常にセントラル (クライアント)。
            rdoClient.Checked = true;
        }

        if (preset.Kind != LinkKind.Ble)
        {
            StopBleScan();
        }

        // 一覧に並んでいるデバイスの方式が変わったら、選び間違いを防ぐためクリアする。
        if (lvDevices.Items.Count > 0 && _listedKind != preset.Kind)
        {
            lvDevices.Items.Clear();
            AppendLog(TransferDirection.Info, "通信方式が変わったため、デバイス一覧をクリアしました。再検索してください。");
        }

        UpdateUiState();
    }

    private void role_CheckedChanged(object? sender, EventArgs e) => UpdateUiState();

    private async void btnConnect_Click(object? sender, EventArgs e)
    {
        if (_busy)
        {
            return;
        }

        var preset = CurrentPreset;

        Guid serviceId;
        try
        {
            serviceId = ParseUuid(txtService.Text, "サービス UUID");
        }
        catch (FormatException ex)
        {
            ShowError("UUID を解釈できません。", ex);
            return;
        }

        var framing = SelectedValue<FramingMode>(cmbFraming);

        if (preset.Kind == LinkKind.Classic)
        {
            if (rdoServer.Checked)
            {
                StartServer(serviceId, framing);
            }
            else
            {
                await ConnectClassicAsync(serviceId, framing);
            }

            return;
        }

        await ConnectBleAsync(serviceId, preset.Profile, framing);
    }

    private void StartServer(Guid serviceId, FramingMode framing)
    {
        try
        {
            OpenLogger($"server_{ShortId(serviceId)}");
            _server.Start(serviceId, txtServiceName.Text.Trim(), framing);

            var message =
                $"待ち受けを開始しました。サービス: {txtServiceName.Text.Trim()} / UUID: {serviceId:D} / 区切り: {MessageAssembler.DescribeFraming(framing)}";
            AppendLog(TransferDirection.Info, message);
            _logger.LogEvent(TransferDirection.Info, message);
            lblStatus.Text = "待ち受け中 (接続待ち)";
        }
        catch (Exception ex)
        {
            _logger.Close();
            ShowError("待ち受けを開始できませんでした。", ex);
        }
        finally
        {
            UpdateUiState();
        }
    }

    private async Task ConnectClassicAsync(Guid serviceId, FramingMode framing)
    {
        var entry = GetSelectedDevice();
        if (entry is null)
        {
            return;
        }

        if (entry.Kind != LinkKind.Classic || entry.HardwareAddress is null)
        {
            ShowWarning("Classic のデバイスを選択してください。BLE のデバイスには RFCOMM で接続できません。");
            return;
        }

        if (!entry.Paired && !ConfirmUnpaired(entry))
        {
            return;
        }

        SetBusy(true, $"{Describe(entry)} へ接続しています...");
        try
        {
            OpenLogger(entry.Name);

            var session = await RfcommConnector.ConnectAsync(
                entry.HardwareAddress,
                serviceId,
                framing,
                ConnectTimeout);

            RegisterSession(session);

            var message = $"接続しました: {session.DisplayName} / 区切り: {MessageAssembler.DescribeFraming(framing)}";
            AppendLog(TransferDirection.Info, message);
            _logger.LogEvent(TransferDirection.Info, message);
        }
        catch (Exception ex)
        {
            _logger.Close();
            ShowError("接続に失敗しました。相手側が同じサービス UUID で待ち受けているか確認してください。", ex);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async Task ConnectBleAsync(Guid serviceId, BleProfile profile, FramingMode framing)
    {
        var entry = GetSelectedDevice();
        if (entry is null)
        {
            return;
        }

        if (entry.Kind != LinkKind.Ble)
        {
            ShowWarning("BLE のデバイスを選択してください。");
            return;
        }

        // スキャンを続けたまま接続すると不安定になりやすいので止める。
        StopBleScan();

        var bleDevice = await entry.ResolveBleDeviceAsync();
        if (bleDevice is null)
        {
            ShowWarning("この BLE デバイスを取得できませんでした。電波が届いているか確認してください。");
            return;
        }

        BleEndpoint endpoint;
        try
        {
            endpoint = new BleEndpoint
            {
                Service = serviceId,
                WriteCharacteristic = ParseUuid(txtWriteChar.Text, "書き込み特性 UUID"),
                NotifyCharacteristic = ParseUuid(txtNotifyChar.Text, "通知特性 UUID"),
                Profile = profile,
            };
        }
        catch (FormatException ex)
        {
            ShowError("UUID を解釈できません。", ex);
            return;
        }

        SetBusy(true, $"{Describe(entry)} へ BLE 接続しています...");
        try
        {
            OpenLogger(entry.Name);

            var session = await BleConnector.ConnectAsync(
                bleDevice,
                endpoint,
                framing,
                ConnectTimeout,
                (direction, message) =>
                {
                    AppendLog(direction, message);
                    _logger.LogEvent(direction, message);
                });

            RegisterSession(session);

            var message =
                $"BLE 接続しました: {session.DisplayName} / プロファイル: {DescribeProfile(profile)} / 区切り: {MessageAssembler.DescribeFraming(framing)}";
            AppendLog(TransferDirection.Info, message);
            _logger.LogEvent(TransferDirection.Info, message);

            entry.Refresh();
            UpdateRow(entry);
        }
        catch (Exception ex)
        {
            _logger.Close();
            ShowError("BLE 接続に失敗しました。ハブの電源、UUID、対象プログラムの実行状態を確認してください。", ex);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private bool ConfirmUnpaired(DeviceEntry entry)
    {
        var answer = MessageBox.Show(
            this,
            $"{Describe(entry)} はペアリングされていません。{Environment.NewLine}先にペアリングすることを推奨します。{Environment.NewLine}このまま接続を試みますか?",
            "未ペアリング",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

        return answer == DialogResult.Yes;
    }

    private void btnDisconnect_Click(object? sender, EventArgs e)
    {
        _server.Stop();

        foreach (var session in _sessions.ToArray())
        {
            session.Close("こちらから切断しました。");
        }

        UpdateUiState();
    }

    private void Server_ClientConnected(object? sender, ILinkSession session)
    {
        RunOnUi(() =>
        {
            RegisterSession(session);
            var message = $"クライアントが接続しました: {session.DisplayName}";
            AppendLog(TransferDirection.Info, message);
            _logger.LogEvent(TransferDirection.Info, message);
        });
    }

    private void Server_ListenerStopped(object? sender, string reason)
    {
        RunOnUi(() =>
        {
            AppendLog(TransferDirection.Info, reason);
            _logger.LogEvent(TransferDirection.Info, reason);
            UpdateUiState();
            CloseLoggerIfIdle();
        });
    }

    private void RegisterSession(ILinkSession session)
    {
        session.DataReceived += Session_DataReceived;
        session.Closed += Session_Closed;

        if (session is BleSession ble)
        {
            ble.StatusReported += Ble_StatusReported;
        }

        _sessions.Add(session);
        session.Start();
        UpdateUiState();
    }

    /// <summary>
    /// 受信スレッドから直接呼ばれる。ここでは待ち行列とリングバッファへ積むだけで、
    /// UI スレッドへのマーシャリングは行わない (高頻度受信で UI が飽和するのを防ぐ)。
    /// </summary>
    private void Session_DataReceived(object? sender, SessionDataReceivedEventArgs e)
    {
        _logger.LogData(TransferDirection.Rx, e.Payload);
        _stats.AddRx(e.Payload.Length);
        _monitor.AddData(TransferDirection.Rx, e.Session.DisplayName, e.Payload);
    }

    private void Session_Closed(object? sender, SessionClosedEventArgs e)
    {
        var message = $"切断: {e.Session.DisplayName} - {e.Reason}";
        _logger.LogEvent(e.Error is null ? TransferDirection.Info : TransferDirection.Error, message);

        RunOnUi(() =>
        {
            _sessions.Remove(e.Session);
            AppendLog(e.Error is null ? TransferDirection.Info : TransferDirection.Error, message);
            UpdateUiState();
            CloseLoggerIfIdle();
        });
    }

    /// <summary>通知スレッドから呼ばれる。最新値を置くだけで、表示はタイマーに任せる。</summary>
    private void Ble_StatusReported(object? sender, PybricksStatusEventArgs e)
    {
        var report = e.Report;
        _logger.LogEvent(TransferDirection.Info, report.Describe());
        Volatile.Write(ref _latestStatus, report);

        // 同じ内容が続くときはモニタを汚さない。
        var text = report.Describe();
        if (Interlocked.Exchange(ref _lastStatusText, text) != text)
        {
            _monitor.AddMessage(TransferDirection.Info, text);
        }
    }

    private async void btnPyStart_Click(object? sender, EventArgs e) =>
        await SendControlAsync(PybricksCommand.StartUserProgram, "プログラム開始");

    private async void btnPyStop_Click(object? sender, EventArgs e) =>
        await SendControlAsync(PybricksCommand.StopUserProgram, "プログラム停止");

    private async void btnPyRepl_Click(object? sender, EventArgs e) =>
        await SendControlAsync(PybricksCommand.StartRepl, "REPL 開始");

    private async Task SendControlAsync(PybricksCommand command, string label)
    {
        var session = _sessions.OfType<BleSession>().FirstOrDefault(s => s.IsConnected && s.SupportsControl);
        if (session is null)
        {
            ShowWarning("Pybricks のハブに接続していません。");
            return;
        }

        try
        {
            await session.SendControlAsync(command);
            var message = $"{label} コマンドを送信しました (0x{(byte)command:X2})。";
            AppendLog(TransferDirection.Tx, message);
            _logger.LogEvent(TransferDirection.Info, message);
        }
        catch (Exception ex)
        {
            var message = $"{label} コマンドの送信に失敗しました: {ex.Message}";
            AppendLog(TransferDirection.Error, message);
            _logger.LogEvent(TransferDirection.Error, message);
        }
    }

    // ------------------------------------------------------------------
    // 3. 送信 / ログ
    // ------------------------------------------------------------------

    private async void btnSend_Click(object? sender, EventArgs e) => await SendAsync();

    private async void txtSend_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Control && e.KeyCode == Keys.Enter)
        {
            e.SuppressKeyPress = true;
            await SendAsync();
        }
    }

    private async Task SendAsync()
    {
        var targets = _sessions.Where(s => s.IsConnected).ToArray();
        if (targets.Length == 0)
        {
            ShowWarning("接続されていません。");
            return;
        }

        byte[] payload;
        try
        {
            payload = BuildPayload();
        }
        catch (FormatException ex)
        {
            ShowError("送信データを解釈できません。", ex);
            return;
        }

        if (payload.Length == 0)
        {
            return;
        }

        btnSend.Enabled = false;
        try
        {
            foreach (var session in targets)
            {
                try
                {
                    await session.SendAsync(payload);
                    _logger.LogData(TransferDirection.Tx, payload);
                    _stats.AddTx(payload.Length);
                    _monitor.AddData(TransferDirection.Tx, session.DisplayName, payload);
                }
                catch (SendFailedException ex)
                {
                    // 途中まで相手へ届いている可能性があるので、その分は送信として残す。
                    if (ex.SentData.Length > 0)
                    {
                        _logger.LogData(TransferDirection.Tx, ex.SentData);
                        _stats.AddTx(ex.SentData.Length);
                        _monitor.AddData(TransferDirection.Tx, session.DisplayName, ex.SentData);
                    }

                    var detail = ex.SentData.Length > 0
                        ? $"{ex.SentData.Length} バイト送出後に中断"
                        : "送出できた量は不明";
                    var message = $"送信に失敗しました ({session.DisplayName}): {ex.Message} ({detail})";
                    AppendLog(TransferDirection.Error, message);
                    _logger.LogEvent(TransferDirection.Error, message);
                }
                catch (Exception ex)
                {
                    var message = $"送信に失敗しました ({session.DisplayName}): {ex.Message}";
                    AppendLog(TransferDirection.Error, message);
                    _logger.LogEvent(TransferDirection.Error, message);
                }
            }
        }
        finally
        {
            UpdateUiState();
        }
    }

    private byte[] BuildPayload()
    {
        var text = txtSend.Text;

        if (SelectedValue<PayloadFormat>(cmbFormat) == PayloadFormat.Hex)
        {
            return HexUtil.FromHex(text);
        }

        // 区切り側で終端を付けるモードでは、末尾の改行は取り除く。
        if (SelectedValue<FramingMode>(cmbFraming) is FramingMode.CrLfDelimited or FramingMode.CrDelimited)
        {
            text = text.TrimEnd('\r', '\n');
        }

        return CurrentEncoding().GetBytes(text);
    }

    private void cmbEncoding_SelectedIndexChanged(object? sender, EventArgs e)
    {
        // ログのテキスト表現に使うエンコーディングを切り替える。
        _loggerOptions.TextEncoding = CurrentEncoding();
    }

    private void btnOpenLogFolder_Click(object? sender, EventArgs e)
    {
        try
        {
            Directory.CreateDirectory(_loggerOptions.Directory);
            Process.Start(new ProcessStartInfo
            {
                FileName = _loggerOptions.Directory,
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            ShowError("フォルダを開けませんでした。", ex);
        }
    }

    private void btnClearLog_Click(object? sender, EventArgs e)
    {
        _monitor.Clear();
        _renderedTotal = -1;
        lvMonitor.VirtualListSize = 0;
        lvMonitor.Invalidate();
    }

    private void OpenLogger(string tag)
    {
        _loggerOptions.WriteRawBinary = chkWriteRaw.Checked;
        _loggerOptions.TextEncoding = CurrentEncoding();

        _reportedWriteError = false;

        var path = _logger.Open(tag);
        lblLogPath.Text = $"出力先: {path}";
        AppendLog(TransferDirection.Info, $"ログファイルを開きました: {path}");
    }

    /// <summary>待ち受けも接続も無くなったらログファイルを閉じる (他アプリから開けるようにする)。</summary>
    private void CloseLoggerIfIdle()
    {
        if (_closing || _server.IsListening || _sessions.Any(s => s.IsConnected) || !_logger.IsOpen)
        {
            return;
        }

        _logger.Close();
        _lastStatusText = string.Empty;
        lblPyStatus.Text = string.Empty;
        AppendLog(TransferDirection.Info, "ログファイルを閉じました。");
    }

    /// <summary>
    /// モニタへ 1 行積む。リングバッファに入れるだけなので、どのスレッドから呼んでもよい。
    /// 実際の描画は <see cref="UiTimer_Tick"/> がまとめて行う。
    /// </summary>
    private void AppendLog(TransferDirection direction, string message)
    {
        if (_closing)
        {
            return;
        }

        _monitor.AddMessage(direction, message);
    }

    // ------------------------------------------------------------------
    // モニタ表示 (タイマー駆動)
    // ------------------------------------------------------------------

    /// <summary>
    /// 一定周期で画面を更新する。受信 1 件ごとに描画しないため、
    /// 受信レートがいくら上がっても UI スレッドの負荷は一定に保たれる。
    /// </summary>
    private void UiTimer_Tick(object? sender, EventArgs e)
    {
        if (_closing || IsDisposed)
        {
            return;
        }

        UpdateStatsLabel();
        UpdateMonitorView();
        UpdatePybricksStatusLabel();
        UpdateScanResults();
    }

    /// <summary>
    /// BLE スキャン中、新しく見つかった機器を一覧へ反映する。
    /// 選択中の行は維持する (スキャン中でも選んで接続できるようにするため)。
    /// </summary>
    private void UpdateScanResults()
    {
        if (_bleScanner is null)
        {
            return;
        }

        // スキャナーが自分で停止した場合にもボタン表示を追従させる。
        if (_renderedScanning != _bleScanner.IsScanning)
        {
            _renderedScanning = _bleScanner.IsScanning;
            UpdateUiState();
        }

        if (_renderedScanVersion == _bleScanner.Version)
        {
            return;
        }

        _renderedScanVersion = _bleScanner.Version;

        var selected = lvDevices.SelectedItems.Count > 0
            ? (lvDevices.SelectedItems[0].Tag as DeviceEntry)?.Address
            : null;

        var devices = _bleScanner.Snapshot();
        FillDeviceList(devices, LinkKind.Ble);

        if (selected is not null)
        {
            foreach (ListViewItem item in lvDevices.Items)
            {
                if (item.Tag is DeviceEntry entry && entry.Address == selected)
                {
                    item.Selected = true;
                    item.EnsureVisible();
                    break;
                }
            }
        }

        if (_bleScanner.IsScanning && !_busy)
        {
            lblStatus.Text = $"BLE スキャン中... {devices.Count} 台";
        }
    }

    private void UpdateStatsLabel()
    {
        var snapshot = _stats.Take();
        var text = new StringBuilder(snapshot.Describe());

        var dropped = _logger.DroppedCount;
        var writeErrors = _logger.WriteErrorCount;
        var lost = dropped + writeErrors;

        if (lost > 0)
        {
            text.Append($"    ★記録欠落 {lost:N0} 件");
            if (dropped > 0 && writeErrors > 0)
            {
                text.Append($" (待ち行列 {dropped:N0} / 書き出し失敗 {writeErrors:N0})");
            }
        }

        // 書き出しに失敗し始めたら、原因を一度だけモニタへ出す。
        if (writeErrors > 0 && !_reportedWriteError)
        {
            _reportedWriteError = true;
            AppendLog(
                TransferDirection.Error,
                $"ログの書き出しに失敗しています: {_logger.LastWriteError}  (出力先の空き容量とアクセス権を確認してください)");
        }

        var overwritten = _monitor.Overwritten;
        if (overwritten > 0)
        {
            text.Append($"    モニタ表示外 {overwritten:N0} 件 (ファイルには記録済み)");
        }

        lblStats.Text = text.ToString();
        lblStats.ForeColor = lost > 0 ? Color.FromArgb(200, 0, 0) : SystemColors.ControlText;
    }

    private void UpdateMonitorView()
    {
        var total = _monitor.TotalAdded;
        if (total == _renderedTotal)
        {
            return;
        }

        _renderedTotal = total;
        var count = _monitor.Count;

        if (lvMonitor.VirtualListSize != count)
        {
            lvMonitor.VirtualListSize = count;
        }
        else
        {
            // 件数は変わらないがリングが回って中身が入れ替わった場合。
            lvMonitor.Invalidate();
        }

        if (chkAutoScroll.Checked && count > 0)
        {
            lvMonitor.EnsureVisible(count - 1);
        }
    }

    private void UpdatePybricksStatusLabel()
    {
        var status = Volatile.Read(ref _latestStatus);
        if (status is null)
        {
            return;
        }

        var text = status.UserProgramRunning ? "プログラム: 実行中" : "プログラム: 停止中";
        if (lblPyStatus.Text != text)
        {
            lblPyStatus.Text = text;
            lblPyStatus.ForeColor = status.UserProgramRunning
                ? Color.FromArgb(0, 128, 64)
                : SystemColors.GrayText;
        }
    }

    /// <summary>
    /// 仮想モードの ListView から、表示に必要になった行だけ要求される。
    /// 16 進化や文字列デコードはここで初めて行うので、画面外のデータには一切コストがかからない。
    /// </summary>
    private void lvMonitor_RetrieveVirtualItem(object? sender, RetrieveVirtualItemEventArgs e)
    {
        var record = _monitor.Get(e.ItemIndex);
        if (record is null)
        {
            e.Item = new ListViewItem(string.Empty);
            return;
        }

        var item = new ListViewItem(record.Timestamp.ToString("HH:mm:ss.fff"));

        if (record.Payload is { } payload)
        {
            item.SubItems.Add(record.Direction == TransferDirection.Tx ? "TX >>" : "RX <<");
            item.SubItems.Add(record.Peer);
            item.SubItems.Add(payload.Length.ToString());
            item.SubItems.Add(HexUtil.ToPrintable(SafeDecode(payload)));
            item.SubItems.Add(HexUtil.ToHex(payload, 64));
        }
        else
        {
            item.SubItems.Add(record.Direction == TransferDirection.Error ? "ERR" : "INFO");
            item.SubItems.Add(string.Empty);
            item.SubItems.Add("-");
            item.SubItems.Add(record.Message ?? string.Empty);
            item.SubItems.Add(string.Empty);
        }

        item.ForeColor = record.Direction switch
        {
            TransferDirection.Tx => Color.FromArgb(0, 96, 192),
            TransferDirection.Rx => Color.FromArgb(0, 128, 64),
            TransferDirection.Error => Color.FromArgb(200, 0, 0),
            _ => Color.DimGray,
        };

        e.Item = item;
    }

    // ------------------------------------------------------------------
    // 共通
    // ------------------------------------------------------------------

    private void UpdateUiState()
    {
        var preset = CurrentPreset;
        var isBle = preset.Kind == LinkKind.Ble;
        var listening = _server.IsListening;
        var connected = _sessions.Count(s => s.IsConnected);
        var active = listening || connected > 0;

        btnConnect.Enabled = !active && !_busy;
        btnConnect.Text = isBle || rdoClient.Checked ? "接続" : "待ち受け開始";
        btnDisconnect.Enabled = active;
        btnSend.Enabled = connected > 0 && !_busy;

        cmbPreset.Enabled = !active && !_busy;
        rdoServer.Enabled = !active && !isBle;
        rdoClient.Enabled = !active && !isBle;

        // UUID 欄はカスタムプリセットのときだけ編集できる。
        txtService.ReadOnly = !preset.IsCustom;
        txtWriteChar.ReadOnly = !preset.IsCustom;
        txtNotifyChar.ReadOnly = !preset.IsCustom;
        txtService.Enabled = !active;
        lblWriteChar.Enabled = isBle;
        lblNotifyChar.Enabled = isBle;
        txtWriteChar.Enabled = isBle && !active;
        txtNotifyChar.Enabled = isBle && !active;

        // サービス名は Classic のサーバー時のみ意味がある。
        lblServiceName.Enabled = !isBle;
        txtServiceName.Enabled = !isBle && !active && rdoServer.Checked;

        cmbFraming.Enabled = !active;
        chkWriteRaw.Enabled = !active;

        // 接続中に変えるとログファイルのヘッダと中身が食い違うため固定する。
        cmbEncoding.Enabled = !active;

        var control = _sessions.OfType<BleSession>().FirstOrDefault(s => s.IsConnected && s.SupportsControl);
        btnPyStart.Enabled = control is not null && !_busy;
        btnPyStop.Enabled = control is not null && !_busy;
        btnPyRepl.Enabled = control is not null && !_busy;

        var scanning = _bleScanner is { IsScanning: true };
        btnDiscover.Enabled = !_busy;
        btnDiscover.Text = scanning ? "検索停止" : "デバイス検索";
        btnPaired.Enabled = !_busy;
        btnPair.Enabled = !_busy;
        btnUnpair.Enabled = !_busy;

        if (_busy)
        {
            return;
        }

        if (scanning && connected == 0 && !listening)
        {
            lblStatus.Text = $"BLE スキャン中... {_bleScanner!.Count} 台";
            return;
        }

        lblStatus.Text = (listening, connected) switch
        {
            (true, 0) => "待ち受け中 (接続待ち)",
            (true, _) => $"待ち受け中 / 接続数: {connected}",
            (false, 0) => "未接続",
            _ => $"接続中: {string.Join(", ", _sessions.Where(s => s.IsConnected).Select(s => s.DisplayName))}",
        };
    }

    private void SetBusy(bool busy, string? status = null)
    {
        _busy = busy;
        Cursor = busy ? Cursors.WaitCursor : Cursors.Default;

        if (busy && status is not null)
        {
            lblStatus.Text = status;
        }

        UpdateUiState();
    }

    private void RunOnUi(Action action)
    {
        if (_closing || IsDisposed || !IsHandleCreated)
        {
            return;
        }

        try
        {
            if (InvokeRequired)
            {
                BeginInvoke(action);
            }
            else
            {
                action();
            }
        }
        catch (ObjectDisposedException)
        {
            // フォームが閉じられた直後は無視してよい。
        }
        catch (InvalidOperationException)
        {
            // ハンドル破棄と競合した場合も同様。
        }
    }

    private void ShowError(string message, Exception ex)
    {
        AppendLog(TransferDirection.Error, $"{message} {ex.Message}");
        MessageBox.Show(this, $"{message}{Environment.NewLine}{Environment.NewLine}{ex.Message}", "エラー", MessageBoxButtons.OK, MessageBoxIcon.Error);
    }

    private void ShowWarning(string message) =>
        MessageBox.Show(this, message, "確認", MessageBoxButtons.OK, MessageBoxIcon.Information);

    private Encoding CurrentEncoding() => SelectedValue<Encoding>(cmbEncoding) ?? Encoding.UTF8;

    private string SafeDecode(byte[] payload)
    {
        try
        {
            return CurrentEncoding().GetString(payload);
        }
        catch (DecoderFallbackException)
        {
            return "(デコード不可)";
        }
    }

    private void SelectFraming(FramingMode mode)
    {
        for (var i = 0; i < cmbFraming.Items.Count; i++)
        {
            if (cmbFraming.Items[i] is ComboItem<FramingMode> item && item.Value == mode)
            {
                cmbFraming.SelectedIndex = i;
                return;
            }
        }
    }

    private static T SelectedValue<T>(ComboBox combo) =>
        combo.SelectedItem is ComboItem<T> item ? item.Value : default!;

    private static string DescribeProfile(BleProfile profile) =>
        profile == BleProfile.PybricksStdio ? "Pybricks stdio" : "そのまま";

    private static string ShortId(Guid id) =>
        id == LinkPreset.SerialPortService ? "spp" : id.ToString("N")[..8];

    /// <summary>UUID 文字列を解釈する。"1101" のような 16bit 短縮形も受け付ける。</summary>
    private static Guid ParseUuid(string text, string label)
    {
        var trimmed = text.Trim();

        if (Guid.TryParse(trimmed, out var guid))
        {
            return guid;
        }

        if (ushort.TryParse(trimmed, System.Globalization.NumberStyles.HexNumber, null, out var shortUuid))
        {
            return InTheHand.Net.Bluetooth.BluetoothService.CreateBluetoothUuid(shortUuid);
        }

        throw new FormatException($"{label} '{text}' は UUID として解釈できません。");
    }

    private static List<ComboItem<Encoding>> BuildEncodingItems()
    {
        var items = new List<ComboItem<Encoding>>
        {
            new("UTF-8", new UTF8Encoding(false)),
        };

        try
        {
            items.Add(new ComboItem<Encoding>("Shift_JIS", Encoding.GetEncoding("shift_jis")));
        }
        catch (Exception)
        {
            // コードページが使えない環境では選択肢から外す。
        }

        items.Add(new ComboItem<Encoding>("ASCII", Encoding.ASCII));
        items.Add(new ComboItem<Encoding>("UTF-16 LE", Encoding.Unicode));
        return items;
    }

    /// <summary>コンボボックスに表示名と値を組で持たせるための小さな入れ物。</summary>
    private sealed class ComboItem<T>(string label, T value)
    {
        public T Value { get; } = value;

        public override string ToString() => label;
    }
}
