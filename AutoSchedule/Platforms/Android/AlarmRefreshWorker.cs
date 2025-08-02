namespace AutoSchedule.Platforms.Android;

using AndroidX.Work;

using AutoSchedule.Services;

using global::Android.Content;

using Google.Common.Util.Concurrent;

public class AlarmRefreshWorker : Worker
{
    private readonly CalendarService cal;
    
    public AlarmRefreshWorker(Context ctx, WorkerParameters p, CalendarService cal)
        : base(ctx, p)
    {
        this.cal = cal;
    }

    public override Result DoWork()
    {
        AlarmService svr = MauiProgram.Services.GetRequiredService<AlarmService>();
        long calId = cal.GetCalendarIdForAccount("(INSERT YOUR EMAIL HERE)");
        svr.RefreshAlarmForToday(calId);
        
        return Result.InvokeSuccess();
    }
}