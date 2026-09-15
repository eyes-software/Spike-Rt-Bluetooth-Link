using System.Text;

namespace BluetoothLink;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        // Shift_JIS など .NET Core 既定では使えないコードページを有効化する。
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }
}
