namespace LoudEnv.Tray;

public static class Schedule
{
    public static bool IsActive(TimeOnly now, bool enabled, TimeOnly start, TimeOnly end)
    {
        if (!enabled || start == end)
            return true;

        return start < end
            ? now >= start && now < end
            : now >= start || now < end;
    }
}
