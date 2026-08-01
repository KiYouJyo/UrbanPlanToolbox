using System.Security.Cryptography;
using System.Text;

namespace UrbanPlanToolbox.Services;

/// <summary>Maps durable project and milestone IDs to the shell's 16-character identifiers.</summary>
public static class MilestoneReminderIdentity
{
    public const string GroupPrefix = "UPT-";
    public const string LegacyGroup = "UrbanPlanToolbox.Milestones";

    public static string Group(Guid projectId) => GroupPrefix + Token(projectId.ToString("N"), 12);
    public static string Tag(Guid milestoneId) => Token(milestoneId.ToString("N"), 16);

    private static string Token(string value, int length) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant()[..length];
}

public sealed record MilestoneReminderRefreshResult(bool Succeeded, int ScheduledCount, string? FailureType = null, string? Diagnostic = null)
{
    public static MilestoneReminderRefreshResult Success(int scheduledCount) => new(true, scheduledCount);
    public static MilestoneReminderRefreshResult Failure(Exception exception) => new(false, 0, exception.GetType().Name, $"{exception.Message} [{exception.GetType().Name}, 0x{exception.HResult:X8}]");
}
