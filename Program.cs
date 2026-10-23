using System.Runtime.InteropServices;

namespace LoudEnv;

public static class Program
{
    private const string Usage =
        "Uso: LoudEnv [--list | [--device <índice>] [--calibrate [--out <arquivo.csv>]]].";

    private sealed record Options(bool List, int? DeviceIndex, bool Calibrate, string? OutPath);

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
                    Console.WriteLine($"Gravando em {Path.GetFullPath(outPath)}");
                    Console.WriteLine("Teclas: 1 fala normal | 2 fala de jogo | 3 silêncio com jogo | 4 pausa | g grito (2 s)");
                    await CalibrationSession.RunAsync(monitor, outPath, cts.Token);
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
                default:
                    throw new ArgumentException(Usage);
            }
        }

        if (outPath is not null && !calibrate)
            throw new ArgumentException(Usage);

        return new Options(list, deviceIndex, calibrate, outPath);
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
}
