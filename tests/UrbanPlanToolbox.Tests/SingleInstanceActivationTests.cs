using Xunit;

namespace UrbanPlanToolbox.Tests;

public sealed class SingleInstanceActivationTests
{
    [Fact]
    public void StartupArbitratesBeforeWinUiAndCompletesRedirectSynchronously()
    {
        var root = FindRepositoryRoot();
        var program = File.ReadAllText(Path.Combine(root, "Program.cs"));
        var registration = program.IndexOf("AppInstance.FindOrRegisterForKey", StringComparison.Ordinal);
        var applicationStart = program.IndexOf("Application.Start", StringComparison.Ordinal);

        Assert.True(registration >= 0 && registration < applicationStart);
        Assert.Contains("public static void Main(string[] args)", program);
        Assert.Contains("mainInstance.RedirectActivationToAsync(activationArguments).AsTask().GetAwaiter().GetResult()", program);
        Assert.DoesNotContain("public static async Task Main", program);
        Assert.Contains("if (!mainInstance.IsCurrent)", program);
        Assert.Contains("return;", program);
        Assert.Contains("mainInstance.Activated", program);
        Assert.Contains("DispatcherQueueSynchronizationContext", program);
        Assert.True(program.IndexOf("SynchronizationContext.SetSynchronizationContext", StringComparison.Ordinal) < program.IndexOf("new App()", StringComparison.Ordinal));
        Assert.True(program.IndexOf("ComWrappersSupport.InitializeComWrappers", StringComparison.Ordinal) < program.IndexOf("AppInstance.GetCurrent", StringComparison.Ordinal));
    }

    [Fact]
    public void InstanceKeyIsStableAndNeverDerivedFromTheVersion()
    {
        var root = FindRepositoryRoot();
        var service = File.ReadAllText(Path.Combine(root, "Services", "SingleInstanceActivation.cs"));

        Assert.Contains("public const string InstanceKey = \"UrbanPlanToolbox.Main\"", service);
        Assert.DoesNotContain("AppVersionProvider", service);
        Assert.DoesNotContain("Guid", service);
    }

    [Fact]
    public void RedirectedActivationOnlyTargetsTheExistingWindowAndRestoresIt()
    {
        var root = FindRepositoryRoot();
        var app = File.ReadAllText(Path.Combine(root, "App.xaml.cs"));
        var window = File.ReadAllText(Path.Combine(root, "MainWindow.xaml.cs"));

        var handlerStart = app.IndexOf("OnRedirectedActivation(AppActivationArguments activationArguments)", StringComparison.Ordinal);
        var handlerEnd = app.IndexOf("private static void ActivatePendingRedirectedWindow", handlerStart, StringComparison.Ordinal);
        var handler = app.Substring(handlerStart, handlerEnd - handlerStart);
        Assert.Contains("existingWindow.RestoreAndActivate", handler);
        Assert.DoesNotContain("new MainWindow", handler);
        Assert.Contains("OverlappedPresenterState.Minimized", window);
        Assert.Contains("presenter.Restore()", window);
        Assert.Contains("Activate();", window);
        Assert.Contains("SetForegroundWindow", window);
    }

    [Fact]
    public void RestartServiceContinuesToUseWindowsAppSdkRestart()
    {
        var root = FindRepositoryRoot();
        var restart = File.ReadAllText(Path.Combine(root, "Services", "ApplicationRestartService.cs"));
        Assert.Contains("AppInstance.Restart(string.Empty)", restart);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "UrbanPlanToolbox.slnx"))) return directory.FullName;
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
