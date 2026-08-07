using System.Text.Json;
using Xunit;
using UrbanPlanToolbox.Services;

namespace UrbanPlanToolbox.Tests;

public sealed class AppLoggerTests
{
    [Fact]
    public void WritesInfoAndErrorAsJsonLinesWithExceptionType()
    {
        var root = CreateRoot();
        try
        {
            var logger = new AppLogger(root);
            logger.Info("Tests", "Write", "safe message");
            logger.Error("Tests", "Failure", new InvalidOperationException("secret=C:\\Users\\person"), "token=abc C:\\Users\\person");
            var lines = File.ReadAllLines(Directory.GetFiles(root, "*.log").Single());
            Assert.Equal(2, lines.Length);
            Assert.Equal("Info", JsonDocument.Parse(lines[0]).RootElement.GetProperty("level").GetString());
            var error = JsonDocument.Parse(lines[1]).RootElement;
            Assert.Equal(typeof(InvalidOperationException).FullName, error.GetProperty("exceptionType").GetString());
            Assert.DoesNotContain("C:\\Users", lines[1]);
            Assert.DoesNotContain("abc", lines[1]);
        }
        finally { DeleteRoot(root); }
    }

    [Fact]
    public void WriteFailureDoesNotThrow()
    {
        var root = Path.Combine(Path.GetTempPath(), $"UrbanPlanToolbox-log-file-{Guid.NewGuid():N}");
        File.WriteAllText(root, "not a directory");
        try { new AppLogger(root).Error("Tests", "Failure", new Exception(), "safe"); }
        finally { File.Delete(root); }
    }

    [Fact]
    public void RetentionKeepsNewestConfiguredFiles()
    {
        var root = CreateRoot();
        try
        {
            for (var i = 0; i < 4; i++) File.WriteAllText(Path.Combine(root, $"{i}.log"), i.ToString());
            var files = Directory.GetFiles(root).OrderBy(path => path).ToArray();
            for (var i = 0; i < files.Length; i++) File.SetLastWriteTimeUtc(files[i], DateTime.UtcNow.AddDays(-i));
            new AppLogger(root, 2).RunRetention();
            Assert.Equal(2, Directory.GetFiles(root, "*.log").Length);
        }
        finally { DeleteRoot(root); }
    }

    private static string CreateRoot() { var root = Path.Combine(Path.GetTempPath(), $"UrbanPlanToolbox-logs-{Guid.NewGuid():N}"); Directory.CreateDirectory(root); return root; }
    private static void DeleteRoot(string root) { if (Directory.Exists(root)) Directory.Delete(root, true); }
}
