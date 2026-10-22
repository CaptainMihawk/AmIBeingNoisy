using System.Runtime.InteropServices;

namespace LoudEnv;

public static class Program
{
    public static int Main(string[] args)
    {
        try
        {
            if (args.Contains("--list"))
            {
                PrintCaptureEndpoints();
                return 0;
            }

            if (args.Length != 0)
            {
                throw new ArgumentException("Uso: LoudEnv [--list | --device <índice>].");
            }

            using var monitor = new MicLevelMonitor();
            Console.WriteLine($"Dispositivo de captura: {monitor.DeviceName}");
            return 0;
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException or COMException)
        {
            Console.Error.WriteLine($"Erro: {ex.Message}");
            return 1;
        }
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
}
