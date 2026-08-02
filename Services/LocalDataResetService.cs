namespace UrbanPlanToolbox.Services;

public sealed class LocalDataResetService
{
    private readonly IAppDataPathProvider _paths;
    public LocalDataResetService(IAppDataPathProvider paths) => _paths = paths ?? throw new ArgumentNullException(nameof(paths));

    public Task<bool> ResetAsync()
    {
        try
        {
            _paths.EnsureInfrastructureDirectories();
            foreach (var file in Directory.EnumerateFiles(_paths.Paths.RootDirectory)) File.Delete(file);
            foreach (var directory in Directory.EnumerateDirectories(_paths.Paths.RootDirectory)) Directory.Delete(directory, recursive: true);
            _paths.EnsureInfrastructureDirectories();
            return Task.FromResult(true);
        }
        catch (IOException) { return Task.FromResult(false); }
        catch (UnauthorizedAccessException) { return Task.FromResult(false); }
    }
}
