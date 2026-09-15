using InTheHand.Net;
using InTheHand.Net.Bluetooth;
using InTheHand.Net.Sockets;

namespace BluetoothLink.Core;

/// <summary>Bluetooth Classic のデバイス検索とペアリング、およびアダプター情報。</summary>
public static class DeviceService
{
    /// <summary>ローカルの Bluetooth アダプターの状態を表す文字列を返す。</summary>
    public static string DescribeRadio()
    {
        try
        {
            var radio = BluetoothRadio.Default;
            if (radio is null)
            {
                return "Bluetooth アダプターが見つかりません。";
            }

            return $"アダプター: {radio.Name} / {AddressUtil.Format(radio.LocalAddress)} / 状態: {radio.Mode}";
        }
        catch (Exception ex)
        {
            return $"アダプター情報を取得できません: {ex.Message}";
        }
    }

    /// <summary>Bluetooth が利用可能かどうか。</summary>
    public static bool IsRadioAvailable()
    {
        try
        {
            return BluetoothRadio.Default is not null;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>周囲の Classic デバイスを検索する (数秒〜十数秒かかるブロッキング処理)。</summary>
    public static Task<List<DeviceEntry>> DiscoverAsync(int maxDevices = 30)
    {
        return Task.Run(() =>
        {
            using var client = new BluetoothClient();
            return client.DiscoverDevices(maxDevices).Select(DeviceEntry.FromClassic).ToList();
        });
    }

    /// <summary>ペアリング済みの Classic デバイスを返す。</summary>
    public static Task<List<DeviceEntry>> GetPairedAsync()
    {
        return Task.Run(() =>
        {
            using var client = new BluetoothClient();
            return client.PairedDevices.Select(DeviceEntry.FromClassic).ToList();
        });
    }

    /// <summary>
    /// ペアリングを要求する。PIN を指定しない場合は OS のペアリング UI が表示されることがある。
    /// </summary>
    public static Task<bool> PairAsync(BluetoothAddress address, string? pin)
    {
        return Task.Run(() =>
            BluetoothSecurity.PairRequest(address, string.IsNullOrEmpty(pin) ? null : pin, null));
    }

    /// <summary>ペアリングを解除する。Classic / BLE いずれもアドレスが分かれば解除できる。</summary>
    public static Task<bool> UnpairAsync(BluetoothAddress address)
    {
        return Task.Run(() => BluetoothSecurity.RemoveDevice(address));
    }
}
