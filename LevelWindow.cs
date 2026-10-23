using NAudio.Wave;

namespace LoudEnv;

public sealed class LevelWindow
{
    private readonly object _sync = new();
    private float _peak;
    private double _sumSquares;
    private long _count;

    public void Add(WaveFormat format, byte[] buffer, int bytesRecorded)
    {
        float peak = 0f;
        double sumSquares = 0;
        int count;
        if (format.Encoding == WaveFormatEncoding.IeeeFloat)
        {
            int total = bytesRecorded / 4;
            count = 0;
            for (int i = 0; i < total; i++)
            {
                float v = BitConverter.ToSingle(buffer, i * 4);
                if (!float.IsFinite(v))
                    continue;

                count++;
                float a = Math.Abs(v);
                if (a > peak)
                    peak = a;
                sumSquares += (double)v * v;
            }
        }
        else
        {
            count = bytesRecorded / 2;
            for (int i = 0; i < count; i++)
            {
                float v = Math.Abs((int)BitConverter.ToInt16(buffer, i * 2)) / 32768f;
                if (v > peak)
                    peak = v;
                sumSquares += (double)v * v;
            }
        }

        lock (_sync)
        {
            if (peak > _peak)
                _peak = peak;
            _sumSquares += sumSquares;
            _count += count;
        }
    }

    public LevelReading TakeReading()
    {
        lock (_sync)
        {
            float rms = _count == 0 ? 0f : (float)Math.Sqrt(_sumSquares / _count);
            var reading = new LevelReading(_peak, rms);
            _peak = 0f;
            _sumSquares = 0;
            _count = 0;
            return reading;
        }
    }
}
