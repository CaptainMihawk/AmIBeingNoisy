using System.Runtime.InteropServices;

namespace LoudEnv;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        try
        {
            if (args.Contains("--list"))
            {
                PrintCaptureEndpoints();
                return 0;
            }

            int? deviceIndex = ParseDeviceIndex(args);
            using var monitor = new MicLevelMonitor(deviceIndex);

            Console.WriteLine($"Dispositivo de captura: {monitor.DeviceName}");
            Console.WriteLine("Monitorando o nível do microfone... (Ctrl+C para sair)");

            using var cts = new CancellationTokenSource();
            Console.CancelKeyPress += (_, e) =>
            {
                e.Cancel = true;
                cts.Cancel();
            };

            var renderer = new InlineProgress<LevelReading>(RenderLevel);
            try
            {
                await monitor.RunAsync(renderer, cts.Token);
            }
            catch (OperationCanceledException)
            {
                // Ctrl+C: encerra sem erro.
            }

            Console.WriteLine();
            Console.WriteLine("Encerrado.");
            return 0;
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException or COMException)
        {
            Console.Error.WriteLine($"Erro: {ex.Message}");
            return 1;
        }
    }

    private static int? ParseDeviceIndex(string[] args)
    {
        if (args.Length == 0)
            return null;
        if (args.Length == 2 && args[0] == "--device" && int.TryParse(args[1], out int index))
            return index;
        throw new ArgumentException("Uso: LoudEnv [--list | --device <índice>].");
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
