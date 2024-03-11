using Common.Engine;
using Common.Engine.Config;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Schema;

namespace Web.Bots;


public class FeedbackBot<T> : DialogueBot<T> where T : Dialog
{
    public readonly AppConfig _configuration;
    private readonly BotActionsHelper _helper;
    BotConversationCache _conversationCache;

    public FeedbackBot(ConversationState conversationState, UserState userState, T dialog, ILogger<DialogueBot<T>> logger, BotActionsHelper helper, AppConfig configuration, BotConversationCache botConversationCache)
        : base(conversationState, userState, dialog, logger)
    {
        _helper = helper;
        _conversationCache = botConversationCache;
        _configuration = configuration;
    }

    protected override async Task OnMembersAddedAsync(IList<ChannelAccount> membersAdded, ITurnContext<IConversationUpdateActivity> turnContext, CancellationToken cancellationToken)
    {
        foreach (var member in membersAdded)
        {
            if (member.Id != turnContext.Activity.Recipient.Id)
            {
                // Is this an Azure AD user?
                if (string.IsNullOrEmpty(member.AadObjectId))
                    await turnContext.SendActivityAsync(MessageFactory.Text($"Hi, anonynous user. I only work with Azure AD users in Teams normally..."));
                else
                {
                    // Add current user to conversation reference cache.
                    await _conversationCache.AddConversationReferenceToCache((Activity)turnContext.Activity);
                }

                // First time meeting a user (new thread). Can be because we've just installed the app. Introduce bot and start a new dialog.
                await _helper.SendBotFirstIntro(turnContext, cancellationToken);
            }
        }
    }

    protected override async Task OnTeamsSigninVerifyStateAsync(ITurnContext<IInvokeActivity> turnContext, CancellationToken cancellationToken)
    {
        Logger.LogInformation("Running dialog with signin/verifystate from an Invoke Activity.");

        // The OAuth Prompt needs to see the Invoke Activity in order to complete the login process.

        // Run the Dialog with the new Invoke Activity.
        await Dialog.RunAsync(turnContext, ConversationState.CreateProperty<DialogState>(nameof(DialogState)), cancellationToken);
    }
}
