namespace Listener.Models;

public static class Format
{
    public static string Bytes(long value) => value >= 1_073_741_824
        ? $"{value / 1_073_741_824d:F1} GB"
        : value >= 1_048_576 ? $"{value / 1_048_576d:F1} MB" : $"{value / 1024d:F1} KB";
}
