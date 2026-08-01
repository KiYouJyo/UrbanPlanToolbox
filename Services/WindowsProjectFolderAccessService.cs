using Windows.Storage;
using Windows.Storage.AccessCache;
using Windows.Storage.Pickers;
using Windows.System;
using UrbanPlanToolbox.Models.Projects;

namespace UrbanPlanToolbox.Services;

public sealed class WindowsProjectFolderAccessService : IProjectFolderAccessService
{
    public static WindowsProjectFolderAccessService Default { get; } = new();

    public async Task<ProjectFolderAccessResult> SelectAsync(Guid projectId, ProjectFolderReference? current = null)
    {
        var picker = new FolderPicker { SuggestedStartLocation = PickerLocationId.DocumentsLibrary };
        picker.FileTypeFilter.Add("*");
        if (App.MainWindow is not null)
            WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow));
        var folder = await picker.PickSingleFolderAsync();
        if (folder is null) return new(false, ErrorKey: "ProjectFolder_SelectionCancelled");

        var token = projectId.ToString("N");
        StorageApplicationPermissions.FutureAccessList.AddOrReplace(token, folder);
        return new(true, new ProjectFolderReference
        {
            AccessToken = token,
            DisplayName = folder.Name,
            DisplayPath = folder.Path,
            RequiresReselection = false
        });
    }

    public async Task<ProjectFolderAccessResult> OpenAsync(ProjectFolderReference reference)
    {
        if (reference.RequiresReselection || string.IsNullOrWhiteSpace(reference.AccessToken))
            return new(false, ErrorKey: "ProjectFolder_RequiresReselection");
        try
        {
            var folder = await StorageApplicationPermissions.FutureAccessList.GetFolderAsync(reference.AccessToken);
            return await Launcher.LaunchFolderAsync(folder)
                ? new(true, reference)
                : new(false, ErrorKey: "ProjectFolder_OpenFailed");
        }
        catch (Exception exception) when (exception is FileNotFoundException or UnauthorizedAccessException or ArgumentException)
        {
            return new(false, ErrorKey: "ProjectFolder_AccessExpired");
        }
    }

    public void Clear(ProjectFolderReference? reference)
    {
        if (!string.IsNullOrWhiteSpace(reference?.AccessToken) && StorageApplicationPermissions.FutureAccessList.ContainsItem(reference.AccessToken))
            StorageApplicationPermissions.FutureAccessList.Remove(reference.AccessToken);
    }
}
