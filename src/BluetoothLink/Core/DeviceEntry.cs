using InTheHand.Net;
using InTheHand.Net.Sockets;
using BleDevice = InTheHand.Bluetooth.BluetoothDevice;

namespace BluetoothLink.Core;

/// <summary>
/// 一覧に表示するデバイス 1 件。Classic (BluetoothDeviceInfo)、BLE (BluetoothDevice)、
/// および BLE のアドバタイズだけが見えている状態を、同じ形で扱えるようにするラッパー。
/// </summary>
public sealed class DeviceEntry
{
    private DeviceEntry(LinkKind kind, string name, BluetoothAddress? address)
    {
        Kind = kind;
        Name = string.IsNullOrWhiteSpace(name) ? "(名前なし)" : name;
        HardwareAddress = address;
        Address = address is null ? "-" : AddressUtil.Format(address);
    }

    public LinkKind Kind { get; }

    public string Name { get; }

    /// <summary>表示用のアドレス文字列。</summary>
    public string Address { get; }

    /// <summary>アドレス。取得できなかった場合は null。</summary>
    public BluetoothAddress? HardwareAddress { get; }

    public bool Paired { get; private set; }

    public bool Connected { get; private set; }

    /// <summary>デバイス種別などの補足情報。</summary>
    public string Category { get; private set; } = "-";

    public BluetoothDeviceInfo? ClassicDevice { get; private init; }

    public BleDevice? BleDevice { get; private set; }

    /// <summary>アドバタイズ由来の場合の生アドレス。BLE デバイスを取り出すのに使う。</summary>
    public ulong? BleAddressValue { get; private init; }

    /// <summary>電波強度 (dBm)。アドバタイズ由来のときのみ。</summary>
    public short? Rssi { get; private init; }

    /// <summary>アドバタイズに含まれていたサービス UUID。</summary>
    public IReadOnlyList<Guid> AdvertisedServices { get; private init; } = [];

    public static DeviceEntry FromClassic(BluetoothDeviceInfo device)
    {
        return new DeviceEntry(LinkKind.Classic, device.DeviceName, device.DeviceAddress)
        {
            ClassicDevice = device,
            Paired = device.Authenticated,
            Connected = device.Connected,
            Category = DescribeClass(device),
        };
    }

    public static DeviceEntry FromBle(BleDevice device)
    {
        // Windows では Id が区切りなしのアドレス文字列になる。
        var address = AddressUtil.TryParse(device.Id);

        return new DeviceEntry(LinkKind.Ble, device.Name ?? string.Empty, address)
        {
            BleDevice = device,
            BleAddressValue = address is null ? null : ToUInt64(address),
            Paired = device.IsPaired,
            Connected = device.Gatt?.IsConnected ?? false,
            Category = address is null ? $"BLE (Id: {device.Id})" : "BLE",
        };
    }

    /// <summary>アドバタイズ 1 件から作る。接続時に <see cref="ResolveBleDeviceAsync"/> で実体化する。</summary>
    public static DeviceEntry FromAdvertisement(ulong address, string name, short rssi, IReadOnlyList<Guid> services)
    {
        var parsed = new BluetoothAddress(address);

        return new DeviceEntry(LinkKind.Ble, name, parsed)
        {
            BleAddressValue = address,
            Rssi = rssi,
            AdvertisedServices = services,
            Category = DescribeAdvertisement(rssi, services),
        };
    }

    /// <summary>同じ機器から後続のアドバタイズが来たときに、新しい情報で作り直す。</summary>
    public DeviceEntry MergeAdvertisement(string name, short rssi, IReadOnlyList<Guid> services)
    {
        var betterName = string.IsNullOrWhiteSpace(name) ? null : name;
        var moreServices = services.Count > AdvertisedServices.Count ? services : AdvertisedServices;
        var nameChanged = betterName is not null && betterName != Name;
        var servicesChanged = !ReferenceEquals(moreServices, AdvertisedServices);

        // 電波強度は常に更新するが、それだけの変化では画面を作り直さない。
        if (!nameChanged && !servicesChanged)
        {
            Category = DescribeAdvertisement(rssi, AdvertisedServices);
            return this;
        }

        return new DeviceEntry(LinkKind.Ble, betterName ?? (Name == "(名前なし)" ? string.Empty : Name), HardwareAddress)
        {
            BleDevice = BleDevice,
            BleAddressValue = BleAddressValue,
            Rssi = rssi,
            AdvertisedServices = moreServices,
            Paired = Paired,
            Connected = Connected,
            Category = DescribeAdvertisement(rssi, moreServices),
        };
    }

    /// <summary>接続に使う BLE デバイスを取り出す。アドバタイズ由来なら実体化する。</summary>
    public async Task<BleDevice?> ResolveBleDeviceAsync()
    {
        if (BleDevice is not null)
        {
            return BleDevice;
        }

        if (BleAddressValue is not { } address)
        {
            return null;
        }

        var resolved = await BleScanner.ResolveAsync(address).ConfigureAwait(false);
        if (resolved is not null)
        {
            BleDevice = resolved;
            Paired = resolved.IsPaired;
        }

        return resolved;
    }

    /// <summary>OS から最新の状態を取り直す。</summary>
    public void Refresh()
    {
        try
        {
            if (ClassicDevice is not null)
            {
                ClassicDevice.Refresh();
                Paired = ClassicDevice.Authenticated;
                Connected = ClassicDevice.Connected;
            }
            else if (BleDevice is not null)
            {
                Paired = BleDevice.IsPaired;
                Connected = BleDevice.Gatt?.IsConnected ?? false;
            }
        }
        catch (Exception)
        {
            // 取得できないスタックもあるため無視する。
        }
    }

    private static string DescribeAdvertisement(short rssi, IReadOnlyList<Guid> services)
    {
        var text = $"BLE  {rssi} dBm";

        if (services.Contains(PybricksProtocol.Service))
        {
            text += "  [Pybricks]";
        }

        if (services.Contains(LinkPreset.NusService))
        {
            text += "  [NUS]";
        }

        return text;
    }

    private static ulong ToUInt64(BluetoothAddress address)
    {
        // BluetoothAddress は 6 バイト。下位から詰め直す。
        var bytes = address.ToByteArray();
        ulong value = 0;
        for (var i = 0; i < 6 && i < bytes.Length; i++)
        {
            value |= (ulong)bytes[i] << (8 * i);
        }

        return value;
    }

    private static string DescribeClass(BluetoothDeviceInfo device)
    {
        try
        {
            var cod = device.ClassOfDevice;
            return $"{cod.MajorDevice} / {cod.Device}";
        }
        catch (Exception)
        {
            return "-";
        }
    }
}
