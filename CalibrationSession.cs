using System.Diagnostics;

namespace LoudEnv;

public static class CalibrationSession
{
    public static async Task RunAsync(MicLevelMonitor monitor, string outPath, CancellationToken ct)
    {
        var labeler = new CalibrationLabeler();
        var clock = Stopwatch.StartNew();
        using var log = new CalibrationLog(outPath, monitor.DeviceName);

        var progress = new InlineProgress<LevelReading>(reading =>
        {
            long elapsedMs = clock.ElapsedMilliseconds;
            string label = labeler.LabelAt(elapsedMs);
            log.Write(DateTime.Now, elapsedMs, label, reading);
            Console.Write($"\r{label,-14} {LevelRenderer.Format(reading.Peak)}");
        });

        using var stop = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var keys = ReadKeysAsync(labeler, clock, stop);
        try
        {
            await monitor.RunAsync(progress, stop.Token);
        }
        finally
        {
            stop.Cancel();
            try
            {
                await keys;
            }
            catch (OperationCanceledException)
            {
            }
        }
    }

    private static async Task ReadKeysAsync(CalibrationLabeler labeler, Stopwatch clock, CancellationTokenSource stop)
    {
        try
        {
            while (!stop.IsCancellationRequested)
            {
                if (Console.KeyAvailable)
                {
                    var key = Console.ReadKey(intercept: true);
                    labeler.Press(key.KeyChar, clock.ElapsedMilliseconds);
                }
                await Task.Delay(50, stop.Token).ConfigureAwait(false);
            }
        }
        catch
        {
            stop.Cancel();
            throw;
        }
    }
}
