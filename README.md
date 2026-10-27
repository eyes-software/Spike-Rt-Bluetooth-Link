# BluetoothLink

PC 上で動作する Bluetooth データ送受信ツール (C# / WinForms)。
2026年の[ETロボコン](https://www.etrobo.jp/) ベーシッククラスの走行体「SPIKE-RT」に使われている、LEGO SPIKE Prime のラージハブに搭載されているBluetoothとの接続に用いたものです。
実際には動かしたことがない機能も含んで、現状雄姿のままとなります。

詳細は、src/BluetoothLink/README.md を参照してください。

**Bluetooth Classic (RFCOMM/SPP)** と **BLE (GATT)** の両方に対応し、以下の 3 機能を提供します。

1. **ペアリング** — デバイス検索、ペアリング / ペアリング解除
2. **サーバー / クライアント接続と双方向のデータ送受信**
3. **送受信データのファイル書き出し**

## 動作要件

| 項目 | 内容 |
| --- | --- |
| OS | Windows 10 バージョン 2004 (build 19041) 以降 / Windows 11 |
| ランタイム | .NET 10 デスクトップランタイム |
| ハードウェア | Bluetooth アダプター (BLE を使う場合は BLE 対応のもの) |
| ライブラリ | [32feet.NET](https://github.com/inthehand/32feet) `InTheHand.Net.Bluetooth` 4.2.4 / `InTheHand.BluetoothLE` 4.0.44 |

