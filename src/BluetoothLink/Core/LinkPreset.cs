namespace BluetoothLink.Core;

/// <summary>
/// 接続先ごとの UUID・区切り・BLE プロファイルの組み合わせ。
/// 画面のプリセット選択に並べる。
/// </summary>
public sealed class LinkPreset
{
    // ---- Classic ----

    /// <summary>SPP (シリアルポートプロファイル)。</summary>
    public static readonly Guid SerialPortService = new("00001101-0000-1000-8000-00805F9B34FB");

    /// <summary>このアプリ同士で通信するときに使う独自サービス UUID。</summary>
    public static readonly Guid AppService = new("9C6D7B1E-4F2A-4C1B-9E3D-8A5F0B7C2D31");

    // ---- BLE: Pybricks ----
    // https://github.com/pybricks/technical-info/blob/master/assigned-numbers.md

    public static readonly Guid PybricksService = new("c5f50001-8280-46da-89f4-6d8051e4aeef");

    /// <summary>Pybricks の Command/Event 特性。書き込みと通知を兼ねる。</summary>
    public static readonly Guid PybricksCommandEvent = new("c5f50002-8280-46da-89f4-6d8051e4aeef");

    // ---- BLE: Nordic UART Service ----

    public static readonly Guid NusService = new("6e400001-b5a3-f393-e0a9-e50e24dcca9e");

    /// <summary>NUS の RX 特性 (セントラル -> ペリフェラルの書き込み)。</summary>
    public static readonly Guid NusRxWrite = new("6e400002-b5a3-f393-e0a9-e50e24dcca9e");

    /// <summary>NUS の TX 特性 (ペリフェラル -> セントラルの通知)。</summary>
    public static readonly Guid NusTxNotify = new("6e400003-b5a3-f393-e0a9-e50e24dcca9e");

    private LinkPreset(string label, LinkKind kind)
    {
        Label = label;
        Kind = kind;
    }

    public string Label { get; private init; }

    public LinkKind Kind { get; private init; }

    /// <summary>Classic ならサービス UUID、BLE なら GATT サービス UUID。</summary>
    public Guid Service { get; private init; }

    /// <summary>BLE: 書き込み先の特性 UUID。</summary>
    public Guid WriteCharacteristic { get; private init; }

    /// <summary>BLE: 通知を受ける特性 UUID。書き込みと同じこともある。</summary>
    public Guid NotifyCharacteristic { get; private init; }

    public BleProfile Profile { get; private init; } = BleProfile.Raw;

    public FramingMode DefaultFraming { get; private init; } = FramingMode.CrLfDelimited;

    /// <summary>Classic のサーバー時に SDP へ登録する名前。</summary>
    public string ServiceName { get; private init; } = "BluetoothLink";

    /// <summary>ユーザーが UUID を自由に編集できるプリセットか。</summary>
    public bool IsCustom { get; private init; }

    /// <summary>起動時に選ばれるプリセットか。</summary>
    public bool IsDefault { get; private init; }

    /// <summary>画面のヒント欄に出す補足。</summary>
    public string Hint { get; private init; } = string.Empty;

    public override string ToString() => Label;

    /// <summary>起動時に選ばれるプリセット。</summary>
    public static LinkPreset Default => All.FirstOrDefault(p => p.IsDefault) ?? All[0];

    public static IReadOnlyList<LinkPreset> All { get; } = new[]
    {
        new LinkPreset("Classic: SPIKE Hub / SPP", LinkKind.Classic)
        {
            Service = SerialPortService,
            DefaultFraming = FramingMode.CrDelimited,
            ServiceName = "BluetoothLink",
            Hint = "LEGO 公式ファームの SPIKE Hub は SPP 上の JSON を CR(0x0D) で終端します。クライアントで接続してください。",
        },
        new LinkPreset("Classic: BluetoothLink 独自", LinkKind.Classic)
        {
            Service = AppService,
            DefaultFraming = FramingMode.CrLfDelimited,
            ServiceName = "BluetoothLink",
            Hint = "本アプリ同士で通信する場合に使います。片方をサーバー、もう片方をクライアントにしてください。",
        },
        new LinkPreset("Classic: カスタム", LinkKind.Classic)
        {
            Service = SerialPortService,
            DefaultFraming = FramingMode.Raw,
            ServiceName = "BluetoothLink",
            IsCustom = true,
            Hint = "サービス UUID を直接入力します。",
        },
        new LinkPreset("BLE: Pybricks (stdio)", LinkKind.Ble)
        {
            Service = PybricksService,
            WriteCharacteristic = PybricksCommandEvent,
            NotifyCharacteristic = PybricksCommandEvent,
            Profile = BleProfile.PybricksStdio,
            DefaultFraming = FramingMode.CrDelimited,
            IsDefault = true,
            Hint = "Pybricks ファームのハブ。送信は stdin、受信は print() などの stdout になります。ハブ側でプログラムを実行しておく必要があります。",
        },
        new LinkPreset("BLE: Nordic UART (NUS)", LinkKind.Ble)
        {
            Service = NusService,
            WriteCharacteristic = NusRxWrite,
            NotifyCharacteristic = NusTxNotify,
            Profile = BleProfile.Raw,
            DefaultFraming = FramingMode.CrLfDelimited,
            Hint = "BLE でシリアル相当の通信をする一般的なサービスです。",
        },
        new LinkPreset("BLE: カスタム", LinkKind.Ble)
        {
            Service = NusService,
            WriteCharacteristic = NusRxWrite,
            NotifyCharacteristic = NusTxNotify,
            Profile = BleProfile.Raw,
            DefaultFraming = FramingMode.Raw,
            IsCustom = true,
            Hint = "サービス / 特性 UUID を直接入力します。",
        },
    };
}
