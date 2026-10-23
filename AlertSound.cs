using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace LoudEnv;

public static class AlertSound
{
    private const int SampleRate = 44100;
    private const double Frequency = 880;
    private const double Gain = 0.25;
    private static readonly TimeSpan Duration = TimeSpan.FromMilliseconds(150);
    private static bool _failed;

    public static void Play()
    {
        if (_failed)
            return;

        try
        {
            var output = new WaveOutEvent();
            var tone = new SignalGenerator(SampleRate, 1)
            {
                Type = SignalGeneratorType.Sin,
                Frequency = Frequency,
                Gain = Gain,
            };
            output.Init(tone.Take(Duration));
            output.PlaybackStopped += (_, _) => output.Dispose();
            output.Play();
        }
        catch (Exception ex)
        {
            _failed = true;
            Console.Error.WriteLine($"Aviso: não foi possível tocar o alerta sonoro: {ex.Message}");
        }
    }
}
