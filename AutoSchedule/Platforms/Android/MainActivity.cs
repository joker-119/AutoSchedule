namespace AutoSchedule.Platforms.Android;

using global::Android.App;
using global::Android.Content.PM;

[Activity(
    Theme = "@style/Maui.MainTheme",
    MainLauncher = true,
    ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize )]

public class MainActivity : MauiAppCompatActivity
{
    public MainActivity()
    {
        // WorkManager.GetInstance(this).EnqueueUniquePeriodicWork("refreshAlarm", ExistingPeriodicWorkPolicy.Update, PeriodicWorkRequest.Builder.From<AlarmRefreshWorker>(12, TimeUnit.Hours).Build());
    }
}