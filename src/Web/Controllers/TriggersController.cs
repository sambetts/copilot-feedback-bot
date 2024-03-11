using Common.Engine.Notifications;
using Common.Engine.Surveys;
using Microsoft.AspNetCore.Mvc;

namespace Web.Controllers;

[Route("api/[controller]")]
[ApiController]
public class TriggersController : ControllerBase
{
    private readonly SurveyManager _surveyManager;
    private readonly IBotConvoResumeManager _botConvoResumeManager;

    public TriggersController(SurveyManager surveyManager, IBotConvoResumeManager botConvoResumeManager)
    {
        _surveyManager = surveyManager;
        _botConvoResumeManager = botConvoResumeManager;
    }

    // Send surveys to all users that have new survey events, installing bot for users that don't have it
    // POST: api/Triggers/SendSurveys
    [HttpPost(nameof(SendSurveys))]
    public async Task<IActionResult> SendSurveys()
    {
        var sent = await _surveyManager.FindAndProcessNewSurveyEventsAllUsers();
        return Ok($"Sent {sent} new surveys");
    }

    // Force install bot for a user, regardless of whether they have any copilot activity or not
    // POST: api/Triggers/InstallBotForUser
    [HttpPost(nameof(InstallBotForUser))]
    public async Task<IActionResult> InstallBotForUser(string upn)
    {
        await _botConvoResumeManager.ResumeConversation(upn);
        return Ok($"Bot installed for user {upn}");
    }
}
