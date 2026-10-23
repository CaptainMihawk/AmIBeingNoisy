using System.Globalization;
using System.Text;

namespace LoudEnv;

public sealed class AlertLog : IDisposable
{
    private const string TimestampFormat = "yyyy-MM-dd'T'HH:mm:ss.fff";

    private readonly StreamWriter _writer;
    private readonly string _device;
    private readonly float _threshold;
    private readonly string _sessionStart;
    private DateTime? _start;
    private DateTime _last;
    private double _sum;
    private int _count;
    private float _max;
    private int _alerts;

    public AlertLog(string path, string device, float threshold)
    {
        _device = device;
        _threshold = threshold;
        _sessionStart = DateTime.Now.ToString(TimestampFormat, CultureInfo.InvariantCulture);

        bool isNew = !File.Exists(path) || new FileInfo(path).Length == 0;
        try
        {
            _writer = new StreamWriter(path, append: true, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false))
            {
                AutoFlush = true,
            };
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new IOException(
                $"Não foi possível abrir o log '{path}'. Verifique a pasta e se o arquivo não está aberto em outro programa.", ex);
        }

        if (isNew)
        {
            try
            {
                _writer.WriteLine("sessao_inicio;inicio;fim;duracao_s;rms_max;rms_medio;alertas;limiar;device");
            }
            catch
            {
                _writer.Dispose();
                throw;
            }
        }
    }

    public void Observe(float rms, bool above)
    {
        if (above)
        {
            if (_start is null)
            {
                _start = DateTime.Now;
                _sum = 0;
                _count = 0;
                _max = 0f;
                _alerts = 0;
            }

            _last = DateTime.Now;
            _sum += rms;
            _count++;
            if (rms > _max)
                _max = rms;
        }
        else if (_start is not null)
        {
            CloseEpisode();
        }
    }

    public void NoteAlert()
    {
        if (_start is not null)
            _alerts++;
    }

    private void CloseEpisode()
    {
        var start = _start!.Value;
        if (_alerts == 0)
        {
            _start = null;
            return;
        }

        double duration = (_last - start).TotalSeconds;
        double mean = _count == 0 ? 0 : _sum / _count;

        _writer.WriteLine(string.Join(';',
            _sessionStart,
            start.ToString(TimestampFormat, CultureInfo.InvariantCulture),
            _last.ToString(TimestampFormat, CultureInfo.InvariantCulture),
            duration.ToString("0.00", CultureInfo.InvariantCulture),
            _max.ToString("0.0000", CultureInfo.InvariantCulture),
            mean.ToString("0.0000", CultureInfo.InvariantCulture),
            _alerts.ToString(CultureInfo.InvariantCulture),
            _threshold.ToString("0.000", CultureInfo.InvariantCulture),
            _device));

        _start = null;
    }

    public void Dispose()
    {
        if (_start is not null)
            CloseEpisode();

        _writer.Dispose();
    }
}
