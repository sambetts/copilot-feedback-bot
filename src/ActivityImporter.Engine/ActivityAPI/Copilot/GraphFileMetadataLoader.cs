using ActivityImporter.Engine.ActivityAPI.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.Graph.Models.ODataErrors;

namespace ActivityImporter.Engine.ActivityAPI.Copilot;

/// <summary>
/// Populates file metadata from Graph API
/// </summary>
public class GraphFileMetadataLoader : ICopilotMetadataLoader
{
    private readonly GraphServiceClient _graphServiceClient;
    private readonly SiteGraphCache _siteGraphCache;
    private readonly UserGraphCache _userGraphCache;
    private readonly ILogger _logger;

    public GraphFileMetadataLoader(GraphServiceClient graphServiceClient, ILogger logger)
    {
        _graphServiceClient = graphServiceClient;
        _logger = logger;
        _siteGraphCache = new SiteGraphCache(graphServiceClient);
        _userGraphCache = new UserGraphCache(graphServiceClient);
    }

    public async Task<MeetingMetadata> GetMeetingInfo(string meetingId, string userGuid)
    {
        // Requires OnlineMeetings.Read.All and https://learn.microsoft.com/en-us/graph/cloud-communication-online-meeting-application-access-policy#configure-application-access-policy
        var meeting = await _graphServiceClient.Users[userGuid].OnlineMeetings[meetingId].GetAsync();

        return new MeetingMetadata(meeting);
    }

    // Example: https://m365cp123890-my.sharepoint.com/personal/sambetts_m365cp123890_onmicrosoft_com/_layouts/15/Doc.aspx?sourcedoc=%7B0D86F64F-8435-430C-8979-FF46C00F7ACB%7D&file=Presentation.pptx&action=edit&mobileredirect=true
    public async Task<SpoDocumentFileInfo?> GetSpoFileInfo(string copilotDocContextId, string eventUpn)
    {
        var siteUrl = StringUtils.GetSiteUrl(copilotDocContextId);
        if (siteUrl == null) throw new ArgumentException("Invalid copilotDocContextId");
        
        Drive? drive;
        if (StringUtils.IsMySiteUrl(siteUrl))
        {
            drive = await GetSpoInfoFromMySiteUrl(eventUpn);
        }
        else
        {
            drive = await GetSpoInfoFromSiteUrl(siteUrl);
        }
        if (drive == null)
        {
            return null;
        }

        // Get site ID from url
        // https://learn.microsoft.com/en-us/graph/api/drive-get?view=graph-rest-beta&tabs=http
        var spSiteId = drive.SharePointIds?.SiteId;
        if (string.IsNullOrEmpty(spSiteId))
        {
            throw new ArgumentOutOfRangeException("SharePointIds.SiteId");
        }
        var spListId = drive.SharePointIds?.ListId;
        if (string.IsNullOrEmpty(spListId))
        {
            throw new ArgumentOutOfRangeException("SharePointIds.ListId");
        }
        var driveItemId = StringUtils.GetDriveItemId(copilotDocContextId);

        if (driveItemId != null)
        {
            var site = await _siteGraphCache.GetResourceOrNullIfNotExists(spSiteId);

            ListItem? item;
            try
            {
                item = await _graphServiceClient.Sites[spSiteId].Lists[spListId].Items[driveItemId]
                    .GetAsync(op => op.QueryParameters.Expand = ["fields"]);
            }
            catch (ODataError ex)
            {
                _logger.LogWarning(ex, "Error getting file info for copilotDocContextId {copilotDocContextId}", copilotDocContextId);
                return null;
            }
            

            return new SpoDocumentFileInfo(item, site);
        }
        else
        {
            _logger.LogWarning("No driveItemId found in copilotDocContextId {copilotDocContextId}", copilotDocContextId);
            return null;
        }
    }

    public async Task<string> GetUserIdFromUpn(string userPrincipalName)
    {
        var user = await _userGraphCache.GetResource(userPrincipalName);
        return user.Id ?? throw new Exception($"No user ID found on user in Graph by upn {userPrincipalName}");
    }

    private async Task<Drive?> GetSpoInfoFromMySiteUrl(string eventUpn)
    {
        // Needs Files.Read.All
        try
        {
            return await _graphServiceClient.Users[eventUpn].Drive.GetAsync(o => o.QueryParameters.Select = ["SharePointIds"]) ?? throw new ArgumentOutOfRangeException(eventUpn);
        }
        catch (ODataError ex)
        {
            _logger.LogWarning(ex, "Error getting drive info for user {eventUpn}", eventUpn);
            return null;
        }
    }

    private async Task<Drive?> GetSpoInfoFromSiteUrl(string siteUrl)
    {
        var siteAddress = StringUtils.GetHostAndSiteRelativeUrl(siteUrl);
        if (siteAddress == null)
        {
            throw new ArgumentException("Invalid copilotDocContextId");
        }

        // Get drive ID from site ID
        try
        {
            return await _graphServiceClient.Sites[siteAddress].Drive.GetAsync(o => o.QueryParameters.Select = ["SharePointIds"]) ?? throw new ArgumentOutOfRangeException(siteAddress);
        }
        catch (ODataError ex)
        {
            _logger.LogWarning(ex, "Error getting drive info for site {siteUrl}", siteUrl);
            return null;
        }
    }
}
