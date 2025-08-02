namespace AutoSchedule;

public partial class MainPage
{
    public MainPage()
    {
        InitializeComponent();
        BindingContext = MauiProgram.Services.GetRequiredService<ViewModels.ImportViewModel>();
    }
}