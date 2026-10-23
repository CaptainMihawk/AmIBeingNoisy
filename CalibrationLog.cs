using System.Globalization;
using System.Text;

namespace LoudEnv;

public sealed class CalibrationLog : IDisposable
{
    private readonly StreamWriter _writer;
    private readonly string _device;

    public CalibrationLog(string path, string device)
    {
        _device = device;
        _writer = new StreamWriter(path, append: false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false))
        {
            AutoFlush = true,
        };
        _writer.WriteLine("timestamp;elapsed_ms;device;label;peak;rms");
    }

    public void Write(DateTime timestamp, long elapsedMs, string label, LevelReading reading)
    {
        _writer.WriteLine(string.Join(';',
            timestamp.ToString("yyyy-MM-dd'T'HH:mm:ss.fff", CultureInfo.InvariantCulture),
            elapsedMs.ToString(CultureInfo.InvariantCulture),
            _device,
            label,
            reading.Peak.ToString("0.000000", CultureInfo.InvariantCulture),
            reading.Rms.ToString("0.000000", CultureInfo.InvariantCulture)));
    }

    public void Dispose() => _writer.Dispose();
}
