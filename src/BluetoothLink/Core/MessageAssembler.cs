using System.Buffers.Binary;

namespace BluetoothLink.Core;

/// <summary>
/// 受信したバイトストリームを <see cref="FramingMode"/> に従ってメッセージ単位に切り出す。
/// RFCOMM はストリーム指向で、送信側の 1 回の Write が受信側の 1 回の Read に対応する保証がないため、
/// 区切りが必要な用途ではこのクラスでバッファリングする。
/// BLE の通知も MTU で分割されて届くため、同じ仕組みで組み立て直す。
/// </summary>
public sealed class MessageAssembler
{
    /// <summary>1 メッセージの上限。異常データでメモリを食いつぶさないためのガード。</summary>
    public const int MaxMessageLength = 16 * 1024 * 1024;

    private readonly FramingMode _mode;
    private readonly List<byte> _buffer = new();

    public MessageAssembler(FramingMode mode)
    {
        _mode = mode;
    }

    /// <summary>受信チャンクを投入し、完成したメッセージを列挙する。</summary>
    public IEnumerable<byte[]> Push(byte[] chunk, int count)
    {
        if (count <= 0)
        {
            yield break;
        }

        if (_mode == FramingMode.Raw)
        {
            // 区切りなし: 受け取ったチャンクをそのまま 1 件として返す。
            var raw = new byte[count];
            Array.Copy(chunk, raw, count);
            yield return raw;
            yield break;
        }

        for (var i = 0; i < count; i++)
        {
            _buffer.Add(chunk[i]);
        }

        while (true)
        {
            var message = TryTake();
            if (message is null)
            {
                yield break;
            }

            yield return message;
        }
    }

    /// <summary>接続終了時に、区切り文字が来ないまま残っているデータを取り出す。</summary>
    public byte[]? Flush()
    {
        if (_buffer.Count == 0)
        {
            return null;
        }

        var rest = _buffer.ToArray();
        _buffer.Clear();
        return rest;
    }

    private byte[]? TryTake()
    {
        switch (_mode)
        {
            case FramingMode.CrLfDelimited:
                // LF までを 1 件とし、CRLF の CR は取り除く。
                return TryTakeDelimited((byte)'\n', stripTrailingCr: true);

            case FramingMode.CrDelimited:
                // SPIKE Hub の JSON プロトコルなど、CR だけで終端する機器向け。
                return TryTakeDelimited((byte)'\r', stripTrailingCr: false);

            case FramingMode.LengthPrefixed:
            {
                if (_buffer.Count < 4)
                {
                    return null;
                }

                Span<byte> header = stackalloc byte[4];
                for (var i = 0; i < 4; i++)
                {
                    header[i] = _buffer[i];
                }

                var length = BinaryPrimitives.ReadInt32BigEndian(header);
                if (length < 0 || length > MaxMessageLength)
                {
                    throw new InvalidDataException($"長さヘッダーが不正です: {length}");
                }

                if (_buffer.Count < 4 + length)
                {
                    return null;
                }

                var body = new byte[length];
                _buffer.CopyTo(4, body, 0, length);
                _buffer.RemoveRange(0, 4 + length);
                return body;
            }

            default:
                return null;
        }
    }

    private byte[]? TryTakeDelimited(byte delimiter, bool stripTrailingCr)
    {
        var index = _buffer.IndexOf(delimiter);
        if (index < 0)
        {
            if (_buffer.Count > MaxMessageLength)
            {
                throw new InvalidDataException("区切り文字が現れないまま受信データが上限を超えました。");
            }

            return null;
        }

        var length = index;
        if (stripTrailingCr && length > 0 && _buffer[length - 1] == (byte)'\r')
        {
            length--;
        }

        var line = new byte[length];
        _buffer.CopyTo(0, line, 0, length);
        _buffer.RemoveRange(0, index + 1);
        return line;
    }

    /// <summary>送信データに区切り情報を付加する。</summary>
    public static byte[] Frame(byte[] payload, FramingMode mode)
    {
        switch (mode)
        {
            case FramingMode.CrLfDelimited:
            {
                var framed = new byte[payload.Length + 2];
                Array.Copy(payload, framed, payload.Length);
                framed[^2] = (byte)'\r';
                framed[^1] = (byte)'\n';
                return framed;
            }

            case FramingMode.CrDelimited:
            {
                var framed = new byte[payload.Length + 1];
                Array.Copy(payload, framed, payload.Length);
                framed[^1] = (byte)'\r';
                return framed;
            }

            case FramingMode.LengthPrefixed:
            {
                var framed = new byte[payload.Length + 4];
                BinaryPrimitives.WriteInt32BigEndian(framed.AsSpan(0, 4), payload.Length);
                Array.Copy(payload, 0, framed, 4, payload.Length);
                return framed;
            }

            default:
                return payload;
        }
    }

    /// <summary>画面やログに出す短い名前。</summary>
    public static string DescribeFraming(FramingMode mode) => mode switch
    {
        FramingMode.CrLfDelimited => "改行区切り(CRLF)",
        FramingMode.CrDelimited => "CR区切り",
        FramingMode.LengthPrefixed => "長さ付き",
        _ => "区切りなし",
    };
}
