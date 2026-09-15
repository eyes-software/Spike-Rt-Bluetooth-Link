using System.Text;

namespace BluetoothLink.Core;

/// <summary>16 進文字列とバイト列の相互変換、ログ用の整形。</summary>
public static class HexUtil
{
    /// <summary>"48 65 6C 6C 6F" 形式の文字列を返す。</summary>
    public static string ToHex(ReadOnlySpan<byte> data, int maxBytes = int.MaxValue)
    {
        if (data.Length == 0)
        {
            return string.Empty;
        }

        var count = Math.Min(data.Length, maxBytes);
        var sb = new StringBuilder(count * 3);
        for (var i = 0; i < count; i++)
        {
            if (i > 0)
            {
                sb.Append(' ');
            }

            sb.Append(data[i].ToString("X2"));
        }

        if (count < data.Length)
        {
            sb.Append(" ...");
        }

        return sb.ToString();
    }

    /// <summary>
    /// 16 進文字列をバイト列に変換する。空白・改行・カンマ・"0x" プレフィックスは無視する。
    /// </summary>
    /// <exception cref="FormatException">16 進として解釈できない場合。</exception>
    public static byte[] FromHex(string text)
    {
        var cleaned = new StringBuilder(text.Length);
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (char.IsWhiteSpace(c) || c is ',' or '-' or ':' or '_')
            {
                continue;
            }

            if ((c is '0') && i + 1 < text.Length && (text[i + 1] is 'x' or 'X'))
            {
                i++;
                continue;
            }

            if (!Uri.IsHexDigit(c))
            {
                throw new FormatException($"16 進として解釈できない文字が含まれています: '{c}'");
            }

            cleaned.Append(c);
        }

        if (cleaned.Length % 2 != 0)
        {
            throw new FormatException("16 進文字列の桁数が奇数です。1 バイト = 2 桁で指定してください。");
        }

        var result = new byte[cleaned.Length / 2];
        for (var i = 0; i < result.Length; i++)
        {
            result[i] = Convert.ToByte(cleaned.ToString(i * 2, 2), 16);
        }

        return result;
    }

    /// <summary>ログ 1 行に埋め込めるよう、制御文字を '.' に置き換えた文字列を返す。</summary>
    public static string ToPrintable(string text)
    {
        var sb = new StringBuilder(text.Length);
        foreach (var c in text)
        {
            sb.Append(char.IsControl(c) ? '.' : c);
        }

        return sb.ToString();
    }
}
