using System.Runtime.InteropServices;
using NAudio.CoreAudioApi;

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

    public MicLevelMonitor(string deviceName)
    {
        _device = ResolveByName(deviceName);
    }

    public static string DefaultDeviceName()
    {
        using var enumerator = new MMDeviceEnumerator();
        using var device = enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Multimedia);
        return device.FriendlyName;
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

    public async Task RunAsync(IProgress<LevelReading> level, CancellationToken ct)
    {
        using var capture = new WasapiCapture(_device);
        var window = new LevelWindow();
        Exception? captureError = null;

        capture.DataAvailable += (_, e) => window.Add(capture.WaveFormat, e.Buffer, e.BytesRecorded);
        capture.RecordingStopped += (_, e) => Volatile.Write(ref captureError, e.Exception);

        capture.StartRecording();
        try
        {
            while (!ct.IsCancellationRequested)
            {
                await Task.Delay(PollIntervalMs, ct).ConfigureAwait(false);
                var error = Volatile.Read(ref captureError);
                if (error is not null)
                    throw new InvalidOperationException($"Captura de áudio interrompida: {error.Message}", error);
                level.Report(window.TakeReading());
            }
        }
        finally
        {
            capture.StopRecording();
        }
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

    private MMDevice ResolveByName(string name)
    {
        var collection = _enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active);

        for (int i = 0; i < collection.Count; i++)
        {
            if (string.Equals(collection[i].FriendlyName, name, StringComparison.OrdinalIgnoreCase))
                return collection[i];
        }

        for (int i = 0; i < collection.Count; i++)
        {
            if (collection[i].FriendlyName.Contains(name, StringComparison.OrdinalIgnoreCase))
                return collection[i];
        }

        var available = string.Join(", ", Enumerable.Range(0, collection.Count)
            .Select(i => collection[i].FriendlyName));
        throw new InvalidOperationException(
            $"Dispositivo de captura não encontrado: {name}. Disponíveis: {(available.Length == 0 ? "(nenhum)" : available)}");
    }

    public void Dispose()
    {
        _device.Dispose();
        _enumerator.Dispose();
    }
}
