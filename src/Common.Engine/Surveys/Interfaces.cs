using Entities.DB.Entities;
using Entities.DB.Entities.AuditLog;

namespace Common.Engine.Surveys;

public interface ISurveyManagerDataLoader
{
    Task<DateTime?> GetLastUserSurveyDate(User user);
    Task<List<BaseCopilotEvent>> GetUnsurveyedActivities(User user, DateTime? from);
    Task<User> GetUser(string upn);
    Task<List<User>> GetUsersWithActivity();
    Task<int> LogDisconnectedSurveyResult(int scoreGiven, string userUpn);
    Task LogSurveyRequested(CommonAuditEvent @event);

    Task StopBotheringUser(string upn, DateTime until);
    Task UpdateSurveyResult(CommonAuditEvent @event, int score);
}

public interface ISurveyProcessor
{
    Task ProcessSurveyRequest(SurveyPendingActivities activities);
}
