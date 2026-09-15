using System.Text;
using InTheHand.Bluetooth;
using BleDevice = InTheHand.Bluetooth.BluetoothDevice;
using Uap = Windows.Devices.Bluetooth.GenericAttributeProfile;

namespace BluetoothLink.Core;

/// <summary>接続に必要な GATT の UUID 一式。</summary>
public sealed class BleEndpoint
{
    public required Guid Service { get; init; }

    public required Guid WriteCharacteristic { get; init; }

    public required Guid NotifyCharacteristic { get; init; }

    public BleProfile Profile { get; init; } = BleProfile.Raw;
}

/// <summary>接続の経過を画面へ伝えるためのコールバック。</summary>
public delegate void BleConnectLog(TransferDirection direction, string message);

/// <summary>BLE の GATT クライアントとして接続する。</summary>
public static class BleConnector
{
    public static async Task<ILinkSession> ConnectAsync(
        BleDevice device,
        BleEndpoint endpoint,
        FramingMode framing,
        TimeSpan timeout,
        BleConnectLog? log = null)
    {
        var connectTask = ConnectCoreAsync(device, endpoint, framing, log ?? ((_, _) => { }));
        var completed = await Task.WhenAny(connectTask, Task.Delay(timeout)).ConfigureAwait(false);

        if (completed != connectTask)
        {
            SafeDisconnect(device);
            throw new TimeoutException(
                $"接続がタイムアウトしました ({timeout.TotalSeconds:0} 秒)。ハブの電源と、対象のプログラムが動作しているかを確認してください。");
        }

        return await connectTask.ConfigureAwait(false);
    }

    private static async Task<ILinkSession> ConnectCoreAsync(
        BleDevice device,
        BleEndpoint endpoint,
        FramingMode framing,
        BleConnectLog log)
    {
        try
        {
            await device.Gatt.ConnectAsync().ConfigureAwait(false);

            if (!device.Gatt.IsConnected)
            {
                throw new InvalidOperationException("GATT サーバーに接続できませんでした。");
            }

            log(TransferDirection.Info, $"GATT 接続完了。MTU: {SafeMtu(device)}");

            var setup = endpoint.Profile == BleProfile.PybricksStdio
                ? await BuildPybricksSetupAsync(device, endpoint, framing, log).ConfigureAwait(false)
                : await BuildGenericSetupAsync(device, endpoint, framing, log).ConfigureAwait(false);

            var session = new BleSession(setup);
            session.Start();

            foreach (var source in setup.NotifySources)
            {
                await EnableNotificationsAsync(source, log).ConfigureAwait(false);
            }

            return session;
        }
        catch
        {
            SafeDisconnect(device);
            throw;
        }
    }

    // ------------------------------------------------------------------
    // 一般的な BLE (NUS など)
    // ------------------------------------------------------------------

    private static async Task<BleLinkSetup> BuildGenericSetupAsync(
        BleDevice device,
        BleEndpoint endpoint,
        FramingMode framing,
        BleConnectLog log)
    {
        var service = await GetServiceAsync(device, endpoint.Service).ConfigureAwait(false)
            ?? throw new InvalidOperationException(
                $"サービス {endpoint.Service:D} が見つかりません。UUID とファームウェアを確認してください。");

        var write = await GetCharacteristicAsync(service, endpoint.WriteCharacteristic).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"書き込み用の特性 {endpoint.WriteCharacteristic:D} が見つかりません。");

        var notify = endpoint.NotifyCharacteristic == endpoint.WriteCharacteristic
            ? write
            : await GetCharacteristicAsync(service, endpoint.NotifyCharacteristic).ConfigureAwait(false)
                ?? throw new InvalidOperationException($"通知用の特性 {endpoint.NotifyCharacteristic:D} が見つかりません。");

        EnsureWritable(write);
        EnsureNotifiable(notify);

        log(TransferDirection.Info, $"特性を解決しました。書込: {Describe(write)} / 通知: {Describe(notify)}");

        return new BleLinkSetup
        {
            Device = device,
            WriteCharacteristic = write,
            NotifySources = [new NotifySource { Characteristic = notify, IsPybricksEvent = false, Label = "通知" }],
            Profile = endpoint.Profile,
            Framing = framing,
        };
    }

    // ------------------------------------------------------------------
    // Pybricks
    // ------------------------------------------------------------------

    private static async Task<BleLinkSetup> BuildPybricksSetupAsync(
        BleDevice device,
        BleEndpoint endpoint,
        FramingMode framing,
        BleConnectLog log)
    {
        var service = await GetServiceAsync(device, endpoint.Service).ConfigureAwait(false)
            ?? throw new InvalidOperationException(
                $"Pybricks サービス {endpoint.Service:D} が見つかりません。ハブに Pybricks ファームウェアが入っているか確認してください。");

        var commandEvent = await GetCharacteristicAsync(service, endpoint.WriteCharacteristic).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Command/Event 特性 {endpoint.WriteCharacteristic:D} が見つかりません。");

        EnsureWritable(commandEvent);
        EnsureNotifiable(commandEvent);

        // ハブの素性を調べる。プロトコルバージョンで stdio の経路が変わる。
        var protocol = PybricksProtocol.ParseVersion(
            await ReadStringAsync(device, PybricksProtocol.DeviceInformationService, PybricksProtocol.SoftwareRevision).ConfigureAwait(false));
        var firmware = await ReadStringAsync(device, PybricksProtocol.DeviceInformationService, PybricksProtocol.FirmwareRevision).ConfigureAwait(false);

        PybricksHubCapabilities? capabilities = null;
        if (protocol is null || protocol >= PybricksProtocol.CapabilitiesVersion)
        {
            var raw = await ReadValueAsync(service, PybricksProtocol.HubCapabilities).ConfigureAwait(false);
            capabilities = PybricksProtocol.ParseCapabilities(raw);
        }

        log(TransferDirection.Info, PybricksProtocol.Describe(protocol, firmware, capabilities));

        var notifySources = new List<NotifySource>
        {
            new() { Characteristic = commandEvent, IsPybricksEvent = true, Label = "Pybricks Command/Event" },
        };

        // プロトコル v1.3.0 未満のファームは stdin/stdout を Nordic UART Service で扱う。
        var legacyStdio = protocol is not null && protocol < PybricksProtocol.StdioVersion;
        GattCharacteristic? nusWrite = null;

        var nusService = await GetServiceAsync(device, LinkPreset.NusService).ConfigureAwait(false);
        if (nusService is not null)
        {
            nusWrite = await GetCharacteristicAsync(nusService, LinkPreset.NusRxWrite).ConfigureAwait(false);
            var nusNotify = await GetCharacteristicAsync(nusService, LinkPreset.NusTxNotify).ConfigureAwait(false);

            if (nusNotify is not null)
            {
                // 新しいファームでも NUS が残っている場合があるので、両方から受信しておく。
                notifySources.Add(new NotifySource
                {
                    Characteristic = nusNotify,
                    IsPybricksEvent = false,
                    Label = "NUS TX",
                });
            }

            log(TransferDirection.Info, $"Nordic UART Service を検出しました (書込: {(nusWrite is null ? "なし" : "あり")} / 通知: {(nusNotify is null ? "なし" : "あり")})。");
        }
        else if (legacyStdio)
        {
            throw new InvalidOperationException(
                $"このファームウェア (プロトコル {protocol}) は stdio に Nordic UART Service を使いますが、そのサービスが見つかりません。");
        }

        // 送信先: 旧方式なら NUS RX、新方式なら Command/Event + WRITE_STDIN。
        var writeCharacteristic = legacyStdio && nusWrite is not null ? nusWrite : commandEvent;
        var writePrefix = ReferenceEquals(writeCharacteristic, commandEvent)
            ? (byte)PybricksCommand.WriteStdin
            : (byte?)null;

        if (legacyStdio)
        {
            log(TransferDirection.Info, "旧方式のため、送信は NUS RX 特性へ行います。");
        }

        return new BleLinkSetup
        {
            Device = device,
            WriteCharacteristic = writeCharacteristic,
            WritePrefix = writePrefix,
            NotifySources = notifySources,
            ControlCharacteristic = commandEvent,
            Profile = endpoint.Profile,
            Framing = framing,
            MaxWriteSize = capabilities?.MaxWriteSize ?? 0,
        };
    }

    // ------------------------------------------------------------------
    // GATT ヘルパー
    // ------------------------------------------------------------------

    /// <summary>
    /// 通知を購読する。InTheHand の StartNotificationsAsync は CCCD 書き込みの失敗を
    /// 握りつぶすため、ネイティブ側の戻り値でも確認する。
    /// </summary>
    private static async Task EnableNotificationsAsync(NotifySource source, BleConnectLog log)
    {
        await source.Characteristic.StartNotificationsAsync().ConfigureAwait(false);

        try
        {
            Uap.GattCharacteristic native = source.Characteristic;
            var value = native.CharacteristicProperties.HasFlag(Uap.GattCharacteristicProperties.Notify)
                ? Uap.GattClientCharacteristicConfigurationDescriptorValue.Notify
                : Uap.GattClientCharacteristicConfigurationDescriptorValue.Indicate;

            var status = await native.WriteClientCharacteristicConfigurationDescriptorAsync(value);
            if (status != Uap.GattCommunicationStatus.Success)
            {
                throw new InvalidOperationException(
                    $"{source.Label} の通知を有効にできませんでした (状態: {status})。他のアプリがハブを掴んでいないか確認してください。");
            }

            log(TransferDirection.Info, $"{source.Label} の通知を有効にしました。");
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new InvalidOperationException(
                $"{source.Label} の通知を有効にできませんでした。他のアプリ (Pybricks Code など) がハブに接続している可能性があります。", ex);
        }
    }

    private static async Task<GattService?> GetServiceAsync(BleDevice device, Guid uuid)
    {
        try
        {
            return await device.Gatt.GetPrimaryServiceAsync(uuid).ConfigureAwait(false);
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static async Task<GattCharacteristic?> GetCharacteristicAsync(GattService service, Guid uuid)
    {
        try
        {
            return await service.GetCharacteristicAsync(uuid).ConfigureAwait(false);
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static async Task<byte[]?> ReadValueAsync(GattService service, Guid characteristic)
    {
        var target = await GetCharacteristicAsync(service, characteristic).ConfigureAwait(false);
        if (target is null)
        {
            return null;
        }

        try
        {
            return await target.ReadValueAsync().ConfigureAwait(false);
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static async Task<string?> ReadStringAsync(BleDevice device, Guid service, Guid characteristic)
    {
        var target = await GetServiceAsync(device, service).ConfigureAwait(false);
        if (target is null)
        {
            return null;
        }

        var value = await ReadValueAsync(target, characteristic).ConfigureAwait(false);
        return value is null ? null : Encoding.UTF8.GetString(value).TrimEnd('\0').Trim();
    }

    private static void EnsureWritable(GattCharacteristic characteristic)
    {
        const GattCharacteristicProperties writable =
            GattCharacteristicProperties.Write | GattCharacteristicProperties.WriteWithoutResponse;

        if ((characteristic.Properties & writable) == 0)
        {
            throw new InvalidOperationException(
                $"特性 {characteristic.Uuid} は書き込みに対応していません (Properties: {characteristic.Properties})。");
        }
    }

    private static void EnsureNotifiable(GattCharacteristic characteristic)
    {
        const GattCharacteristicProperties notifiable =
            GattCharacteristicProperties.Notify | GattCharacteristicProperties.Indicate;

        if ((characteristic.Properties & notifiable) == 0)
        {
            throw new InvalidOperationException(
                $"特性 {characteristic.Uuid} は通知に対応していません (Properties: {characteristic.Properties})。");
        }
    }

    private static string Describe(GattCharacteristic characteristic) =>
        $"{characteristic.Uuid} ({characteristic.Properties})";

    private static string SafeMtu(BleDevice device)
    {
        try
        {
            var mtu = device.Gatt?.Mtu ?? 0;
            return mtu > 0 ? mtu.ToString() : "不明";
        }
        catch (Exception)
        {
            return "不明";
        }
    }

    private static void SafeDisconnect(BleDevice device)
    {
        try
        {
            device.Gatt?.Disconnect();
        }
        catch (Exception)
        {
            // 接続前・切断済みの場合は無視してよい。
        }
    }
}
