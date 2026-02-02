using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.ReactiveUI;

namespace StepikAnalyticsDesktop.App;

internal static class Program
{
    public static void Main(string[] args)
    {
        if (args.Contains("--run-tests"))
        {
            var exitCode = StepikAnalyticsDesktop.Tests.SelfTestRunner.RunAsync().GetAwaiter().GetResult();
            Environment.Exit(exitCode);
            return;
        }

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace()
            .UseReactiveUI();
    }
}
