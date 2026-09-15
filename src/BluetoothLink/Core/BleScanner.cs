using System.Collections.Concurrent;
using Windows.Devices.Bluetooth.Advertisement;
using WinBle = Windows.Devices.Bluetooth.BluetoothLEDevice;

namespace BluetoothLink.Core;

/// <summary>
/// BLE のアドバタイズを受信し続けるスキャナー。
/// ライブラリの ScanForDevicesAsync は「指定時間ぶん待ってから全件返す」方式で待たされるため、
/// アドバタイズを 1 件受けるたびに結果へ反映できる WinRT のウォッチャーを直接使う。
/// </summary>
public sealed class BleScanner : IDisposable
{
    private readonly BluetoothLEAdvertisementWatcher _watcher;
    private readonly ConcurrentDictionary<ulong, DeviceEntry> _devices = new();

    // 見つかった順を保つための並び (RSSI で並べ替えると選択中の行が動いてしまう)。
    private readonly ConcurrentQueue<ulong> _order = new();

    private long _version;
    private int _started;
    private bool _disposed;

    public BleScanner()
    {
        _watcher = new BluetoothLEAdvertisementWatcher
        {
            // Active にするとスキャン応答も受け取れるため、機器名が得られやすい。
            ScanningMode = BluetoothLEScanningMode.Active,
        };

        _watcher.Received += OnReceived;

        // 電波が切られた場合など、ウォッチャーが自分で止まることがある。
        _watcher.Stopped += (_, _) => Volatile.Write(ref _started, 0);
    }

    /// <summary>結果が変わるたびに増える。画面側はこの値を見て再描画すればよい。</summary>
    public long Version => Interlocked.Read(ref _version);

    /// <summary>
    /// スキャン中かどうか。Start() 直後は <c>Watcher.Status</c> がまだ Started にならないことがあるため、
    /// 画面表示がぶれないよう自前のフラグで持つ。
    /// </summary>
    public bool IsScanning => Volatile.Read(ref _started) != 0;

    public int Count => _devices.Count;

    public void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (IsScanning)
        {
            return;
        }

        _devices.Clear();
        while (_order.TryDequeue(out _))
        {
        }

        Interlocked.Increment(ref _version);
        _watcher.Start();
        Volatile.Write(ref _started, 1);
    }

    public void Stop()
    {
        Volatile.Write(ref _started, 0);

        if (_watcher.Status is BluetoothLEAdvertisementWatcherStatus.Started)
        {
            _watcher.Stop();
        }
    }

    /// <summary>見つかった順に並べた現在の一覧。</summary>
    public List<DeviceEntry> Snapshot()
    {
        var result = new List<DeviceEntry>(_devices.Count);
        var seen = new HashSet<ulong>();

        foreach (var address in _order)
        {
            if (seen.Add(address) && _devices.TryGetValue(address, out var entry))
            {
                result.Add(entry);
            }
        }

        return result;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _watcher.Received -= OnReceived;
        Stop();
    }

    private void OnReceived(BluetoothLEAdvertisementWatcher sender, BluetoothLEAdvertisementReceivedEventArgs args)
    {
        var address = args.BluetoothAddress;
        var name = args.Advertisement.LocalName ?? string.Empty;
        var services = args.Advertisement.ServiceUuids.ToArray();

        _devices.AddOrUpdate(
            address,
            _ =>
            {
                _order.Enqueue(address);
                Interlocked.Increment(ref _version);
                return DeviceEntry.FromAdvertisement(address, name, args.RawSignalStrengthInDBm, services);
            },
            (_, existing) =>
            {
                // 同じ機器から繰り返し届く。名前やサービスは後続のスキャン応答で埋まることがある。
                var merged = existing.MergeAdvertisement(name, args.RawSignalStrengthInDBm, services);
                if (!ReferenceEquals(merged, existing))
                {
                    Interlocked.Increment(ref _version);
                }

                return merged;
            });
    }

    /// <summary>アドレスから実際の BLE デバイスを取り出す。接続直前に呼ぶ。</summary>
    internal static async Task<InTheHand.Bluetooth.BluetoothDevice?> ResolveAsync(ulong address)
    {
        var native = await WinBle.FromBluetoothAddressAsync(address);
        if (native is null)
        {
            return null;
        }

        // ライブラリ側に用意されているネイティブ型からの暗黙変換を使う。
        return (InTheHand.Bluetooth.BluetoothDevice)native;
    }
}
