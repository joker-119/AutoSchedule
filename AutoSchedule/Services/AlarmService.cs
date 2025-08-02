namespace AutoSchedule.Services;

using Android.Content;

using AutoSchedule.Models;
using AutoSchedule.Platforms.Android;

public class AlarmService
{
    private readonly CalendarService cal;
    private readonly Context ctx;

    public AlarmService(CalendarService cal)
    {
        this.cal = cal;
        ctx = Android.App.Application.Context;
    }

    public void RefreshAlarmForToday(long calendarId)
    {
        DateTime now = DateTime.Now;
        DateTime startOfDay = new(now.Year, now.Month, now.Day, 0, 0, 0, DateTimeKind.Local);
        DateTime endOfDay = startOfDay.AddDays(1);

        // query events today
        List<Shift> shifts = cal.FindShiftsInRange(calendarId, startOfDay, endOfDay).ToList();

        if (shifts.Count == 0)
        {
            AlarmHelper.DisableAlarm(ctx);
            return;
        }

        // take the first shift (earliest start)
        Shift first = shifts.OrderBy(s => s.start).First();
        DateTime alarmTime = first.start.AddHours(-2);

        if (alarmTime <= now)
        {
            // too late to ring today → don't schedule
            AlarmHelper.DisableAlarm(ctx);
            return;
        }

        AlarmHelper.SetOrReplaceAlarm(ctx, alarmTime);
    }
}