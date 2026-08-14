namespace UrbanPlanToolbox.Services;

public static class AppUpdateErrorMapper
{
    public static string ToResourceKey(string? code) => code switch
    {
        "NetworkError" => "Update_ErrorNetwork",
        "StoreUnavailable" or "StoreCheckFailed" => "Update_ErrorStoreUnavailable",
        "DownloadFailed" or "BundleDownloadFailed" => "Update_ErrorDownload",
        "InstallFailed" or "StoreInstallFailed" or "PackageDeploymentFailed" => "Update_ErrorInstall",
        "StoreRestartRegistrationFailed" => "Update_ErrorRestartRegistration",
        "FallbackRestartFailed" => "Update_ErrorFallbackRestart",
        "UnableToContactGitHub" => "Update_ErrorGitHubNetwork",
        "ReleaseNotFound" => "Update_ErrorReleaseNotFound",
        "BundleAssetNotFound" => "Update_ErrorBundleAssetNotFound",
        "ChecksumDownloadFailed" => "Update_ErrorChecksumDownload",
        "ChecksumMissing" => "Update_ErrorChecksumDownload",
        "ChecksumMismatch" => "Update_ErrorChecksumMismatch",
        "SignatureMissing" or "SignatureInvalid" or "SignerSubjectMismatch" or "SignerThumbprintMismatch" or "SignatureMismatch" or "BundleVerificationFailed" => "Update_ErrorSignatureMismatch",
        "GitHubRateLimited" => "Update_ErrorGitHubRateLimited",
        "InvalidReleaseResponse" => "Update_ErrorInvalidRelease",
        "NoPendingUpdate" => "Update_ErrorNoPending",
        "StoreWindowUnavailable" => "Update_ErrorWindowUnavailable",
        "LegacyMigrationRequired" => "Update_ErrorLegacyMigration",
        "AppInstallerUnavailable" => "Update_ErrorAppInstallerUnavailable",
        _ when code?.StartsWith("0x", StringComparison.Ordinal) == true => "Update_ErrorStoreCode",
        _ => "Update_ErrorGeneric"
    };
}
