using ActivityImporter.Engine.ActivityAPI.Models;
using Common.DataUtils.Sql.Inserts;
using Entities.DB.Entities.AuditLog;
using Microsoft.Extensions.Logging;

namespace ActivityImporter.Engine.ActivityAPI.Copilot;

/// <summary>
/// Saves copilot event metadata to SQL
/// </summary>
public class CopilotAuditEventManager
{
    private readonly ICopilotMetadataLoader _copilotEventAdaptor;
    private readonly ILogger _logger;
    private readonly InsertBatch<SPCopilotLogTempEntity> _spCopilotInserts;
    private readonly InsertBatch<TeamsCopilotLogTempEntity> _teamsCopilotInserts;

    public CopilotAuditEventManager(string connectionString, ICopilotMetadataLoader copilotEventAdaptor, ILogger logger)
    {
        _copilotEventAdaptor = copilotEventAdaptor;
        _logger = logger;

        _spCopilotInserts = new InsertBatch<SPCopilotLogTempEntity>(connectionString, logger);
        _teamsCopilotInserts = new InsertBatch<TeamsCopilotLogTempEntity>(connectionString, logger);
    }

    public async Task SaveSingleCopilotEventToSql(CopilotEventData eventData, CommonAuditEvent baseOfficeEvent)
    {
        _logger.LogInformation($"Saving copilot event metadata to SQL for event {baseOfficeEvent.Id}");

        int meetingsCount = 0, filesCount = 0;
        foreach (var context in eventData.Contexts)
        {
            if (context.Type == ActivityImportConstants.COPILOT_CONTEXT_TYPE_TEAMSMEETING)
            {
                // We need the user guid to construct the meeting ID
                var userGuid = await _copilotEventAdaptor.GetUserIdFromUpn(baseOfficeEvent.User.UserPrincipalName);

                // Construct meeting ID from user GUID and thread ID
                var meetingId = StringUtils.GetOnlineMeetingId(context.Id, userGuid);

                var meetingInfo = await _copilotEventAdaptor.GetMeetingInfo(meetingId, userGuid);
                _teamsCopilotInserts.Rows.Add(new TeamsCopilotLogTempEntity
                {
                    EventId = baseOfficeEvent.Id,
                    AppHost = eventData.AppHost,
                    MeetingId = meetingId!,
                    MeetingCreatedUTC = meetingInfo.CreatedUTC,
                    MeetingName = meetingInfo.Subject
                });

                meetingsCount++;
            }
            else
            {
                // Load from Graph the SPO file info
                var spFileInfo = await _copilotEventAdaptor.GetSpoFileInfo(context.Id, baseOfficeEvent.User.UserPrincipalName);

                if (spFileInfo != null)
                {
                    // Use the bulk insert 
                    _spCopilotInserts.Rows.Add(new SPCopilotLogTempEntity
                    {
                        EventId = baseOfficeEvent.Id,
                        AppHost = eventData.AppHost,
                        FileExtension = spFileInfo.Extension,
                        FileName = spFileInfo.Filename,
                        Url = spFileInfo.Url,
                        UrlBase = spFileInfo.SiteUrl
                    });
                    filesCount++;
                }
                else
                {
                    _logger.LogWarning("No file info found for copilotDocContextId {copilotDocContextId}", context.Id);
                }
            }
        }

        if (meetingsCount > 0 || filesCount > 0)
        {
            _logger.LogInformation($"Saved {meetingsCount} meetings and {filesCount} files to SQL for event {baseOfficeEvent.Id}");
        }
        else
        {
            // AppChat?
            _logger.LogTrace($"No copilot event metadata saved to SQL for event {baseOfficeEvent.Id} for host '{eventData.AppHost}'");
        }
    }


    public async Task CommitAllChanges()
    {
        var docsMergeSql = Properties.Resources.insert_sp_copilot_events_from_staging_table
            .Replace(ActivityImportConstants.STAGING_TABLE_VARNAME,
            ActivityImportConstants.STAGING_TABLE_COPILOT_SP);
        var teamsMergeSql = Properties.Resources.insert_teams_copilot_events_from_staging_table
            .Replace(ActivityImportConstants.STAGING_TABLE_VARNAME,
            ActivityImportConstants.STAGING_TABLE_COPILOT_TEAMS);

        await _spCopilotInserts.SaveToStagingTable(docsMergeSql);
        await _teamsCopilotInserts.SaveToStagingTable(teamsMergeSql);
    }
}

public interface ICopilotMetadataLoader
{
    Task<SpoDocumentFileInfo?> GetSpoFileInfo(string copilotId, string eventUpn);
    Task<MeetingMetadata> GetMeetingInfo(string threadId, string userGuid);
    Task<string> GetUserIdFromUpn(string userPrincipalName);
}
