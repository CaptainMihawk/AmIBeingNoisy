namespace LoudEnv;

public sealed class ThresholdDetector
{
    public const int RequiredWindows = 3;

    private readonly float _threshold;
    private int _consecutive;
    private bool _active;

    public ThresholdDetector(float threshold) => _threshold = threshold;

    public bool Sample(float rms)
    {
        if (rms < _threshold)
        {
            _consecutive = 0;
            _active = false;
            return false;
        }

        if (_active)
            return false;

        _consecutive++;
        if (_consecutive < RequiredWindows)
            return false;

        _active = true;
        _consecutive = 0;
        return true;
    }
}
