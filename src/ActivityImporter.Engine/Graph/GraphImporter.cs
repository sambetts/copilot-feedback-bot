﻿using ActivityImporter.Engine.Graph.GraphUser;
using ActivityImporter.Engine.Graph.O365UsageReports.Models;
using ActivityImporter.Engine.Graph.O365UsageReports.ReportLoaders.ActivityLoaders;
using Common.DataUtils;
using Common.Engine.Config;
using Entities.DB;
using Entities.DB.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Graph;

namespace ActivityImporter.Engine.Graph;

/// <summary>
/// Reads and saves all data read from Graph
/// </summary>
public class GraphImporter : AbstractApiLoader
{
    #region Constructors & Props

    private readonly GraphAppIndentityOAuthContext _graphAppIndentityOAuthContext;
    private GraphServiceClient? _graphClient = null;
    private readonly AppConfig _appConfig;

    public GraphImporter(AppConfig appConfig, ILogger telemetry) : base(telemetry)
    {
        _graphAppIndentityOAuthContext = new GraphAppIndentityOAuthContext(telemetry, appConfig.AuthConfig.ClientId, appConfig.AuthConfig.TenantId, appConfig.AuthConfig.ClientSecret, string.Empty, false);
        _appConfig = appConfig;
    }

    #endregion

    async Task InitAuth()
    {
        await _graphAppIndentityOAuthContext.InitClientCredential();
        _graphClient = new GraphServiceClient(_graphAppIndentityOAuthContext.Creds);
    }

    /// <summary>
    /// Main entry-point
    /// </summary>
    public async Task GetAndSaveAllGraphData()
    {
        await InitAuth();

        if (_graphAppIndentityOAuthContext.Creds == null)
        {
            throw new Exception("No Graph credentials found");
        }

        var httpClient = new ManualGraphCallClient(_graphAppIndentityOAuthContext, _telemetry);

        var userMetadaTimer = new JobTimer(_telemetry, "User metadata refresh");
        userMetadaTimer.Start();

        // Update Graph users first
        var userUpdater = new UserMetadataUpdater(_appConfig, _telemetry, _graphAppIndentityOAuthContext.Creds, httpClient);
        await userUpdater.InsertAndUpdateDatabaseUsersFromGraph();

        // Track finished event 
        userMetadaTimer.TrackFinishedEventAndStopTimer(AnalyticsEvent.FinishedSectionImport);


        var optionsBuilder = new DbContextOptionsBuilder<DataContext>();
        optionsBuilder.UseSqlServer(_appConfig.ConnectionStrings.SQL);

        using (var db = new DataContext(optionsBuilder.Options))
        {
            var usageActivityTimer = new JobTimer(_telemetry, "Usage reports");
            usageActivityTimer.Start();

            // Global user activity report. Each thread creates own context.
            await GetAndSaveActivityReportsMultiThreaded(6, httpClient);

            // Track finished event 
            usageActivityTimer.TrackFinishedEventAndStopTimer(AnalyticsEvent.FinishedSectionImport);

        }
    }

    public async Task GetAndSaveActivityReportsMultiThreaded(int daysBackMax, ManualGraphCallClient client)
    {
        _telemetry.LogInformation($"\nReading all activity reports from {daysBackMax} days back...");

        // Parallel-load all, each one with own DB context
        var importTasks = new List<Task>();

        var lookupIdCache = new ConcurrentLookupDbIdsCache();

        var teamsUserUsageLoader = new TeamsUserUsageLoader(client, _telemetry);
        importTasks.Add(LoadAndSaveReportAsync(teamsUserUsageLoader, daysBackMax, "Teams user activity", _telemetry, lookupIdCache));


        var outlookLoader = new OutlookUserActivityLoader(client, _telemetry);
        importTasks.Add(LoadAndSaveReportAsync(outlookLoader, daysBackMax, "Outlook activity", _telemetry, lookupIdCache));

        var oneDriveUserActivityLoader = new OneDriveUserActivityLoader(client, _telemetry);
        importTasks.Add(LoadAndSaveReportAsync(oneDriveUserActivityLoader, daysBackMax, "OneDrive activity", _telemetry, lookupIdCache));

        var sharePointUserActivityLoader = new SharePointUserActivityLoader(client, _telemetry);
        importTasks.Add(LoadAndSaveReportAsync(sharePointUserActivityLoader, daysBackMax, "SharePoint user activity", _telemetry, lookupIdCache));

        await Task.WhenAll(importTasks);


        // Check for anonimised data
        var allTeamsData = teamsUserUsageLoader.LoadedReportPages.SelectMany(r => r.Value).ToList();
        if (allTeamsData.Count > 0)
        {
            if (!Common.DataUtils.CommonStringUtils.IsEmail(allTeamsData[0].UserPrincipalName))
            {
                _telemetry.LogInformation($"\nWARNING: Usage reports have associated user email concealed - we won't be able to link any activity back to users. See Office 365 Advanced Analytics Engine prerequisites.\n");
            }
        }

        _telemetry.LogInformation($"Activity reports imported.\n");
    }

    async Task<int> LoadAndSaveReportAsync<TReportDbType, TUserActivityUserDetail>
        (AbstractActivityLoader<TReportDbType, TUserActivityUserDetail> abstractActivityLoader,
        int daysBackMax, string thingWeAreImporting, ILogger telemetry, ConcurrentLookupDbIdsCache userEmailToDbIdCache)
        where TReportDbType : AbstractUsageActivityLog, new()
    where TUserActivityUserDetail : AbstractActivityRecord
    {
        telemetry.LogInformation($"Importing {thingWeAreImporting} reports...");
        await abstractActivityLoader.PopulateLoadedReportPagesFromGraph(daysBackMax);

        var optionsBuilder = new DbContextOptionsBuilder<DataContext>();
        optionsBuilder.UseSqlServer(_appConfig.ConnectionStrings.SQL);

        using (var db = new DataContext(optionsBuilder.Options))
        {
            _telemetry.LogInformation($"Read {abstractActivityLoader.LoadedReportPages.SelectMany(p => p.Value).Count().ToString("N0")} {thingWeAreImporting} records from Graph API");
            await abstractActivityLoader.SaveLoadedReportsToSql(userEmailToDbIdCache, db, new Entities.DB.LookupCaches.Discrete.UserCache(db));
        }

        var total = abstractActivityLoader.LoadedReportPages.SelectMany(r => r.Value).Count();
        telemetry.LogInformation($"Imported {total.ToString("N0")} {thingWeAreImporting} reports.");

        return total;
    }
}
