using System.Buffers.Binary;
using System.Text;

namespace BluetoothLink.Core;

/// <summary>ハブへ送るコマンド (Command/Event 特性の先頭バイト)。</summary>
public enum PybricksCommand : byte
{
    StopUserProgram = 0,
    StartUserProgram = 1,
    StartRepl = 2,

    /// <summary>ハブの stdin へ書き込む。プロトコル v1.3.0 以降。</summary>
    WriteStdin = 6,
}

/// <summary>ハブから届くイベント (通知の先頭バイト)。</summary>
public enum PybricksEvent : byte
{
    StatusReport = 0,

    /// <summary>ハブの stdout 出力。プロトコル v1.3.0 以降。</summary>
    WriteStdout = 1,
}

/// <summary>ステータスレポートのビットフラグ。</summary>
[Flags]
public enum PybricksStatus : uint
{
    None = 0,
    BatteryLowVoltageWarning = 1u << 0,
    BatteryLowVoltageShutdown = 1u << 1,
    BatteryHighCurrent = 1u << 2,
    BleAdvertising = 1u << 3,
    BleLowSignal = 1u << 4,
    PowerButtonPressed = 1u << 5,

    /// <summary>ユーザープログラムが実行中。これが立たないと stdin/stdout は流れない。</summary>
    UserProgramRunning = 1u << 6,

    Shutdown = 1u << 7,
    ShutdownRequested = 1u << 8,

    /// <summary>ハブが「ホストと接続済み」と認識している。</summary>
    BleHostConnected = 1u << 9,
}

/// <summary>ステータスレポート 1 件。</summary>
public sealed class PybricksStatusReport
{
    public required PybricksStatus Flags { get; init; }

    /// <summary>実行中のプログラム番号。プロトコル v1.4.0 以降のみ。</summary>
    public byte? RunningProgram { get; init; }

    public bool UserProgramRunning => (Flags & PybricksStatus.UserProgramRunning) != 0;

    public bool BleHostConnected => (Flags & PybricksStatus.BleHostConnected) != 0;

    /// <summary>ログに出す 1 行。</summary>
    public string Describe()
    {
        var names = Flags == PybricksStatus.None ? "(なし)" : Flags.ToString();
        var program = RunningProgram is null ? string.Empty : $" / プログラム番号: {RunningProgram}";
        return $"ハブの状態: 0x{(uint)Flags:X8} [{names}]{program}";
    }
}

/// <summary>Hub Capabilities 特性の内容。</summary>
public sealed class PybricksHubCapabilities
{
    public required int MaxWriteSize { get; init; }

    public required uint Flags { get; init; }

    public required int MaxUserProgramSize { get; init; }

    /// <summary>プログラムスロット数。0 ならスロット非対応。</summary>
    public int SlotCount { get; init; }
}

/// <summary>Pybricks の BLE プロトコル定義。UUID と、バイト列の組み立て / 解釈。</summary>
public static class PybricksProtocol
{
    public static readonly Guid Service = new("c5f50001-8280-46da-89f4-6d8051e4aeef");

    public static readonly Guid CommandEvent = new("c5f50002-8280-46da-89f4-6d8051e4aeef");

    public static readonly Guid HubCapabilities = new("c5f50003-8280-46da-89f4-6d8051e4aeef");

    /// <summary>Device Information Service。</summary>
    public static readonly Guid DeviceInformationService = FromShortUuid(0x180A);

    /// <summary>Firmware Revision String。ファームウェアのバージョン。</summary>
    public static readonly Guid FirmwareRevision = FromShortUuid(0x2A26);

    /// <summary>Software Revision String。Pybricks ではプロトコルバージョンそのものが入る。</summary>
    public static readonly Guid SoftwareRevision = FromShortUuid(0x2A28);

    /// <summary>WRITE_STDIN / WRITE_STDOUT が使えるようになったプロトコルバージョン。</summary>
    public static readonly Version StdioVersion = new(1, 3, 0);

    /// <summary>Hub Capabilities 特性が追加されたプロトコルバージョン。</summary>
    public static readonly Version CapabilitiesVersion = new(1, 2, 0);

    /// <summary>ステータスレポートに実行中プログラム番号が付くようになったバージョン。</summary>
    public static readonly Version RunningProgramVersion = new(1, 4, 0);

    /// <summary>16bit の短縮 UUID を 128bit へ展開する。</summary>
    public static Guid FromShortUuid(ushort id) =>
        new($"{id:X4}".PadLeft(8, '0') + "-0000-1000-8000-00805F9B34FB");

    /// <summary>"1.3.0" のようなバージョン文字列を解釈する。解釈できなければ null。</summary>
    public static Version? ParseVersion(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        // "1.3.0-beta.1" のような semver の接尾辞は落とす。
        var core = new string(text.Trim().TakeWhile(c => char.IsDigit(c) || c == '.').ToArray()).Trim('.');
        return Version.TryParse(core, out var version) ? version : null;
    }

    /// <summary>ステータスレポートの通知を解釈する。別のイベントなら null。</summary>
    public static PybricksStatusReport? ParseStatusReport(byte[]? value)
    {
        if (value is null || value.Length < 5 || value[0] != (byte)PybricksEvent.StatusReport)
        {
            return null;
        }

        var flags = BinaryPrimitives.ReadUInt32LittleEndian(value.AsSpan(1, 4));

        return new PybricksStatusReport
        {
            Flags = (PybricksStatus)flags,
            RunningProgram = value.Length >= 6 ? value[5] : null,
        };
    }

    /// <summary>
    /// Hub Capabilities 特性の値を解釈する。
    /// 構造は uint16 最大書き込みサイズ / uint32 機能フラグ / uint32 最大プログラムサイズ
    /// (プロトコル v1.5.0 以降は uint8 スロット数が続く)。
    /// </summary>
    public static PybricksHubCapabilities? ParseCapabilities(byte[]? value)
    {
        if (value is null || value.Length < 10)
        {
            return null;
        }

        return new PybricksHubCapabilities
        {
            MaxWriteSize = BinaryPrimitives.ReadUInt16LittleEndian(value.AsSpan(0, 2)),
            Flags = BinaryPrimitives.ReadUInt32LittleEndian(value.AsSpan(2, 4)),
            MaxUserProgramSize = (int)BinaryPrimitives.ReadUInt32LittleEndian(value.AsSpan(6, 4)),
            SlotCount = value.Length >= 11 ? value[10] : 0,
        };
    }

    /// <summary>制御コマンドのバイト列を組み立てる。</summary>
    public static byte[] BuildCommand(PybricksCommand command, byte? parameter = null) =>
        parameter is null ? [(byte)command] : [(byte)command, parameter.Value];

    /// <summary>ハブの情報をログ 1 行にまとめる。</summary>
    public static string Describe(Version? protocol, string? firmware, PybricksHubCapabilities? capabilities)
    {
        var sb = new StringBuilder("Pybricks ハブ情報:");
        sb.Append($" プロトコル {protocol?.ToString() ?? "不明"}");
        sb.Append($" / ファーム {(string.IsNullOrWhiteSpace(firmware) ? "不明" : firmware)}");

        if (capabilities is not null)
        {
            sb.Append($" / 最大書き込み {capabilities.MaxWriteSize} バイト");
            sb.Append($" / スロット数 {capabilities.SlotCount}");
        }

        var legacy = protocol is not null && protocol < StdioVersion;
        sb.Append(legacy ? " / stdio は NUS 経由 (旧方式)" : " / stdio は Pybricks 特性経由");
        return sb.ToString();
    }
}
