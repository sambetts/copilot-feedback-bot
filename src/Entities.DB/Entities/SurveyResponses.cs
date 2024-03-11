using Entities.DB.Entities.AuditLog;
using System.ComponentModel.DataAnnotations.Schema;

namespace Entities.DB.Entities;

[Table("survey_responses")]
public class UserSurveyResponse : UserRelatedEntity
{
    [Column("responded")]
    public DateTime Responded { get; set; }

    [Column("requested")]
    public DateTime Requested { get; set; }

    [Column("rating")]
    public int Rating { get; set; }

    [Column("estimated_time_saved_minutes")]
    public int? EstimatedTimeSavedMinutes { get; set; }

    [Column("comment")]
    public string? Comment { get; set; }

    [ForeignKey(nameof(RelatedEvent))]
    [Column("related_audit_event_id")]
    public Guid? RelatedEventId { get; set; }
    public CommonAuditEvent? RelatedEvent { get; set; } = null!;
}

/// <summary>
/// Associate a survey response with an activity type
/// </summary>
[Table("survey_response_activity_types")]
public class UserSurveyResponseActivityType : AbstractEFEntity
{

    [ForeignKey(nameof(UserSurveyResponse))]
    [Column("user_response_id")]
    public int UserSurveyResponseId { get; set; }
    public UserSurveyResponse UserSurveyResponse { get; set; } = null!;


    [ForeignKey(nameof(CopilotActivity))]
    [Column("copilot_activity_id")]
    public int CopilotActivityId { get; set; }
    public CopilotActivity CopilotActivity { get; set; } = null!;
}
