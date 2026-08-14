namespace UrbanPlanToolbox.Services;

/// <summary>Registers Windows-managed recovery for an update that may terminate this process.</summary>
public interface IApplicationRestartRegistrationService
{
    bool TryRegister(out string? failureReason);
    void Unregister();
}
