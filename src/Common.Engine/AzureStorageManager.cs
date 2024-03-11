using Common.Engine.Notifications;

namespace Common.Engine;


public interface IAzureStorageManager
{
    Task ClearPropertyValue(string property);

    Task<PropertyBagEntry?> GetPropertyValue(string property);

    Task SetPropertyValue(string property, string value);
}

/// <summary>
/// Handles Azure blob & table storage operations
/// </summary>
public class AzureStorageManager : TableStorageManager, IAzureStorageManager
{
    public const string AzureTablePropertyBag = "propertybag";

    public AzureStorageManager(string storageConnectionString) : base(storageConnectionString)
    {
    }

    #region PropertyBag

    public async Task<PropertyBagEntry?> GetPropertyValue(string property)
    {
        var tableClient = await GetTableClient(AzureTablePropertyBag);

        var queryResultsFilter = tableClient.QueryAsync<PropertyBagEntry>(f =>
            f.RowKey == property
        );

        // Iterate the <see cref="Pageable"> to access all queried entities.
        await foreach (var qEntity in queryResultsFilter)
        {
            return qEntity;
        }

        // No results
        return null;
    }

    public async Task SetPropertyValue(string property, string value)
    {
        var tableClient = await GetTableClient(AzureTablePropertyBag);

        var entity = new PropertyBagEntry(property, value);

        // Entity doesn't exist in table, so invoking UpsertEntity will simply insert the entity.
        await tableClient.UpsertEntityAsync(entity);
    }

    public async Task ClearPropertyValue(string property)
    {
        var tableClient = await GetTableClient(AzureTablePropertyBag);
        tableClient.DeleteEntity(PropertyBagEntry.PARTITION_NAME, property);
    }

    #endregion
}
