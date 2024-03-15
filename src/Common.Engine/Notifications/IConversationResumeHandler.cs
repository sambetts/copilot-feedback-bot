using Entities.DB.Entities.AuditLog;
using Microsoft.Bot.Schema;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.Engine.Notifications;

public interface IConversationResumeHandler
{
    Task<(BaseCopilotEvent?, Attachment)> GetProactiveConversationResumeConversationCard(string chatUserUpn);
}
