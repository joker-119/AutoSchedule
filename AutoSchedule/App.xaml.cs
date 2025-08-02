﻿namespace AutoSchedule;

public partial class App
{
    public App() => InitializeComponent();

    protected override Window CreateWindow(IActivationState? activationState) => new(new NavigationPage(new MainPage()));
}