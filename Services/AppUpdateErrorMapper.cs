namespace UrbanPlanToolbox.Services;

public static class AppUpdateErrorMapper
{
    public static string ToResourceKey(string? code) => code switch
    {
        "NetworkError" => "Update_ErrorNetwork",
        "StoreUnavailable" or "StoreCheckFailed" => "Update_ErrorStoreUnavailable",
        "DownloadFailed" => "Update_ErrorDownload",
        "InstallFailed" or "StoreInstallFailed" => "Update_ErrorInstall",
        "NoPendingUpdate" => "Update_ErrorNoPending",
        "StoreWindowUnavailable" => "Update_ErrorWindowUnavailable",
        _ when code?.StartsWith("0x", StringComparison.Ordinal) == true => "Update_ErrorStoreCode",
        _ => "Update_ErrorGeneric"
    };
}
