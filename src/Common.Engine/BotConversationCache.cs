using Azure;
using Common.Engine.Config;
using Microsoft.Bot.Schema;
using Microsoft.Graph;
using System.Collections.Concurrent;

namespace Common.Engine;


public class BotConversationCache : TableStorageManager
{
    #region Privates & Constructors

    const string TABLE_NAME = "ConversationCache";
    private readonly GraphServiceClient _graphServiceClient;
    private ConcurrentDictionary<string, CachedUserAndConversationData> _userIdConversationCache = new();

    public BotConversationCache(GraphServiceClient graphServiceClient, AppConfig appConfig) : base(appConfig.ConnectionStrings.Storage)
    {
        _graphServiceClient = graphServiceClient;

        // Dev only: make sure the Azure Storage emulator is running or this will fail
    }

    #endregion

    public async Task PopulateMemCacheIfEmpty()
    {
        if (_userIdConversationCache.Count > 0)
        {
            return;
        }

        var client = await base.GetTableClient(TABLE_NAME);
        var queryResultsFilter = client.Query<CachedUserAndConversationData>(filter: $"PartitionKey eq '{CachedUserAndConversationData.PartitionKeyVal}'");
        foreach (var qEntity in queryResultsFilter)
        {
            _userIdConversationCache.AddOrUpdate(qEntity.RowKey, qEntity, (key, newValue) => qEntity);
            Console.WriteLine($"{qEntity.RowKey}: {qEntity}");
        }
    }

    internal async Task RemoveFromCache(string aadObjectId)
    {
        CachedUserAndConversationData? u = null;
        if (_userIdConversationCache.TryGetValue(aadObjectId, out u))
        {
            _userIdConversationCache.TryRemove(aadObjectId, out u);
        }
        var client = await base.GetTableClient(TABLE_NAME);

        await client.DeleteEntityAsync(CachedUserAndConversationData.PartitionKeyVal, aadObjectId);
    }

    /// <summary>
    /// App installed for user & now we have a conversation reference to cache for future chat threads.
    /// </summary>
    public async Task AddConversationReferenceToCache(Activity activity)
    {
        var conversationReference = activity.GetConversationReference();
        await AddOrUpdateUserAndConversationId(conversationReference, activity.ServiceUrl, _graphServiceClient);
    }

    internal async Task AddOrUpdateUserAndConversationId(ConversationReference conversationReference, string serviceUrl, GraphServiceClient graphClient)
    {
        var cacheId = conversationReference.User.AadObjectId;
        CachedUserAndConversationData? u = null;
        var client = await base.GetTableClient(TABLE_NAME);

        if (!_userIdConversationCache.TryGetValue(cacheId, out u))
        {

            // Have not got in memory cache

            Response<CachedUserAndConversationData>? entityResponse = null;
            try
            {
                entityResponse = client.GetEntity<CachedUserAndConversationData>(CachedUserAndConversationData.PartitionKeyVal, cacheId);
            }
            catch (RequestFailedException ex)
            {
                if (ex.ErrorCode == "ResourceNotFound")
                {
                    // No worries
                }
                else
                {
                    throw;
                }
            }

            if (entityResponse == null)
            {
                var user = await graphClient.Users[conversationReference.User.AadObjectId].GetAsync(op => op.QueryParameters.Select = ["userPrincipalName"]);

                // Not in storage account either. Add there
                u = new CachedUserAndConversationData()
                {
                    RowKey = cacheId,
                    ServiceUrl = serviceUrl,
                    UserPrincipalName = user?.UserPrincipalName ?? throw new ArgumentNullException($"No userPrincipalName for {nameof(conversationReference.User.AadObjectId)} '{conversationReference.User.AadObjectId}'"),
                };
                u.ConversationId = conversationReference.Conversation.Id;
                client.AddEntity(u);
            }
            else
            {
                u = entityResponse.Value;
            }
        }

        // Update memory cache
        _userIdConversationCache.AddOrUpdate(cacheId, u, (key, newValue) => u);
    }


    public List<CachedUserAndConversationData> GetCachedUsers()
    {
        return _userIdConversationCache.Values.ToList();
    }

    public CachedUserAndConversationData? GetCachedUser(string aadObjectId)
    {
        return _userIdConversationCache.Values.Where(u => u.RowKey == aadObjectId).SingleOrDefault();
    }

    public bool ContainsUserId(string aadId)
    {
        return _userIdConversationCache.ContainsKey(aadId);
    }
}