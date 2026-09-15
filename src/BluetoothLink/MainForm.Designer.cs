namespace BluetoothLink;

partial class MainForm
{
    private System.ComponentModel.IContainer components = null;

    private System.Windows.Forms.GroupBox grpDevice;
    private System.Windows.Forms.Label lblRadio;
    private System.Windows.Forms.Button btnDiscover;
    private System.Windows.Forms.Button btnPaired;
    private System.Windows.Forms.Button btnPair;
    private System.Windows.Forms.Button btnUnpair;
    private System.Windows.Forms.Label lblPin;
    private System.Windows.Forms.TextBox txtPin;
    private System.Windows.Forms.ListView lvDevices;
    private System.Windows.Forms.ColumnHeader colName;
    private System.Windows.Forms.ColumnHeader colAddress;
    private System.Windows.Forms.ColumnHeader colKind;
    private System.Windows.Forms.ColumnHeader colPaired;
    private System.Windows.Forms.ColumnHeader colConnected;
    private System.Windows.Forms.ColumnHeader colClass;

    private System.Windows.Forms.GroupBox grpConnect;
    private System.Windows.Forms.Label lblPreset;
    private System.Windows.Forms.ComboBox cmbPreset;
    private System.Windows.Forms.RadioButton rdoServer;
    private System.Windows.Forms.RadioButton rdoClient;
    private System.Windows.Forms.Label lblService;
    private System.Windows.Forms.TextBox txtService;
    private System.Windows.Forms.Label lblServiceName;
    private System.Windows.Forms.TextBox txtServiceName;
    private System.Windows.Forms.Label lblFraming;
    private System.Windows.Forms.ComboBox cmbFraming;
    private System.Windows.Forms.Label lblWriteChar;
    private System.Windows.Forms.TextBox txtWriteChar;
    private System.Windows.Forms.Label lblNotifyChar;
    private System.Windows.Forms.TextBox txtNotifyChar;
    private System.Windows.Forms.Button btnConnect;
    private System.Windows.Forms.Button btnDisconnect;
    private System.Windows.Forms.Button btnPyStart;
    private System.Windows.Forms.Button btnPyStop;
    private System.Windows.Forms.Button btnPyRepl;
    private System.Windows.Forms.Label lblPyStatus;
    private System.Windows.Forms.Label lblHint;
    private System.Windows.Forms.Label lblStatus;

    private System.Windows.Forms.GroupBox grpIo;
    private System.Windows.Forms.Label lblFormat;
    private System.Windows.Forms.ComboBox cmbFormat;
    private System.Windows.Forms.Label lblEncoding;
    private System.Windows.Forms.ComboBox cmbEncoding;
    private System.Windows.Forms.CheckBox chkWriteRaw;
    private System.Windows.Forms.CheckBox chkAutoScroll;
    private System.Windows.Forms.Button btnSend;
    private System.Windows.Forms.TextBox txtSend;
    private System.Windows.Forms.Label lblStats;
    private System.Windows.Forms.ListView lvMonitor;
    private System.Windows.Forms.ColumnHeader colTime;
    private System.Windows.Forms.ColumnHeader colDirection;
    private System.Windows.Forms.ColumnHeader colPeer;
    private System.Windows.Forms.ColumnHeader colBytes;
    private System.Windows.Forms.ColumnHeader colText;
    private System.Windows.Forms.ColumnHeader colHex;
    private System.Windows.Forms.Label lblLogPath;
    private System.Windows.Forms.Button btnOpenLogFolder;
    private System.Windows.Forms.Button btnClearLog;

    protected override void Dispose(bool disposing)
    {
        if (disposing && (components != null))
        {
            components.Dispose();
        }

        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        this.grpDevice = new System.Windows.Forms.GroupBox();
        this.lblRadio = new System.Windows.Forms.Label();
        this.btnDiscover = new System.Windows.Forms.Button();
        this.btnPaired = new System.Windows.Forms.Button();
        this.btnPair = new System.Windows.Forms.Button();
        this.btnUnpair = new System.Windows.Forms.Button();
        this.lblPin = new System.Windows.Forms.Label();
        this.txtPin = new System.Windows.Forms.TextBox();
        this.lvDevices = new System.Windows.Forms.ListView();
        this.colName = new System.Windows.Forms.ColumnHeader();
        this.colAddress = new System.Windows.Forms.ColumnHeader();
        this.colKind = new System.Windows.Forms.ColumnHeader();
        this.colPaired = new System.Windows.Forms.ColumnHeader();
        this.colConnected = new System.Windows.Forms.ColumnHeader();
        this.colClass = new System.Windows.Forms.ColumnHeader();
        this.grpConnect = new System.Windows.Forms.GroupBox();
        this.lblPreset = new System.Windows.Forms.Label();
        this.cmbPreset = new System.Windows.Forms.ComboBox();
        this.rdoServer = new System.Windows.Forms.RadioButton();
        this.rdoClient = new System.Windows.Forms.RadioButton();
        this.lblService = new System.Windows.Forms.Label();
        this.txtService = new System.Windows.Forms.TextBox();
        this.lblServiceName = new System.Windows.Forms.Label();
        this.txtServiceName = new System.Windows.Forms.TextBox();
        this.lblFraming = new System.Windows.Forms.Label();
        this.cmbFraming = new System.Windows.Forms.ComboBox();
        this.lblWriteChar = new System.Windows.Forms.Label();
        this.txtWriteChar = new System.Windows.Forms.TextBox();
        this.lblNotifyChar = new System.Windows.Forms.Label();
        this.txtNotifyChar = new System.Windows.Forms.TextBox();
        this.btnConnect = new System.Windows.Forms.Button();
        this.btnDisconnect = new System.Windows.Forms.Button();
        this.btnPyStart = new System.Windows.Forms.Button();
        this.btnPyStop = new System.Windows.Forms.Button();
        this.btnPyRepl = new System.Windows.Forms.Button();
        this.lblPyStatus = new System.Windows.Forms.Label();
        this.lblHint = new System.Windows.Forms.Label();
        this.lblStatus = new System.Windows.Forms.Label();
        this.grpIo = new System.Windows.Forms.GroupBox();
        this.lblFormat = new System.Windows.Forms.Label();
        this.cmbFormat = new System.Windows.Forms.ComboBox();
        this.lblEncoding = new System.Windows.Forms.Label();
        this.cmbEncoding = new System.Windows.Forms.ComboBox();
        this.chkWriteRaw = new System.Windows.Forms.CheckBox();
        this.chkAutoScroll = new System.Windows.Forms.CheckBox();
        this.btnSend = new System.Windows.Forms.Button();
        this.txtSend = new System.Windows.Forms.TextBox();
        this.lblStats = new System.Windows.Forms.Label();
        this.lvMonitor = new System.Windows.Forms.ListView();
        this.colTime = new System.Windows.Forms.ColumnHeader();
        this.colDirection = new System.Windows.Forms.ColumnHeader();
        this.colPeer = new System.Windows.Forms.ColumnHeader();
        this.colBytes = new System.Windows.Forms.ColumnHeader();
        this.colText = new System.Windows.Forms.ColumnHeader();
        this.colHex = new System.Windows.Forms.ColumnHeader();
        this.lblLogPath = new System.Windows.Forms.Label();
        this.btnOpenLogFolder = new System.Windows.Forms.Button();
        this.btnClearLog = new System.Windows.Forms.Button();
        this.grpDevice.SuspendLayout();
        this.grpConnect.SuspendLayout();
        this.grpIo.SuspendLayout();
        this.SuspendLayout();

        //
        // grpDevice
        //
        this.grpDevice.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
        this.grpDevice.Controls.Add(this.lblRadio);
        this.grpDevice.Controls.Add(this.btnDiscover);
        this.grpDevice.Controls.Add(this.btnPaired);
        this.grpDevice.Controls.Add(this.btnPair);
        this.grpDevice.Controls.Add(this.btnUnpair);
        this.grpDevice.Controls.Add(this.lblPin);
        this.grpDevice.Controls.Add(this.txtPin);
        this.grpDevice.Controls.Add(this.lvDevices);
        this.grpDevice.Location = new System.Drawing.Point(12, 12);
        this.grpDevice.Name = "grpDevice";
        this.grpDevice.Size = new System.Drawing.Size(960, 220);
        this.grpDevice.TabIndex = 0;
        this.grpDevice.TabStop = false;
        this.grpDevice.Text = "1. デバイス検索 / ペアリング";
        //
        // lblRadio
        //
        this.lblRadio.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
        this.lblRadio.Location = new System.Drawing.Point(14, 22);
        this.lblRadio.Name = "lblRadio";
        this.lblRadio.Size = new System.Drawing.Size(930, 18);
        this.lblRadio.TabIndex = 0;
        this.lblRadio.Text = "アダプター情報を取得中...";
        //
        // btnDiscover
        //
        this.btnDiscover.Location = new System.Drawing.Point(14, 46);
        this.btnDiscover.Name = "btnDiscover";
        this.btnDiscover.Size = new System.Drawing.Size(150, 30);
        this.btnDiscover.TabIndex = 1;
        this.btnDiscover.Text = "デバイス検索";
        this.btnDiscover.UseVisualStyleBackColor = true;
        this.btnDiscover.Click += new System.EventHandler(this.btnDiscover_Click);
        //
        // btnPaired
        //
        this.btnPaired.Location = new System.Drawing.Point(170, 46);
        this.btnPaired.Name = "btnPaired";
        this.btnPaired.Size = new System.Drawing.Size(170, 30);
        this.btnPaired.TabIndex = 2;
        this.btnPaired.Text = "ペアリング済みを表示";
        this.btnPaired.UseVisualStyleBackColor = true;
        this.btnPaired.Click += new System.EventHandler(this.btnPaired_Click);
        //
        // btnPair
        //
        this.btnPair.Location = new System.Drawing.Point(356, 46);
        this.btnPair.Name = "btnPair";
        this.btnPair.Size = new System.Drawing.Size(110, 30);
        this.btnPair.TabIndex = 3;
        this.btnPair.Text = "ペアリング";
        this.btnPair.UseVisualStyleBackColor = true;
        this.btnPair.Click += new System.EventHandler(this.btnPair_Click);
        //
        // btnUnpair
        //
        this.btnUnpair.Location = new System.Drawing.Point(472, 46);
        this.btnUnpair.Name = "btnUnpair";
        this.btnUnpair.Size = new System.Drawing.Size(130, 30);
        this.btnUnpair.TabIndex = 4;
        this.btnUnpair.Text = "ペアリング解除";
        this.btnUnpair.UseVisualStyleBackColor = true;
        this.btnUnpair.Click += new System.EventHandler(this.btnUnpair_Click);
        //
        // lblPin
        //
        this.lblPin.Location = new System.Drawing.Point(614, 53);
        this.lblPin.Name = "lblPin";
        this.lblPin.Size = new System.Drawing.Size(70, 18);
        this.lblPin.TabIndex = 5;
        this.lblPin.Text = "PIN(任意):";
        //
        // txtPin
        //
        this.txtPin.Location = new System.Drawing.Point(686, 49);
        this.txtPin.Name = "txtPin";
        this.txtPin.Size = new System.Drawing.Size(100, 25);
        this.txtPin.TabIndex = 6;
        //
        // lvDevices
        //
        this.lvDevices.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
        this.lvDevices.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.colName,
            this.colAddress,
            this.colKind,
            this.colPaired,
            this.colConnected,
            this.colClass});
        this.lvDevices.FullRowSelect = true;
        this.lvDevices.GridLines = true;
        this.lvDevices.HideSelection = false;
        this.lvDevices.Location = new System.Drawing.Point(14, 86);
        this.lvDevices.MultiSelect = false;
        this.lvDevices.Name = "lvDevices";
        this.lvDevices.Size = new System.Drawing.Size(932, 120);
        this.lvDevices.TabIndex = 7;
        this.lvDevices.UseCompatibleStateImageBehavior = false;
        this.lvDevices.View = System.Windows.Forms.View.Details;
        //
        // colName
        //
        this.colName.Text = "名前";
        this.colName.Width = 230;
        //
        // colAddress
        //
        this.colAddress.Text = "アドレス";
        this.colAddress.Width = 140;
        //
        // colKind
        //
        this.colKind.Text = "方式";
        this.colKind.Width = 70;
        //
        // colPaired
        //
        this.colPaired.Text = "ペアリング";
        this.colPaired.Width = 80;
        //
        // colConnected
        //
        this.colConnected.Text = "接続";
        this.colConnected.Width = 60;
        //
        // colClass
        //
        this.colClass.Text = "種別";
        this.colClass.Width = 320;

        //
        // grpConnect
        //
        this.grpConnect.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
        this.grpConnect.Controls.Add(this.lblPreset);
        this.grpConnect.Controls.Add(this.cmbPreset);
        this.grpConnect.Controls.Add(this.rdoServer);
        this.grpConnect.Controls.Add(this.rdoClient);
        this.grpConnect.Controls.Add(this.lblService);
        this.grpConnect.Controls.Add(this.txtService);
        this.grpConnect.Controls.Add(this.lblServiceName);
        this.grpConnect.Controls.Add(this.txtServiceName);
        this.grpConnect.Controls.Add(this.lblFraming);
        this.grpConnect.Controls.Add(this.cmbFraming);
        this.grpConnect.Controls.Add(this.lblWriteChar);
        this.grpConnect.Controls.Add(this.txtWriteChar);
        this.grpConnect.Controls.Add(this.lblNotifyChar);
        this.grpConnect.Controls.Add(this.txtNotifyChar);
        this.grpConnect.Controls.Add(this.btnConnect);
        this.grpConnect.Controls.Add(this.btnDisconnect);
        this.grpConnect.Controls.Add(this.btnPyStart);
        this.grpConnect.Controls.Add(this.btnPyStop);
        this.grpConnect.Controls.Add(this.btnPyRepl);
        this.grpConnect.Controls.Add(this.lblPyStatus);
        this.grpConnect.Controls.Add(this.lblHint);
        this.grpConnect.Controls.Add(this.lblStatus);
        this.grpConnect.Location = new System.Drawing.Point(12, 238);
        this.grpConnect.Name = "grpConnect";
        this.grpConnect.Size = new System.Drawing.Size(960, 214);
        this.grpConnect.TabIndex = 1;
        this.grpConnect.TabStop = false;
        this.grpConnect.Text = "2. 接続 (Classic: サーバー/クライアント, BLE: クライアント)";
        //
        // lblPreset
        //
        this.lblPreset.Location = new System.Drawing.Point(16, 28);
        this.lblPreset.Name = "lblPreset";
        this.lblPreset.Size = new System.Drawing.Size(70, 18);
        this.lblPreset.TabIndex = 0;
        this.lblPreset.Text = "プリセット:";
        //
        // cmbPreset
        //
        this.cmbPreset.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
        this.cmbPreset.Location = new System.Drawing.Point(90, 24);
        this.cmbPreset.Name = "cmbPreset";
        this.cmbPreset.Size = new System.Drawing.Size(250, 25);
        this.cmbPreset.TabIndex = 1;
        this.cmbPreset.SelectedIndexChanged += new System.EventHandler(this.cmbPreset_SelectedIndexChanged);
        //
        // rdoServer
        //
        this.rdoServer.Location = new System.Drawing.Point(360, 26);
        this.rdoServer.Name = "rdoServer";
        this.rdoServer.Size = new System.Drawing.Size(190, 24);
        this.rdoServer.TabIndex = 2;
        this.rdoServer.Text = "サーバー (待ち受ける)";
        this.rdoServer.UseVisualStyleBackColor = true;
        this.rdoServer.CheckedChanged += new System.EventHandler(this.role_CheckedChanged);
        //
        // rdoClient
        //
        this.rdoClient.Checked = true;
        this.rdoClient.Location = new System.Drawing.Point(556, 26);
        this.rdoClient.Name = "rdoClient";
        this.rdoClient.Size = new System.Drawing.Size(280, 24);
        this.rdoClient.TabIndex = 3;
        this.rdoClient.TabStop = true;
        this.rdoClient.Text = "クライアント (選択デバイスへ接続)";
        this.rdoClient.UseVisualStyleBackColor = true;
        this.rdoClient.CheckedChanged += new System.EventHandler(this.role_CheckedChanged);
        //
        // lblService
        //
        this.lblService.Location = new System.Drawing.Point(16, 62);
        this.lblService.Name = "lblService";
        this.lblService.Size = new System.Drawing.Size(86, 18);
        this.lblService.TabIndex = 4;
        this.lblService.Text = "サービスUUID:";
        //
        // txtService
        //
        this.txtService.Location = new System.Drawing.Point(106, 58);
        this.txtService.Name = "txtService";
        this.txtService.Size = new System.Drawing.Size(300, 25);
        this.txtService.TabIndex = 5;
        //
        // lblServiceName
        //
        this.lblServiceName.Location = new System.Drawing.Point(416, 62);
        this.lblServiceName.Name = "lblServiceName";
        this.lblServiceName.Size = new System.Drawing.Size(70, 18);
        this.lblServiceName.TabIndex = 6;
        this.lblServiceName.Text = "サービス名:";
        //
        // txtServiceName
        //
        this.txtServiceName.Location = new System.Drawing.Point(490, 58);
        this.txtServiceName.Name = "txtServiceName";
        this.txtServiceName.Size = new System.Drawing.Size(150, 25);
        this.txtServiceName.TabIndex = 7;
        this.txtServiceName.Text = "BluetoothLink";
        //
        // lblFraming
        //
        this.lblFraming.Location = new System.Drawing.Point(650, 62);
        this.lblFraming.Name = "lblFraming";
        this.lblFraming.Size = new System.Drawing.Size(50, 18);
        this.lblFraming.TabIndex = 8;
        this.lblFraming.Text = "区切り:";
        //
        // cmbFraming
        //
        this.cmbFraming.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
        this.cmbFraming.Location = new System.Drawing.Point(702, 58);
        this.cmbFraming.Name = "cmbFraming";
        this.cmbFraming.Size = new System.Drawing.Size(242, 25);
        this.cmbFraming.TabIndex = 9;
        //
        // lblWriteChar
        //
        this.lblWriteChar.Location = new System.Drawing.Point(16, 96);
        this.lblWriteChar.Name = "lblWriteChar";
        this.lblWriteChar.Size = new System.Drawing.Size(86, 18);
        this.lblWriteChar.TabIndex = 10;
        this.lblWriteChar.Text = "書込特性:";
        //
        // txtWriteChar
        //
        this.txtWriteChar.Location = new System.Drawing.Point(106, 92);
        this.txtWriteChar.Name = "txtWriteChar";
        this.txtWriteChar.Size = new System.Drawing.Size(300, 25);
        this.txtWriteChar.TabIndex = 11;
        //
        // lblNotifyChar
        //
        this.lblNotifyChar.Location = new System.Drawing.Point(416, 96);
        this.lblNotifyChar.Name = "lblNotifyChar";
        this.lblNotifyChar.Size = new System.Drawing.Size(70, 18);
        this.lblNotifyChar.TabIndex = 12;
        this.lblNotifyChar.Text = "通知特性:";
        //
        // txtNotifyChar
        //
        this.txtNotifyChar.Location = new System.Drawing.Point(490, 92);
        this.txtNotifyChar.Name = "txtNotifyChar";
        this.txtNotifyChar.Size = new System.Drawing.Size(300, 25);
        this.txtNotifyChar.TabIndex = 13;
        //
        // btnConnect
        //
        this.btnConnect.Location = new System.Drawing.Point(16, 124);
        this.btnConnect.Name = "btnConnect";
        this.btnConnect.Size = new System.Drawing.Size(180, 32);
        this.btnConnect.TabIndex = 14;
        this.btnConnect.Text = "接続 / 待ち受け開始";
        this.btnConnect.UseVisualStyleBackColor = true;
        this.btnConnect.Click += new System.EventHandler(this.btnConnect_Click);
        //
        // btnDisconnect
        //
        this.btnDisconnect.Enabled = false;
        this.btnDisconnect.Location = new System.Drawing.Point(204, 124);
        this.btnDisconnect.Name = "btnDisconnect";
        this.btnDisconnect.Size = new System.Drawing.Size(120, 32);
        this.btnDisconnect.TabIndex = 15;
        this.btnDisconnect.Text = "切断";
        this.btnDisconnect.UseVisualStyleBackColor = true;
        this.btnDisconnect.Click += new System.EventHandler(this.btnDisconnect_Click);
        //
        // btnPyStart
        //
        this.btnPyStart.Enabled = false;
        this.btnPyStart.Location = new System.Drawing.Point(340, 124);
        this.btnPyStart.Name = "btnPyStart";
        this.btnPyStart.Size = new System.Drawing.Size(140, 32);
        this.btnPyStart.TabIndex = 16;
        this.btnPyStart.Text = "プログラム開始";
        this.btnPyStart.UseVisualStyleBackColor = true;
        this.btnPyStart.Click += new System.EventHandler(this.btnPyStart_Click);
        //
        // btnPyStop
        //
        this.btnPyStop.Enabled = false;
        this.btnPyStop.Location = new System.Drawing.Point(488, 124);
        this.btnPyStop.Name = "btnPyStop";
        this.btnPyStop.Size = new System.Drawing.Size(120, 32);
        this.btnPyStop.TabIndex = 17;
        this.btnPyStop.Text = "停止";
        this.btnPyStop.UseVisualStyleBackColor = true;
        this.btnPyStop.Click += new System.EventHandler(this.btnPyStop_Click);
        //
        // btnPyRepl
        //
        this.btnPyRepl.Enabled = false;
        this.btnPyRepl.Location = new System.Drawing.Point(616, 124);
        this.btnPyRepl.Name = "btnPyRepl";
        this.btnPyRepl.Size = new System.Drawing.Size(120, 32);
        this.btnPyRepl.TabIndex = 18;
        this.btnPyRepl.Text = "REPL開始";
        this.btnPyRepl.UseVisualStyleBackColor = true;
        this.btnPyRepl.Click += new System.EventHandler(this.btnPyRepl_Click);
        //
        // lblPyStatus
        //
        this.lblPyStatus.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
        this.lblPyStatus.AutoEllipsis = true;
        this.lblPyStatus.Location = new System.Drawing.Point(748, 132);
        this.lblPyStatus.Name = "lblPyStatus";
        this.lblPyStatus.Size = new System.Drawing.Size(196, 18);
        this.lblPyStatus.TabIndex = 19;
        this.lblPyStatus.Text = "";
        //
        // lblHint
        //
        this.lblHint.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
        this.lblHint.AutoEllipsis = true;
        this.lblHint.ForeColor = System.Drawing.SystemColors.GrayText;
        this.lblHint.Location = new System.Drawing.Point(16, 164);
        this.lblHint.Name = "lblHint";
        this.lblHint.Size = new System.Drawing.Size(928, 18);
        this.lblHint.TabIndex = 20;
        this.lblHint.Text = "";
        //
        // lblStatus
        //
        this.lblStatus.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
        this.lblStatus.AutoEllipsis = true;
        this.lblStatus.Location = new System.Drawing.Point(16, 188);
        this.lblStatus.Name = "lblStatus";
        this.lblStatus.Size = new System.Drawing.Size(928, 18);
        this.lblStatus.TabIndex = 21;
        this.lblStatus.Text = "未接続";

        //
        // grpIo
        //
        this.grpIo.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
        this.grpIo.Controls.Add(this.lblFormat);
        this.grpIo.Controls.Add(this.cmbFormat);
        this.grpIo.Controls.Add(this.lblEncoding);
        this.grpIo.Controls.Add(this.cmbEncoding);
        this.grpIo.Controls.Add(this.chkWriteRaw);
        this.grpIo.Controls.Add(this.chkAutoScroll);
        this.grpIo.Controls.Add(this.btnSend);
        this.grpIo.Controls.Add(this.txtSend);
        this.grpIo.Controls.Add(this.lblStats);
        this.grpIo.Controls.Add(this.lvMonitor);
        this.grpIo.Controls.Add(this.lblLogPath);
        this.grpIo.Controls.Add(this.btnOpenLogFolder);
        this.grpIo.Controls.Add(this.btnClearLog);
        this.grpIo.Location = new System.Drawing.Point(12, 458);
        this.grpIo.Name = "grpIo";
        this.grpIo.Size = new System.Drawing.Size(960, 390);
        this.grpIo.TabIndex = 2;
        this.grpIo.TabStop = false;
        this.grpIo.Text = "3. データ送受信 / ファイル書き出し";
        //
        // lblFormat
        //
        this.lblFormat.Location = new System.Drawing.Point(16, 26);
        this.lblFormat.Name = "lblFormat";
        this.lblFormat.Size = new System.Drawing.Size(60, 18);
        this.lblFormat.TabIndex = 0;
        this.lblFormat.Text = "送信形式:";
        //
        // cmbFormat
        //
        this.cmbFormat.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
        this.cmbFormat.Location = new System.Drawing.Point(78, 22);
        this.cmbFormat.Name = "cmbFormat";
        this.cmbFormat.Size = new System.Drawing.Size(120, 25);
        this.cmbFormat.TabIndex = 1;
        //
        // lblEncoding
        //
        this.lblEncoding.Location = new System.Drawing.Point(210, 26);
        this.lblEncoding.Name = "lblEncoding";
        this.lblEncoding.Size = new System.Drawing.Size(70, 18);
        this.lblEncoding.TabIndex = 2;
        this.lblEncoding.Text = "文字コード:";
        //
        // cmbEncoding
        //
        this.cmbEncoding.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
        this.cmbEncoding.Location = new System.Drawing.Point(284, 22);
        this.cmbEncoding.Name = "cmbEncoding";
        this.cmbEncoding.Size = new System.Drawing.Size(120, 25);
        this.cmbEncoding.TabIndex = 3;
        this.cmbEncoding.SelectedIndexChanged += new System.EventHandler(this.cmbEncoding_SelectedIndexChanged);
        //
        // chkWriteRaw
        //
        this.chkWriteRaw.Checked = true;
        this.chkWriteRaw.CheckState = System.Windows.Forms.CheckState.Checked;
        this.chkWriteRaw.Location = new System.Drawing.Point(416, 24);
        this.chkWriteRaw.Name = "chkWriteRaw";
        this.chkWriteRaw.Size = new System.Drawing.Size(180, 22);
        this.chkWriteRaw.TabIndex = 4;
        this.chkWriteRaw.Text = "生データ(.bin)も書き出す";
        this.chkWriteRaw.UseVisualStyleBackColor = true;
        //
        // chkAutoScroll
        //
        this.chkAutoScroll.Checked = true;
        this.chkAutoScroll.CheckState = System.Windows.Forms.CheckState.Checked;
        this.chkAutoScroll.Location = new System.Drawing.Point(602, 24);
        this.chkAutoScroll.Name = "chkAutoScroll";
        this.chkAutoScroll.Size = new System.Drawing.Size(130, 22);
        this.chkAutoScroll.TabIndex = 5;
        this.chkAutoScroll.Text = "自動スクロール";
        this.chkAutoScroll.UseVisualStyleBackColor = true;
        //
        // btnSend
        //
        this.btnSend.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
        this.btnSend.Enabled = false;
        this.btnSend.Location = new System.Drawing.Point(820, 20);
        this.btnSend.Name = "btnSend";
        this.btnSend.Size = new System.Drawing.Size(124, 30);
        this.btnSend.TabIndex = 6;
        this.btnSend.Text = "送信 (Ctrl+Enter)";
        this.btnSend.UseVisualStyleBackColor = true;
        this.btnSend.Click += new System.EventHandler(this.btnSend_Click);
        //
        // txtSend
        //
        this.txtSend.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
        this.txtSend.Location = new System.Drawing.Point(16, 54);
        this.txtSend.Multiline = true;
        this.txtSend.Name = "txtSend";
        this.txtSend.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
        this.txtSend.Size = new System.Drawing.Size(928, 54);
        this.txtSend.TabIndex = 7;
        this.txtSend.KeyDown += new System.Windows.Forms.KeyEventHandler(this.txtSend_KeyDown);
        //
        // lblStats
        //
        this.lblStats.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
        this.lblStats.AutoEllipsis = true;
        this.lblStats.Location = new System.Drawing.Point(16, 116);
        this.lblStats.Name = "lblStats";
        this.lblStats.Size = new System.Drawing.Size(928, 18);
        this.lblStats.TabIndex = 8;
        this.lblStats.Text = "受信 0 件 / 0 B    送信 0 件 / 0 B";
        //
        // lvMonitor
        //
        this.lvMonitor.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
        this.lvMonitor.BackColor = System.Drawing.Color.White;
        this.lvMonitor.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.colTime,
            this.colDirection,
            this.colPeer,
            this.colBytes,
            this.colText,
            this.colHex});
        this.lvMonitor.Font = new System.Drawing.Font("Consolas", 9F);
        this.lvMonitor.FullRowSelect = true;
        this.lvMonitor.HideSelection = false;
        this.lvMonitor.Location = new System.Drawing.Point(16, 138);
        this.lvMonitor.MultiSelect = false;
        this.lvMonitor.Name = "lvMonitor";
        this.lvMonitor.Size = new System.Drawing.Size(928, 210);
        this.lvMonitor.TabIndex = 9;
        this.lvMonitor.UseCompatibleStateImageBehavior = false;
        this.lvMonitor.View = System.Windows.Forms.View.Details;
        this.lvMonitor.VirtualMode = true;
        this.lvMonitor.RetrieveVirtualItem += new System.Windows.Forms.RetrieveVirtualItemEventHandler(this.lvMonitor_RetrieveVirtualItem);
        //
        // colTime
        //
        this.colTime.Text = "時刻";
        this.colTime.Width = 100;
        //
        // colDirection
        //
        this.colDirection.Text = "方向";
        this.colDirection.Width = 55;
        //
        // colPeer
        //
        this.colPeer.Text = "相手";
        this.colPeer.Width = 140;
        //
        // colBytes
        //
        this.colBytes.Text = "バイト";
        this.colBytes.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
        this.colBytes.Width = 65;
        //
        // colText
        //
        this.colText.Text = "テキスト / メッセージ";
        this.colText.Width = 320;
        //
        // colHex
        //
        this.colHex.Text = "16進";
        this.colHex.Width = 240;
        //
        // lblLogPath
        //
        this.lblLogPath.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
        this.lblLogPath.AutoEllipsis = true;
        this.lblLogPath.Location = new System.Drawing.Point(16, 360);
        this.lblLogPath.Name = "lblLogPath";
        this.lblLogPath.Size = new System.Drawing.Size(660, 18);
        this.lblLogPath.TabIndex = 10;
        this.lblLogPath.Text = "出力先:";
        //
        // btnOpenLogFolder
        //
        this.btnOpenLogFolder.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right;
        this.btnOpenLogFolder.Location = new System.Drawing.Point(694, 354);
        this.btnOpenLogFolder.Name = "btnOpenLogFolder";
        this.btnOpenLogFolder.Size = new System.Drawing.Size(130, 26);
        this.btnOpenLogFolder.TabIndex = 11;
        this.btnOpenLogFolder.Text = "ログフォルダを開く";
        this.btnOpenLogFolder.UseVisualStyleBackColor = true;
        this.btnOpenLogFolder.Click += new System.EventHandler(this.btnOpenLogFolder_Click);
        //
        // btnClearLog
        //
        this.btnClearLog.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right;
        this.btnClearLog.Location = new System.Drawing.Point(830, 354);
        this.btnClearLog.Name = "btnClearLog";
        this.btnClearLog.Size = new System.Drawing.Size(114, 26);
        this.btnClearLog.TabIndex = 12;
        this.btnClearLog.Text = "モニタクリア";
        this.btnClearLog.UseVisualStyleBackColor = true;
        this.btnClearLog.Click += new System.EventHandler(this.btnClearLog_Click);

        //
        // MainForm
        //
        this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
        this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
        this.ClientSize = new System.Drawing.Size(984, 860);
        this.Controls.Add(this.grpDevice);
        this.Controls.Add(this.grpConnect);
        this.Controls.Add(this.grpIo);
        this.MinimumSize = new System.Drawing.Size(1000, 899);
        this.Name = "MainForm";
        this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
        this.Text = "BluetoothLink - Bluetooth データ送受信ツール";
        this.Load += new System.EventHandler(this.MainForm_Load);
        this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.MainForm_FormClosing);
        this.grpDevice.ResumeLayout(false);
        this.grpDevice.PerformLayout();
        this.grpConnect.ResumeLayout(false);
        this.grpConnect.PerformLayout();
        this.grpIo.ResumeLayout(false);
        this.grpIo.PerformLayout();
        this.ResumeLayout(false);
    }
}
