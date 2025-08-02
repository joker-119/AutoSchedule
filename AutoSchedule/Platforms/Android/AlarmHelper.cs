namespace AutoSchedule.Platforms.Android;

using global::Android.Content;
using global::Android.Provider;

public static class AlarmHelper
{
    private const string Label = "Work";
    private const string PrefKey = "LastWorkAlarm"; // "HH:mm" stored

    public static void SetOrReplaceAlarm(Context ctx, DateTime trigger)
    {
        // 1️⃣ dismiss previous one, if any
        (int hr, int min)? last = LoadLast();
        if (last is { hr: var h, min: var m })
        {
            Intent dismiss = new Intent(AlarmClock.ActionDismissAlarm)
                .PutExtra(AlarmClock.ExtraAlarmSearchMode, AlarmClock.AlarmSearchModeTime)
                .PutExtra(AlarmClock.ExtraHour, h)
                .PutExtra(AlarmClock.ExtraMinutes, m)
                .PutExtra(AlarmClock.ExtraMessage, Label)
                .PutExtra(AlarmClock.ExtraSkipUi, true)
                .AddFlags(ActivityFlags.NewTask | ActivityFlags.ClearTop);
            ctx.StartActivity(dismiss);
        }

        // 2️⃣ create / update
        Intent set = new Intent(AlarmClock.ActionSetAlarm)
            .PutExtra(AlarmClock.ExtraHour,    trigger.Hour)
            .PutExtra(AlarmClock.ExtraMinutes, trigger.Minute)
            .PutExtra(AlarmClock.ExtraMessage, Label)
            .PutExtra(AlarmClock.ExtraSkipUi,  true)
            .AddFlags(ActivityFlags.NewTask | ActivityFlags.ClearTop);
        ctx.StartActivity(set);

        SaveLast(trigger.Hour, trigger.Minute);
    }

    public static void DisableAlarm(Context ctx)
    {
        (int hr, int min)? last = LoadLast();
        if (last is null)
            return;
        
        (int h, int m) = last.Value;
        Intent dismiss = new Intent(AlarmClock.ActionDismissAlarm)
            .PutExtra(AlarmClock.ExtraAlarmSearchMode, AlarmClock.AlarmSearchModeTime)
            .PutExtra(AlarmClock.ExtraHour, h)
            .PutExtra(AlarmClock.ExtraMinutes, m)
            .PutExtra(AlarmClock.ExtraMessage, Label)
            .PutExtra(AlarmClock.ExtraSkipUi, true)
            .AddFlags(ActivityFlags.NewTask | ActivityFlags.ClearTop);
        ctx.StartActivity(dismiss);
        Preferences.Default.Remove(PrefKey);
    }
    
    private static (int hr, int min)? LoadLast()
    {
        string s = Preferences.Default.Get(PrefKey, string.Empty);
        return TimeSpan.TryParse(s, out TimeSpan t)
            ? (t.Hours, t.Minutes)
            : null;
    }

    private static void SaveLast(int hr, int min) =>
        Preferences.Default.Set(PrefKey, $"{hr:D2}:{min:D2}");
}