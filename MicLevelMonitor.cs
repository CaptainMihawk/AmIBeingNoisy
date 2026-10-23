using System.Runtime.InteropServices;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace LoudEnv;

public sealed class MicLevelMonitor : IDisposable
{
    public const int PollIntervalMs = 100;

    private readonly MMDeviceEnumerator _enumerator = new();
    private readonly MMDevice _device;

    public string DeviceName => _device.FriendlyName;

    public MicLevelMonitor(int? deviceIndex = null)
    {
        _device = deviceIndex is int index ? ResolveByIndex(index) : ResolveDefault();
    }

    public static IReadOnlyList<(int Index, string Name)> ListCaptureEndpoints()
    {
        using var enumerator = new MMDeviceEnumerator();
        var collection = enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active);
        var endpoints = new List<(int Index, string Name)>(collection.Count);
        for (int i = 0; i < collection.Count; i++)
            endpoints.Add((i, collection[i].FriendlyName));
        return endpoints;
    }

    public async Task RunAsync(IProgress<float> level, CancellationToken ct)
    {
        using var capture = new WasapiCapture(_device);
        var sync = new object();
        float peakSinceLastRead = 0f;

        capture.DataAvailable += (_, e) =>
        {
            float peak = PeakFromBuffer(capture.WaveFormat, e.Buffer, e.BytesRecorded);
            lock (sync)
            {
                if (peak > peakSinceLastRead)
                    peakSinceLastRead = peak;
            }
        };

        capture.StartRecording();
        try
        {
            while (!ct.IsCancellationRequested)
            {
                await Task.Delay(PollIntervalMs, ct).ConfigureAwait(false);
                float reported;
                lock (sync)
                {
                    reported = peakSinceLastRead;
                    peakSinceLastRead = 0f;
                }
                level.Report(Math.Clamp(reported, 0f, 1f));
            }
        }
        finally
        {
            capture.StopRecording();
        }
    }

    private static float PeakFromBuffer(WaveFormat format, byte[] buffer, int bytesRecorded)
    {
        float peak = 0f;
        if (format.Encoding == WaveFormatEncoding.IeeeFloat)
        {
            int count = bytesRecorded / 4;
            for (int i = 0; i < count; i++)
            {
                float v = BitConverter.ToSingle(buffer, i * 4);
                if (v < 0)
                    v = -v;
                if (v > peak)
                    peak = v;
            }
        }
        else
        {
            int count = bytesRecorded / 2;
            for (int i = 0; i < count; i++)
            {
                float v = Math.Abs(BitConverter.ToInt16(buffer, i * 2)) / 32768f;
                if (v > peak)
                    peak = v;
            }
        }
        return peak;
    }

    private MMDevice ResolveDefault()
    {
        try
        {
            return _enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Multimedia);
        }
        catch (Exception ex) when (ex is COMException or InvalidOperationException)
        {
            throw new InvalidOperationException(
                "Nenhum dispositivo de captura de áudio disponível.", ex);
        }
    }

    private MMDevice ResolveByIndex(int index)
    {
        var collection = _enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active);
        if (index < 0 || index >= collection.Count)
        {
            var valid = string.Join(", ", Enumerable.Range(0, collection.Count)
                .Select(i => $"{i} ({collection[i].FriendlyName})"));
            throw new ArgumentOutOfRangeException(nameof(index), index,
                $"Índice de dispositivo inválido. Índices válidos: {(valid.Length == 0 ? "(nenhum)" : valid)}");
        }
        return collection[index];
    }

    public void Dispose()
    {
        _device.Dispose();
        _enumerator.Dispose();
    }
}
