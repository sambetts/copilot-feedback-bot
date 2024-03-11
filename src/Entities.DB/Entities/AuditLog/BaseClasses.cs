using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Entities.DB.Entities.AuditLog;

public abstract class BaseOfficeEvent
{
    /// <summary>
    /// Foriegn key for "Event" only
    /// </summary>
    [Key]
    [ForeignKey(nameof(Event))]
    [Column("event_id")]
    public Guid EventID { get; set; }

    public CommonAuditEvent Event { get; set; } = null!;
}
