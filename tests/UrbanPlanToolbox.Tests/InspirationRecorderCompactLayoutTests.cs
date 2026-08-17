using Xunit;

namespace UrbanPlanToolbox.Tests;

public sealed class InspirationRecorderCompactLayoutTests
{
    [Fact]
    public void RecorderUsesTheAcceptedSquareCompactGeometry()
    {
        var root = FindRepositoryRoot();
        var xaml = File.ReadAllText(Path.Combine(root, "Views", "InspirationRecorderWindow.xaml"));
        var code = File.ReadAllText(Path.Combine(root, "Views", "InspirationRecorderWindow.xaml.cs"));

        Assert.Contains("SizeInt32(560,560)", code);
        Assert.Contains("Padding=\"16,10,16,16\"", xaml);
        Assert.Contains("RowSpacing=\"12\"", xaml);
        Assert.Contains("ColumnSpacing=\"10\"", xaml);
        Assert.Contains("<ColumnDefinition Width=\"120\" />", xaml);
        Assert.Contains("Margin=\"16,0,52,0\"", xaml);
    }

    [Fact]
    public void RecorderKeepsTheExistingControlsAndVisualTreatment()
    {
        var root = FindRepositoryRoot();
        var xaml = File.ReadAllText(Path.Combine(root, "Views", "InspirationRecorderWindow.xaml"));
        var code = File.ReadAllText(Path.Combine(root, "Views", "InspirationRecorderWindow.xaml.cs"));

        Assert.Contains("<MicaBackdrop />", xaml);
        foreach (var name in new[]
        {
            "StatusText", "OpenFullButton", "TitleBox", "CategoryBox", "ContentBox",
            "PreviousButton", "NextButton", "NewButton", "DeleteButton", "SaveButton"
        })
            Assert.Contains($"x:Name=\"{name}\"", xaml);

        Assert.Contains("OpenFullButton.Content=new FontIcon{Glyph=\"\\uE8A7\",FontSize=12}", code);
        Assert.Contains("PreviousButton.Content=\"‹\"", code);
        Assert.Contains("NextButton.Content=\"›\"", code);
        Assert.Contains("NewButton.Content=\"+\"", code);
        Assert.DoesNotContain("#", xaml);
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "UrbanPlanToolbox.slnx"))) return directory.FullName;
        throw new DirectoryNotFoundException();
    }
}
