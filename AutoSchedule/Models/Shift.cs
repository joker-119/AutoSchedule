namespace AutoSchedule.Models;

public record Shift(string location, DayOfWeek day, DateTime start, DateTime end)
{
    public string DayName => day.ToString();
    
    public static long ToUnixEpochMillis(DateTime time) => new DateTimeOffset(time).ToUnixTimeMilliseconds();
}