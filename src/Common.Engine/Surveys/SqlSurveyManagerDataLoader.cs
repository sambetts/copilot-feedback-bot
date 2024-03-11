using Entities.DB;
using Entities.DB.Entities;
using Entities.DB.Entities.AuditLog;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Common.Engine.Surveys;


public class SqlSurveyManagerDataLoader(DataContext db, ILogger<SqlSurveyManagerDataLoader> logger) : ISurveyManagerDataLoader
{
    public async Task<User> GetUser(string upn)
    {
        return await db.Users.Where(u => u.UserPrincipalName == upn).FirstOrDefaultAsync() ?? throw new ArgumentOutOfRangeException(nameof(upn));
    }

    public async Task<DateTime?> GetLastUserSurveyDate(User user)
    {
        var latestUserRespondedSurvey = await db.SurveyResponses
            .Where(e => e.User == user)
            .OrderBy(e => e.Responded).Take(1)
            .FirstOrDefaultAsync();

        if (latestUserRespondedSurvey != null)
        {
            return latestUserRespondedSurvey.Responded;
        }
        return null;
    }

    public async Task<List<BaseCopilotEvent>> GetUnsurveyedActivities(User user, DateTime? from)
    {
        var useRespondedEvents = await db.SurveyResponses
            .Include(e => e.RelatedEvent)
            .Where(e => e.RelatedEvent != null && e.RelatedEvent.User == user && (!from.HasValue || e.Requested > from))
            .Select(e => e.RelatedEvent)
            .ToListAsync();

        var fileEvents = await db.CopilotEventMetadataFiles
            .Include(e => e.Event)
                .ThenInclude(e => e.Operation)
            .Include(e => e.FileName)
            .Where(e => !useRespondedEvents.Contains(e.Event) && e.Event.User == user && (!from.HasValue || e.Event.TimeStamp > from)).ToListAsync();

        var meetingEvents = await db.CopilotEventMetadataMeetings
            .Include(e => e.Event)
                .ThenInclude(e => e.Operation)
            .Include(e => e.OnlineMeeting)
            .Where(e => !useRespondedEvents.Contains(e.Event) && e.Event.User == user && (!from.HasValue || e.Event.TimeStamp > from)).ToListAsync();

        return fileEvents.Cast<BaseCopilotEvent>().Concat(meetingEvents).ToList();
    }

    public Task LogSurveyRequested(CommonAuditEvent @event)
    {
        db.SurveyResponses.Add(new UserSurveyResponse { RelatedEventId = @event.Id, Requested = DateTime.UtcNow, UserID = @event.UserId });
        return db.SaveChangesAsync();
    }

    public async Task<List<User>> GetUsersWithActivity()
    {
        return await db.Users
            .Where(u => db.CopilotEventMetadataFiles.Where(e => e.Event.User == u).Any() || db.CopilotEventMetadataMeetings.Where(e => e.Event.User == u).Any())
            .ToListAsync();
    }

    public async Task UpdateSurveyResult(CommonAuditEvent @event, int score)
    {
        var response = await db.SurveyResponses.Where(e => e.RelatedEvent == @event).FirstOrDefaultAsync();
        if (response != null)
        {
            response.Rating = score;
            await db.SaveChangesAsync();
        }
    }
    public async Task<int> LogDisconnectedSurveyResult(int scoreGiven, string userUpn)
    {
        var user = await db.Users.Where(u => u.UserPrincipalName == userUpn).FirstOrDefaultAsync();
        if (user == null)
        {
            user = new User { UserPrincipalName = userUpn };
        }

        var survey = new UserSurveyResponse { Rating = scoreGiven, Requested = DateTime.UtcNow, User = user };
        db.SurveyResponses.Add(survey);
        await db.SaveChangesAsync();
        return survey.ID;
    }

    public async Task StopBotheringUser(string upn, DateTime until)
    {
        var user = await db.Users.Where(u => u.UserPrincipalName == upn).FirstOrDefaultAsync();
        if (user != null)
        {
            logger.LogInformation("User {upn} has been asked to stop bothering until {until}", upn, until);
            user.MessageNotBefore = until;
            await db.SaveChangesAsync();
        }
        else
        {
            logger.LogWarning("User {upn} not found", upn);
        }
    }

}
