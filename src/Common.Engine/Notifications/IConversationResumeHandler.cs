using Entities.DB.Entities.AuditLog;
using Microsoft.Bot.Schema;

namespace Common.Engine.Notifications;

public interface IConversationResumeHandler
{
    Task<(BaseCopilotEvent?, Attachment)> GetProactiveConversationResumeConversationCard(string chatUserUpn);
}
