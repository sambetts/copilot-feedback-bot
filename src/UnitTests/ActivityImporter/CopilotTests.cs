using ActivityImporter.Engine;
using ActivityImporter.Engine.ActivityAPI;
using ActivityImporter.Engine.ActivityAPI.Copilot;
using ActivityImporter.Engine.ActivityAPI.Models;
using Common.DataUtils;
using Common.Engine.Surveys;
using Entities.DB.Entities;
using Entities.DB.Entities.AuditLog;
using Microsoft.EntityFrameworkCore;
using UnitTests.FakeLoaderClasses;

namespace UnitTests.ActivityImporter;

[TestClass]
public class CopilotTests : AbstractTest
{
    [TestMethod]
    public void SurveyPendingActivitiesGetNext()
    {
        var spa = new SurveyPendingActivities();
        Assert.IsNotNull(spa.FileEvents);
        Assert.IsNotNull(spa.MeetingEvents);
        Assert.IsNull(spa.GetNext());

        var firstFile = new CopilotEventMetadataFile { Event = new CommonAuditEvent { TimeStamp = DateTime.Now.AddDays(-1) } };
        var secondFile = new CopilotEventMetadataFile { Event = new CommonAuditEvent { TimeStamp = DateTime.Now } };
        spa.FileEvents.AddRange(new CopilotEventMetadataFile[] { firstFile, secondFile });

        Assert.IsTrue(spa.GetNext() == firstFile);



        var firstMeeting = new CopilotEventMetadataMeeting { Event = new CommonAuditEvent { TimeStamp = DateTime.Now.AddDays(-1) } };
        var secondMeeting = new CopilotEventMetadataMeeting { Event = new CommonAuditEvent { TimeStamp = DateTime.Now } };
        spa.MeetingEvents.AddRange(new CopilotEventMetadataMeeting[] { firstMeeting, secondMeeting });

        Assert.IsTrue(spa.GetNext() == firstMeeting);


    }

    [TestMethod]
    public async Task FindNewSurveyEventsAndLogSurveyRequested()
    {
        var sm = new SurveyManager(new FakeSurveyManagerDataLoader(_config), new FakeSurveyProcessor(), GetLogger<SurveyManager>());

        var testUser = new User { UserPrincipalName = _config.TestCopilotEventUPN };
        var r = await sm.FindNewSurveyEvents(testUser);

        Assert.IsTrue(r.MeetingEvents.Count == 1);
        Assert.IsTrue(r.FileEvents.Count == 1);
        Assert.IsNotNull(r.MeetingEvents[0]);
        Assert.IsNotNull(r.FileEvents[0]);

        // Request survey
        await sm.Loader.LogSurveyRequested(r.MeetingEvents[0].Event);
        await sm.Loader.LogSurveyRequested(r.FileEvents[0].Event);

        // Survey again
        var r2 = await sm.FindNewSurveyEvents(testUser);

        // Results should be the same as we've already registered the surveys
        Assert.IsTrue(r2.MeetingEvents.Count == 0);
        Assert.IsTrue(r2.FileEvents.Count == 0);
    }

    [TestMethod]
    public async Task CopilotEventManagerSaveTest()
    {
        var copilotEventAdaptor = new CopilotAuditEventManager(_config.ConnectionStrings.SQL, new FakeCopilotEventAdaptor(), _logger);

        var docEvent = new CopilotEventData
        {
            AppHost = "test",
            Contexts = new List<Context>
            {
                new Context
                {
                    Id = _config.TestCopilotDocContextIdSpSite,
                    Type = _config.TeamSiteFileExtension
                }
            }
        };
        var commonEventDocEdit = new CommonAuditEvent
        {
            TimeStamp = DateTime.Now,
            Operation = new EventOperation { Name = "Document Edit" },
            User = new User { AzureAdId = "test", UserPrincipalName = "test" },
            Id = Guid.NewGuid()
        };


        var meeting = new CopilotEventData
        {
            AppHost = "test",
            Contexts = new List<Context>
            {
                new Context
                {
                    Id = "https://microsoft.teams.com/threads/19:meeting_NDQ4MGRhYjgtMzc5MS00ZWMxLWJiZjEtOTIxZmM5Mzg3ZGFi@thread.v2",   // Needs to be real
                    Type = ActivityImportConstants.COPILOT_CONTEXT_TYPE_TEAMSMEETING
                }
            }
        };
        var commonEventMeeting = new CommonAuditEvent
        {
            TimeStamp = DateTime.Now,
            Operation = new EventOperation { Name = "Op" },
            User = new User { AzureAdId = "test", UserPrincipalName = "test" },
            Id = Guid.NewGuid()
        };

        // Clear events 
        _db.CopilotEventMetadataFiles.RemoveRange(_db.CopilotEventMetadataFiles);
        _db.CopilotEventMetadataMeetings.RemoveRange(_db.CopilotEventMetadataMeetings);
        await _db.SaveChangesAsync();

        // Check counts before and after
        var fileEventsPreCount = await _db.CopilotEventMetadataFiles.CountAsync();
        var meetingEventsPreCount = await _db.CopilotEventMetadataMeetings.CountAsync();

        // Save common events as they are required for the foreign key
        _db.AuditEventsCommon.Add(commonEventDocEdit);
        _db.AuditEventsCommon.Add(commonEventMeeting);
        await _db.SaveChangesAsync();

        // Save events
        await copilotEventAdaptor.SaveSingleCopilotEventToSql(meeting, commonEventMeeting);
        await copilotEventAdaptor.SaveSingleCopilotEventToSql(docEvent, commonEventDocEdit);
        await copilotEventAdaptor.CommitAllChanges();

        // Verify counts have increased
        var fileEventsPostCount = await _db.CopilotEventMetadataFiles.CountAsync();
        var meetingEventsPostCount = await _db.CopilotEventMetadataMeetings.CountAsync();
        Assert.IsTrue(fileEventsPostCount == fileEventsPreCount + 1);
        Assert.IsTrue(meetingEventsPostCount == meetingEventsPreCount + 1);
    }

    /// <summary>
    /// Tests we can load metadata from Graph
    /// </summary>
    [TestMethod]
    public async Task GraphCopilotMetadataLoaderTests()
    {
        var auth = new GraphAppIndentityOAuthContext(_logger, _config.AuthConfig.ClientId, _config.AuthConfig.TenantId, _config.AuthConfig.ClientSecret, string.Empty, false);
        await auth.InitClientCredential();

        var loader = new GraphFileMetadataLoader(new Microsoft.Graph.GraphServiceClient(auth.Creds), _logger);

        // Test a file from users OneDrive (my site)
        var mySiteFileInfo = await loader.GetSpoFileInfo(_config.TestCopilotDocContextIdMySites, _config.TestCopilotEventUPN);
        Assert.AreEqual(mySiteFileInfo?.Extension, _config.MySitesFileExtension);
        Assert.AreEqual(mySiteFileInfo?.Filename, _config.MySitesFileName);
        Assert.AreEqual(mySiteFileInfo?.Url, _config.MySitesFileUrl);

        // Test a file from a team site
        var spSiteFileInfo = await loader.GetSpoFileInfo(_config.TestCopilotDocContextIdSpSite, _config.TestCopilotEventUPN);
        Assert.AreEqual(spSiteFileInfo?.Extension, _config.TeamSiteFileExtension);
        Assert.AreEqual(spSiteFileInfo?.Filename, _config.TeamSitesFileName);
        Assert.AreEqual(spSiteFileInfo?.Url, _config.TeamSiteFileUrl);

        // Test a call
        if (!string.IsNullOrEmpty(_config.TestCallThreadId))
        {
            var userId = await loader.GetUserIdFromUpn(_config.TestCopilotEventUPN);
            var meeting = await loader.GetMeetingInfo(StringUtils.GetOnlineMeetingId(_config.TestCallThreadId!, userId), userId);
            Assert.IsNotNull(meeting);
        }
    }
}
