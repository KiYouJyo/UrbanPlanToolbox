using Windows.Security.Credentials;

namespace UrbanPlanToolbox.Services;

public interface IWebDavCredentialStore
{
    bool HasCredential(string username);
    string? GetPassword(string username);
    void Save(string username, string password);
    void Delete(string username);
    void DeleteAll();
}

public sealed class WebDavCredentialStore : IWebDavCredentialStore
{
    private const string ResourceName = "UrbanPlanToolbox.WebDAV";
    private readonly PasswordVault _vault = new();
    public static WebDavCredentialStore Default { get; } = new();

    public bool HasCredential(string username) => !string.IsNullOrWhiteSpace(GetPassword(username));

    public string? GetPassword(string username)
    {
        if (string.IsNullOrWhiteSpace(username)) return null;
        try
        {
            var credential = _vault.Retrieve(ResourceName, username);
            credential.RetrievePassword();
            return string.IsNullOrEmpty(credential.Password) ? null : credential.Password;
        }
        catch (Exception)
        {
            return null;
        }
    }

    public void Save(string username, string password)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(username);
        ArgumentException.ThrowIfNullOrWhiteSpace(password);
        Delete(username);
        _vault.Add(new PasswordCredential(ResourceName, username, password));
    }

    public void Delete(string username)
    {
        if (string.IsNullOrWhiteSpace(username)) return;
        try
        {
            var credential = _vault.Retrieve(ResourceName, username);
            _vault.Remove(credential);
        }
        catch (Exception) { }
    }

    public void DeleteAll()
    {
        try
        {
            foreach (var credential in _vault.FindAllByResource(ResourceName).ToArray()) _vault.Remove(credential);
        }
        catch (Exception) { }
    }
}
