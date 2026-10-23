using System.Globalization;

namespace LoudEnv;

public static class LevelRenderer
{
    private const int BarWidth = 20;

    public static string Format(float value)
    {
        float clamped = Math.Clamp(value, 0f, 1f);
        int filled = (int)Math.Round(clamped * BarWidth);
        string bar = new string('#', filled) + new string('-', BarWidth - filled);
        return $"{clamped.ToString("0.000", CultureInfo.InvariantCulture)} [{bar}]";
    }
}
