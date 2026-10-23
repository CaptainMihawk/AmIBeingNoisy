using System.Text.Json;

namespace LoudEnv.Tray;

public sealed class AppSettings
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };

    public float Threshold { get; set; } = 0.20f;
    public string DeviceName { get; set; } = "";
    public bool ScheduleEnabled { get; set; } = true;
    public TimeOnly Start { get; set; } = new(22, 0);
    public TimeOnly End { get; set; } = new(7, 0);
    public string LogPath { get; set; } = "";
    public bool AlertSound { get; set; } = true;

    public static string FolderPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "LoudEnv");

    public static string FilePath => Path.Combine(FolderPath, "settings.json");

    public static AppSettings Load(out string? error)
    {
        error = null;
        try
        {
            if (File.Exists(FilePath))
            {
                var settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath), JsonOptions);
                if (settings is not null)
                    return settings.Normalize();
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            error = $"Não foi possível ler {FilePath}: {ex.Message}. Usando os padrões.";
        }

        return new AppSettings().Normalize();
    }

    public AppSettings Normalize()
    {
        if (Threshold < 0.01f || Threshold > 0.99f)
            Threshold = 0.20f;

        if (string.IsNullOrWhiteSpace(LogPath))
            LogPath = Path.Combine(FolderPath, "alertas.csv");

        return this;
    }

    public void Save()
    {
        Directory.CreateDirectory(FolderPath);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(this, JsonOptions));
    }
}
