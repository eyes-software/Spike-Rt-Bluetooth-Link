using InTheHand.Bluetooth;
using BleDevice = InTheHand.Bluetooth.BluetoothDevice;

namespace BluetoothLink.Core;

/// <summary>BLE のデバイス検索・ペアリング・接続。</summary>
public static class BleService
{
    /// <summary>BLE が利用可能か。</summary>
    public static async Task<bool> IsAvailableAsync()
    {
        try
        {
            return await Bluetooth.GetAvailabilityAsync().ConfigureAwait(false);
        }
        catch (Exception)
        {
            return false;
        }
    }

    // 周囲の機器を探す処理は BleScanner (WinRT のアドバタイズ受信) 側にある。
    // ライブラリの ScanForDevicesAsync は指定時間ぶん待ってから全件返す方式で待たされるため使わない。

    /// <summary>ペアリング済みの BLE デバイスを返す。</summary>
    public static async Task<List<DeviceEntry>> GetPairedAsync()
    {
        var devices = await Bluetooth.GetPairedDevicesAsync().ConfigureAwait(false);
        return devices.Select(DeviceEntry.FromBle).ToList();
    }

    /// <summary>
    /// ペアリングする。BLE は接続時に自動でペアリングされる機器も多く、
    /// Pybricks のハブのようにペアリング不要でつながるものもある。
    /// </summary>
    public static async Task<bool> PairAsync(BleDevice device, string? pin)
    {
        if (device.IsPaired)
        {
            return true;
        }

        if (string.IsNullOrEmpty(pin))
        {
            await device.PairAsync().ConfigureAwait(false);
        }
        else
        {
            await device.PairAsync(pin).ConfigureAwait(false);
        }

        return device.IsPaired;
    }
}
