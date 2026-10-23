using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace LoudEnv;

public static class AlertSound
{
    private const int SampleRate = 44100;
    private const double Frequency = 880;
    private const double Gain = 0.25;
    private static readonly TimeSpan Duration = TimeSpan.FromMilliseconds(150);
    private static int _failed;

    public static void Play()
    {
        if (Volatile.Read(ref _failed) != 0)
            return;

        Task.Run(PlayOnce);
    }

    private static void PlayOnce()
    {
        WaveOutEvent? output = null;
        try
        {
            output = new WaveOutEvent();
            var tone = new SignalGenerator(SampleRate, 1)
            {
                Type = SignalGeneratorType.Sin,
                Frequency = Frequency,
                Gain = Gain,
            };
            output.Init(tone.Take(Duration));

            var device = output;
            output.PlaybackStopped += (_, e) =>
            {
                try
                {
                    device.Dispose();
                }
                catch (Exception ex)
                {
                    Warn(ex);
                }

                if (e.Exception is not null)
                    Warn(e.Exception);
            };

            output.Play();
            output = null; // A partir daqui, o PlaybackStopped libera o dispositivo.
        }
        catch (Exception ex)
        {
            output?.Dispose();
            Warn(ex);
        }
    }

    private static void Warn(Exception ex)
    {
        if (Interlocked.Exchange(ref _failed, 1) != 0)
            return;

        Console.Error.WriteLine($"Aviso: não foi possível tocar o alerta sonoro: {ex.Message}");
    }
}
