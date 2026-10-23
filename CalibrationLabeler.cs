namespace LoudEnv;

public sealed class CalibrationLabeler
{
    public const int ShoutWindowMs = 2000;

    private readonly object _sync = new();
    private string _segment = "pausa";
    private long _shoutUntilMs = -1;

    public void Press(char key, long nowMs)
    {
        lock (_sync)
        {
            switch (char.ToLowerInvariant(key))
            {
                case '1':
                    _segment = "fala_normal";
                    break;
                case '2':
                    _segment = "fala_jogo";
                    break;
                case '3':
                    _segment = "silencio_jogo";
                    break;
                case '4':
                    _segment = "pausa";
                    break;
                case 'g':
                    _shoutUntilMs = nowMs + ShoutWindowMs;
                    break;
            }
        }
    }

    public string LabelAt(long nowMs)
    {
        lock (_sync)
        {
            return nowMs < _shoutUntilMs ? "grito" : _segment;
        }
    }
}
