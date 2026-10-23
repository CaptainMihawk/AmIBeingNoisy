using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;

namespace LoudEnv;

public static class Program
{
    private const string Usage =
        "Uso: LoudEnv [--list | [--device <índice>] [--calibrate [--out <arquivo.csv>] | --threshold <0-1> [--alert]]].";

    private const long AlertRepeatMs = 5000;

    private sealed record Options(bool List, int? DeviceIndex, bool Calibrate, string? OutPath, float? Threshold, bool Alert);

    public static async Task<int> Main(string[] args)
    {
        try
        {
            var options = ParseArgs(args);
            if (options.List)
            {
                PrintCaptureEndpoints();
                return 0;
            }

            using var monitor = new MicLevelMonitor(options.DeviceIndex);
            Console.WriteLine($"Dispositivo de captura: {monitor.DeviceName}");

            using var cts = new CancellationTokenSource();
            Console.CancelKeyPress += (_, e) =>
            {
                e.Cancel = true;
                cts.Cancel();
            };

            try
            {
                if (options.Calibrate)
                {
                    string outPath = options.OutPath ?? $"calibracao-{DateTime.Now:yyyyMMdd-HHmmss}.csv";
                    if (File.Exists(outPath))
                        throw new IOException($"O arquivo já existe: {outPath}. Escolha outro --out.");
                    Console.WriteLine($"Gravando em {Path.GetFullPath(outPath)}");
                    Console.WriteLine("Teclas: 1 fala normal | 2 fala de jogo | 3 silêncio com jogo | 4 pausa | g grito (2 s)");
                    await CalibrationSession.RunAsync(monitor, outPath, cts.Token);
                }
                else if (options.Threshold is float threshold)
                {
                    var clock = Stopwatch.StartNew();
                    var detector = new ThresholdDetector(threshold, options.Alert ? AlertRepeatMs : long.MaxValue);
                    Console.WriteLine(options.Alert
                        ? $"Alerta ligado: RMS >= {FormatValue(threshold)} por {ThresholdDetector.RequiredWindows} janelas, " +
                          "repetindo a cada 5 s enquanto continuar alto... (Ctrl+C para sair)"
                        : $"Testando limiar {FormatValue(threshold)} de RMS " +
                          $"({ThresholdDetector.RequiredWindows} janelas seguidas para disparar)... (Ctrl+C para sair)");
                    await monitor.RunAsync(
                        new InlineProgress<LevelReading>(reading => RenderThreshold(reading, threshold, detector, clock, options.Alert)),
                        cts.Token);
                }
                else
                {
                    Console.WriteLine("Monitorando o nível do microfone... (Ctrl+C para sair)");
                    await monitor.RunAsync(new InlineProgress<LevelReading>(RenderLevel), cts.Token);
                }
            }
            catch (OperationCanceledException)
            {
                // Ctrl+C: encerra sem erro.
            }

            Console.WriteLine();
            Console.WriteLine("Encerrado.");
            return 0;
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException or COMException
                                       or IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"Erro: {ex.Message}");
            return 1;
        }
    }

    private static Options ParseArgs(string[] args)
    {
        bool list = false;
        bool calibrate = false;
        int? deviceIndex = null;
        string? outPath = null;
        float? threshold = null;
        bool alert = false;

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--list":
                    list = true;
                    break;
                case "--calibrate":
                    calibrate = true;
                    break;
                case "--device" when i + 1 < args.Length && int.TryParse(args[i + 1], out int index):
                    deviceIndex = index;
                    i++;
                    break;
                case "--out" when i + 1 < args.Length:
                    outPath = args[++i];
                    break;
                case "--threshold" when i + 1 < args.Length && TryParseThreshold(args[i + 1], out float value):
                    threshold = value;
                    i++;
                    break;
                case "--alert":
                    alert = true;
                    break;
                default:
                    throw new ArgumentException(Usage);
            }
        }

        if (outPath is not null && !calibrate)
            throw new ArgumentException(Usage);

        if (calibrate && alert)
            throw new ArgumentException("--alert não combina com --calibrate.");

        if (calibrate && threshold is not null)
            throw new ArgumentException("--threshold não combina com --calibrate.");

        if (alert && threshold is null)
            throw new ArgumentException("--alert exige --threshold.");

        return new Options(list, deviceIndex, calibrate, outPath, threshold, alert);
    }

    private static bool TryParseThreshold(string text, out float value)
    {
        value = 0f;
        string normalized = text.Replace(',', '.');
        return float.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out value)
               && value > 0f && value < 1f;
    }

    private static void PrintCaptureEndpoints()
    {
        var endpoints = MicLevelMonitor.ListCaptureEndpoints();
        if (endpoints.Count == 0)
        {
            Console.WriteLine("Nenhum dispositivo de captura encontrado.");
            return;
        }
        foreach (var (index, name) in endpoints)
            Console.WriteLine($"[{index}] {name}");
    }

    private static void RenderLevel(LevelReading reading) =>
        Console.Write("\r" + LevelRenderer.Format(reading.Peak));

    private static void RenderThreshold(
        LevelReading reading, float threshold, ThresholdDetector detector, Stopwatch clock, bool alert)
    {
        if (detector.Sample(reading.Rms, clock.ElapsedMilliseconds))
        {
            if (alert)
                AlertSound.Play();

            Console.WriteLine();
            Console.WriteLine($"ALTO {DateTime.Now:HH:mm:ss} rms {FormatValue(reading.Rms)}");
        }

        string state = reading.Rms >= threshold ? "ACIMA" : "abaixo";
        Console.Write(
            $"\rrms {FormatValue(reading.Rms)} {LevelRenderer.Format(reading.Rms)} " +
            $"limiar {FormatValue(threshold)} {state}");
    }

    private static string FormatValue(float value) =>
        value.ToString("0.000", CultureInfo.InvariantCulture);
}
