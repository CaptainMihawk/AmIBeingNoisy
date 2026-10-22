using System.Globalization;
using System.Runtime.InteropServices;

namespace LoudEnv;

public static class Program
{
    private const int BarWidth = 20;

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

            var renderer = new InlineProgress<float>(RenderLevel);
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

    private static void RenderLevel(float value)
    {
        float clamped = Math.Clamp(value, 0f, 1f);
        int filled = (int)Math.Round(clamped * BarWidth);
        string bar = new string('#', filled) + new string('-', BarWidth - filled);
        Console.Write($"\r{clamped.ToString("0.000", CultureInfo.InvariantCulture)} [{bar}]");
    }

    private sealed class InlineProgress<T> : IProgress<T>
    {
        private readonly Action<T> _handler;

        public InlineProgress(Action<T> handler) => _handler = handler;

        public void Report(T value) => _handler(value);
    }
}
