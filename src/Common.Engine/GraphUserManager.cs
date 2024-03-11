using Azure.Identity;
using Common.Engine.Config;
using Microsoft.Extensions.Logging;
using Microsoft.Graph;
using Microsoft.Graph.Models;

namespace Common.Engine;


public interface IGraphUserManager
{
    Task<List<User>> GetAllUsers(IAzureStorageManager azureStorageManager);
    Task<User> GetUserByEmail(string email);
}

/// <summary>
/// Handle Graph User looksup
/// </summary>
public class GraphUserManager : AbstractGraphManager, IGraphUserManager
{
    public GraphUserManager(AppConfig config, ILogger<GraphUserManager> trace) : base(config, trace)
    {
    }

    public async Task<User> GetUserByEmail(string email)
    {
        var searchResults = await _client.Users[email].GetAsync();
        return searchResults ?? throw new ArgumentOutOfRangeException(nameof(email));
    }

    public async Task<List<User>> GetAllUsers(IAzureStorageManager azureStorageManager)
    {
        var allUsers = await _client.Users.GetAsync();
        return allUsers?.Value ?? throw new Exception("No users found");
    }
}

public abstract class AbstractManager
{
    protected AppConfig _config;
    protected ILogger _trace;
    public AbstractManager(AppConfig config, ILogger trace)
    {
        _config = config;
        _trace = trace;
    }
}

/// <summary>
/// Something that interacts with Graph
/// </summary>
public abstract class AbstractGraphManager : AbstractManager
{
    protected GraphServiceClient _client;

    public AbstractGraphManager(AppConfig config, ILogger trace) : base(config, trace)
    {
        var options = new TokenCredentialOptions
        {
            AuthorityHost = AzureAuthorityHosts.AzurePublicCloud
        };
        var scopes = new[] { "https://graph.microsoft.com/.default" };

        var clientSecretCredential = new ClientSecretCredential(config.AuthConfig.TenantId, config.AuthConfig.ClientId, config.AuthConfig.ClientSecret, options);
        _client = new GraphServiceClient(clientSecretCredential, scopes);
    }
}
