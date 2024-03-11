using Common.Engine;
using Common.Engine.Config;
using Common.Engine.Surveys;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Schema;

namespace Web.Bots.Dialogues.Abstract;

public abstract class CommonBotDialogue : ComponentDialog
{
    protected readonly BotConversationCache _botConversationCache;
    private readonly BotConfig _botConfig;
    protected readonly IServiceProvider _services;

    public CommonBotDialogue(string id, BotConversationCache botConversationCache, BotConfig botConfig, IServiceProvider services)
        : base(id)
    {
        _botConversationCache = botConversationCache;
        _botConfig = botConfig;
        _services = services;
    }


    protected async Task<string?> GetChatUserUPN(WaterfallStepContext stepContext)
    {
        await _botConversationCache.PopulateMemCacheIfEmpty();

        var chatUser = _botConversationCache.GetCachedUser(stepContext.Context.Activity.From.AadObjectId);
        if (chatUser == null)
        {
            return _botConfig.TestUPN;
        }
        return chatUser?.UserPrincipalName;
    }


    public async Task GetSurveyManagerService(Func<SurveyManager, Task> func)
    {
        using (var scope = _services.CreateScope())
        {
            var _surveyManager = scope.ServiceProvider.GetRequiredService<SurveyManager>();
            await func(_surveyManager);
        }
    }
    public async Task<SurveyPendingActivities> GetSurveyPendingActivities(SurveyManager _surveyManager, string chatUserUpn)
    {
        SurveyPendingActivities userPendingEvents;
        Entities.DB.Entities.User? dbUser = null;
        try
        {
            dbUser = await _surveyManager.Loader.GetUser(chatUserUpn);
        }
        catch (ArgumentOutOfRangeException)
        {
            // User doesn't exist, so assume they have no pending events
        }

        if (dbUser != null)
        {
            userPendingEvents = await _surveyManager.FindNewSurveyEvents(dbUser);
        }
        else
        {
            userPendingEvents = new SurveyPendingActivities();
        }
        return userPendingEvents;
    }

    protected async Task SendMsg(ITurnContext context, string msg)
    {
        await context.SendActivityAsync(MessageFactory.Text(msg, msg, InputHints.ExpectingInput));
    }
    protected Activity BuildMsg(string msg)
    {
        return MessageFactory.Text(msg, msg, InputHints.ExpectingInput);
    }
}
