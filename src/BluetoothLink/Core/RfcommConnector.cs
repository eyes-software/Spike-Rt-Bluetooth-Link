using InTheHand.Net;
using InTheHand.Net.Sockets;

namespace BluetoothLink.Core;

/// <summary>クライアント (接続) 機能。</summary>
public static class RfcommConnector
{
    /// <summary>
    /// 指定したデバイスのサービスへ RFCOMM 接続する。
    /// 未ペアリングの機器に対しては Windows 側でペアリング要求が出る場合がある。
    /// </summary>
    /// <param name="address">接続先の Bluetooth アドレス。</param>
    /// <param name="serviceId">サービス UUID。</param>
    /// <param name="framing">受信データの区切り方。</param>
    /// <param name="timeout">接続のタイムアウト。</param>
    public static async Task<ILinkSession> ConnectAsync(
        BluetoothAddress address,
        Guid serviceId,
        FramingMode framing,
        TimeSpan timeout)
    {
        var client = new BluetoothClient();

        try
        {
            var connectTask = client.ConnectAsync(address, serviceId);
            var completed = await Task.WhenAny(connectTask, Task.Delay(timeout)).ConfigureAwait(false);

            if (completed != connectTask)
            {
                throw new TimeoutException(
                    $"接続がタイムアウトしました ({timeout.TotalSeconds:0} 秒)。相手側が待ち受けているか確認してください。");
            }

            // 例外があればここで再スローされる。
            await connectTask.ConfigureAwait(false);

            return new RfcommSession(client, ConnectionRole.Client, framing);
        }
        catch
        {
            client.Dispose();
            throw;
        }
    }
}
