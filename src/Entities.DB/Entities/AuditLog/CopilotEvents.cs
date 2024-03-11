using Entities.DB.Entities.SP;
using System.ComponentModel.DataAnnotations.Schema;

namespace Entities.DB.Entities.AuditLog;

// User stored on common event lookup

public abstract class BaseCopilotEvent : BaseOfficeEvent
{
    [Column("app_host")]
    public string AppHost { get; set; } = null!;

    public abstract string GetEventDescription();
}

[Table("event_meta_copilot_files")]
public class CopilotEventMetadataFile : BaseCopilotEvent
{
    [ForeignKey(nameof(FileExtension))]
    [Column("file_extension_id")]
    public int? FileExtensionId { get; set; } = 0;
    public SPEventFileExtension? FileExtension { get; set; } = null!;

    [ForeignKey(nameof(FileName))]
    [Column("file_name_id")]
    public int? FileNameId { get; set; } = 0;
    public SPEventFileName? FileName { get; set; } = null!;

    [ForeignKey(nameof(Url))]
    [Column("url_id")]
    public int UrlId { get; set; } = 0;
    public Url Url { get; set; } = null!;


    [ForeignKey(nameof(Site))]
    [Column("site_id")]
    public int SiteId { get; set; } = 0;
    public Site Site { get; set; } = null!;

    public override string GetEventDescription()
    {
        return $"{Event.Operation.Name} on {FileName?.Name}";
    }
}

[Table("event_meta_copilot_meetings")]
public class CopilotEventMetadataMeeting : BaseCopilotEvent
{
    [ForeignKey(nameof(OnlineMeeting))]
    [Column("meeting_id")]
    public int OnlineMeetingId { get; set; }

    public OnlineMeeting OnlineMeeting { get; set; } = null!;

    public override string GetEventDescription()
    {
        return $"{Event.Operation.Name} on {OnlineMeeting.Name}";
    }
}

