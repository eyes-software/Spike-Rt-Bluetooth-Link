namespace BluetoothLink.Core;

/// <summary>データの流れる向き。ログの種別としても使用する。</summary>
public enum TransferDirection
{
    /// <summary>送信 (PC -> 相手機器)</summary>
    Tx,
    /// <summary>受信 (相手機器 -> PC)</summary>
    Rx,
    /// <summary>情報メッセージ</summary>
    Info,
    /// <summary>エラーメッセージ</summary>
    Error,
}

/// <summary>通信方式。</summary>
public enum LinkKind
{
    /// <summary>Bluetooth Classic の RFCOMM (SPP など)。</summary>
    Classic,

    /// <summary>Bluetooth Low Energy の GATT。</summary>
    Ble,
}

/// <summary>ストリーム上でメッセージをどう区切るか。</summary>
public enum FramingMode
{
    /// <summary>区切りなし。受信したチャンクをそのまま 1 件として扱う。</summary>
    Raw,

    /// <summary>LF 区切り。送信時は CRLF を付加し、受信時は末尾の CR を取り除く。</summary>
    CrLfDelimited,

    /// <summary>CR 区切り。SPIKE Hub の JSON プロトコルなど CR のみで終端する機器向け。</summary>
    CrDelimited,

    /// <summary>先頭 4 バイト (ビッグエンディアン) が本体長。</summary>
    LengthPrefixed,
}

/// <summary>BLE の特性上でペイロードをどう包むか。</summary>
public enum BleProfile
{
    /// <summary>加工しない。Nordic UART Service などの素の送受信。</summary>
    Raw,

    /// <summary>
    /// Pybricks の Command/Event 特性。送信は先頭に WRITE_STDIN(0x06) を付け、
    /// 受信は WRITE_STDOUT(0x01) のイベントだけを取り出す。
    /// </summary>
    PybricksStdio,
}

/// <summary>送信テキストの解釈方法。</summary>
public enum PayloadFormat
{
    /// <summary>文字列としてエンコードする。</summary>
    Text,

    /// <summary>"48 65 6C" のような 16 進表記としてバイト列に変換する。</summary>
    Hex,
}

/// <summary>接続の役割。</summary>
public enum ConnectionRole
{
    /// <summary>待ち受け側 (BluetoothListener)</summary>
    Server,

    /// <summary>接続側 (BluetoothClient / GATT クライアント)</summary>
    Client,
}
