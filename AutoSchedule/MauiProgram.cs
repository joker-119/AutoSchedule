namespace AutoSchedule;

using AutoSchedule.Services;
using AutoSchedule.ViewModels;

using Microsoft.Maui.Hosting;

public static class MauiProgram
{
    public static IServiceProvider Services { get; private set; } = default!;
    
    public static MauiApp CreateMauiApp()
    {
        MauiAppBuilder builder = MauiApp.CreateBuilder();
        builder.UseMauiApp<App>().ConfigureFonts(f =>
        {
            f.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
            f.AddFont("OpenSans-Semibold.tts", "OpenSansSemibold");
        });
        
        builder.Services.AddSingleton<OcrService>();
        builder.Services.AddSingleton<CalendarService>();
        builder.Services.AddSingleton<ImportViewModel>();
        builder.Services.AddSingleton<AlarmService>();
        builder.Services.AddTransient<MainPage>();
        
        MauiApp app = builder.Build();
        Services = app.Services;
        return app;
    }
}