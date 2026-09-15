using InTheHand.Net;

namespace BluetoothLink.Core;

/// <summary>Bluetooth アドレスの表記ゆれを吸収する。</summary>
public static class AddressUtil
{
    /// <summary>"00:11:22:33:44:55" 形式に整形する。</summary>
    public static string Format(BluetoothAddress address)
    {
        try
        {
            return address.ToString("C");
        }
        catch (FormatException)
        {
            return address.ToString();
        }
    }

    /// <summary>
    /// "001122334455" / "00:11:22:33:44:55" などの文字列をアドレスに変換する。
    /// BLE 側 (InTheHand.Bluetooth) の Id は Windows では区切りなしの 12 桁 16 進になる。
    /// </summary>
    public static BluetoothAddress? TryParse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        return BluetoothAddress.TryParse(text.Trim(), out var address) ? address : null;
    }
}
