namespace LoudEnv;

public sealed class ThresholdDetector
{
    public const int RequiredWindows = 3;

    private readonly float _threshold;
    private readonly long _repeatMs;
    private int _consecutive;
    private bool _above;
    private long _lastAlertMs;

    public ThresholdDetector(float threshold, long repeatMs = long.MaxValue)
    {
        _threshold = threshold;
        _repeatMs = repeatMs;
    }

    public bool Sample(float rms, long nowMs)
    {
        if (rms < _threshold)
        {
            _consecutive = 0;
            _above = false;
            return false;
        }

        if (!_above)
        {
            _consecutive++;
            if (_consecutive < RequiredWindows)
                return false;

            _above = true;
            _lastAlertMs = nowMs;
            return true;
        }

        if (nowMs - _lastAlertMs >= _repeatMs)
        {
            _lastAlertMs = nowMs;
            return true;
        }

        return false;
    }
}
