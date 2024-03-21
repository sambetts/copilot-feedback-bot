using ActivityImporter.Engine.ActivityAPI;
using ActivityImporter.Engine.ActivityAPI.Loaders;
using ActivityImporter.Engine.Graph;
using Common.Engine.Config;
using Entities.DB;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Graph.Models.ODataErrors;

namespace ActivityImporter.ConsoleApp;

internal class ProgramTasks
{
    private readonly AppConfig _importConfig;
    private readonly ILogger _logger;

    public ProgramTasks(AppConfig importConfig, ILogger logger)
    {
        _importConfig = importConfig;
        _logger = logger;
    }

    /// <summary>
    /// Graph data
    /// </summary>
    internal async Task GetGraphData()
    {
        _logger.LogInformation("Starting Graph imports.");
        var graphReader = new GraphImporter(_importConfig, _logger);

        try
        {
            await graphReader.GetAndSaveAllGraphData();
        }
        catch (ODataError ex)
        {
            _logger.LogError(ex.ToString());

            // Don't make a drama if Graph permissions aren't assigned yet.
            if (ex.ResponseStatusCode == 403)
            {
                _logger.LogInformation("ERROR: Can't access Teams user data - are application permissions configured correctly?");
                return;
            }
            else
            {
                throw;
            }
        }

        _logger.LogInformation("Finished Graph API import tasks.");
    }

    /// <summary>
    /// Activity API
    /// </summary>
    internal async Task DownloadAndSaveActivityData()
    {
        // Remember start time
        var startTime = DateTime.Now;

        var optionsBuilder = new DbContextOptionsBuilder<DataContext>();
        optionsBuilder.UseSqlServer(_importConfig.ConnectionStrings.SQL);

        using (var db = new DataContext(optionsBuilder.Options))
        {
            var spFilterList = await SharePointOrgUrlsFilterConfig.Load(db);

            if (spFilterList.OrgUrlConfigs.Count == 0)
            {
                _logger.LogError("FATAL ERROR: No org URLs found in database! " +
                    "This means everything would be ignored for SharePoint audit data. Add at least one URL to the org_urls table for this to work.");

                return;

            }

            _logger.LogInformation("\nBeginning import. Filtering for SharePoint events below these URLs:");

            // Print URLs
            spFilterList.Print(_logger);
            Console.WriteLine();

            _logger.LogInformation($"\nStarting activity import for {spFilterList.OrgUrlConfigs.Count} url filters");

            // Start new O365 activity download session
            const int MAX_IMPORTS_PER_BATCH = 100000;
            var importer = new ActivityWebImporter(_importConfig.AuthConfig, _logger, MAX_IMPORTS_PER_BATCH);

            var sqlAdaptor = new ActivityReportSqlPersistenceManager(db, _importConfig, spFilterList, _logger);
            try
            {
                var stats = await importer.LoadReportsAndSave(sqlAdaptor);

                // Output stats
                _logger.LogInformation($"Finished activity import. Time taken in = {DateTime.Now.Subtract(startTime).TotalMinutes.ToString("N2")} minutes. Stats: {stats}", Microsoft.ApplicationInsights.DataContracts.SeverityLevel.Information);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError($"Got unexpected exception importing activity: {ex.Message}");
            }
        }

    }
}
