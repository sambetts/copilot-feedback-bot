using Common.Engine;
using Common.Engine.Config;
using Common.Engine.Surveys;
using Entities.DB.Entities.AuditLog;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Builder.Dialogs.Choices;
using Microsoft.Bot.Schema;
using System.Text.Json;
using Web.Bots.Cards;
using Web.Bots.Dialogues.Abstract;

namespace Web.Bots.Dialogues;

/// <summary>
/// Entrypoint to all new conversations
/// </summary>
public class SurveyDialogue : StoppableDialogue
{
    private readonly ILogger<SurveyDialogue> _tracer;
    private readonly UserState _userState;

    const string CACHE_NAME_NEXT_ACTION = "NextAction";
    const string BTN_SEND_SURVEY = "Go on then";

    /// <summary>
    /// Setup dialogue flow
    /// </summary>
    public SurveyDialogue(StopBotheringMeDialogue stopBotheringMeDialogue, BotConfig configuration, BotConversationCache botConversationCache, ILogger<SurveyDialogue> tracer,
        UserState userState, IServiceProvider services)
        : base(nameof(SurveyDialogue), botConversationCache, configuration, services)
    {
        _tracer = tracer;
        _userState = userState;
        AddDialog(new TextPrompt(nameof(TextPrompt)));
        AddDialog(stopBotheringMeDialogue);

        AddDialog(new WaterfallDialog(nameof(WaterfallDialog), new WaterfallStep[]
        {
            NewChat,
            SendSurveyOrNot,
            ProcessSurveyResponse
        }));
        AddDialog(new ChoicePrompt(nameof(ChoicePrompt)));
        InitialDialogId = nameof(WaterfallDialog);
    }

    /// <summary>
    /// Main entry-point for bot new chat. User is either responding to the intro card or has said something to the bot.
    /// </summary>
    private async Task<DialogTurnResult> NewChat(WaterfallStepContext stepContext, CancellationToken cancellationToken)
    {
        // Check if user wants to stop the bot from bothering them
        var cancel = await base.InterruptAsync(stepContext, cancellationToken);
        if (cancel != null)
            return cancel;

        var chatUserUpn = await base.GetChatUserUPN(stepContext);

        if (chatUserUpn == null)
        {
            // This may be the first time we've met this user if they've spoken to the bot, but the conversation cache has been cleared.
            // Add them to the cache and try again.
            if (stepContext.Context?.Activity?.From?.AadObjectId != null)
            {
                await base._botConversationCache.AddConversationReferenceToCache(stepContext.Context.Activity);
                chatUserUpn = await base.GetChatUserUPN(stepContext);
            }

            // Now we've tried to add their user to the conversation cache, check again
            if (chatUserUpn == null)
            {
                // Not a Teams user. End the conversation.
                await SendMsg(stepContext.Context!,
                    "Hi, Teams User. For some reason I can't find you in our chat history, which is very odd. " +
                    "It looks like I can't do much without this.");
                return await stepContext.EndDialogAsync();
            }
        }

        // Are we in this diag from a response to the initial introduction card button?
        // Initial introduction card is sent when bot meets a new user for the 1st time in a new thread. 
        var prevActionText = stepContext.Context?.Activity?.Text;
        if (prevActionText == "{}" || prevActionText == "Start Survey")              // In teams, this is empty JSon for some reason, unlike in Bot Framework SDK client

            // User starts the dialogue with "start survey", probably from the intro card.
            return await stepContext.NextAsync(new FoundChoice() { Value = BTN_SEND_SURVEY });
        else
        {
            // We're here because the user said something to the bot outside this dialogue flow.
            // Probably because they said "sup" or something to us, randomly

            // "Hi I'm bot..."
            var introCardAttachment = new BotResumeConversationIntroduction(BotConstants.BotName).GetCardAttachment();
            await stepContext.Context!.SendActivityAsync(MessageFactory.Attachment(introCardAttachment));

            return await stepContext.PromptAsync(nameof(ChoicePrompt), new PromptOptions
            {
                Prompt = BuildMsg("Want to fill out a survey?"),
                Choices = new List<Choice>() {
                    new Choice() { Value = BTN_SEND_SURVEY, Synonyms = new List<string>() { "Yes", "Do it", "Send" } },
                    new Choice() { Value = "Nah", Synonyms = new List<string>() { "No", "Stop", "Abort" } }
                }
            }, cancellationToken);
        }
    }

    /// <summary>
    /// User has chosen to fill out a survey or not
    /// </summary>
    private async Task<DialogTurnResult> SendSurveyOrNot(WaterfallStepContext stepContext, CancellationToken cancellationToken)
    {
        var response = (FoundChoice)stepContext.Result;
        if (response != null)
        {
            if (response.Value == BTN_SEND_SURVEY)
            {
                var chatUserUpn = await base.GetChatUserUPN(stepContext) ?? throw new ArgumentNullException(nameof(stepContext.Context.Activity.From.AadObjectId));
                SurveyPendingActivities? userPendingEvents = null;
                await base.GetSurveyManagerService(async surveyManager =>
                {
                    userPendingEvents = await base.GetSurveyPendingActivities(surveyManager, chatUserUpn);
                });

                // Send survey card
                Attachment? surveyCard = null;

                if (userPendingEvents != null)
                {
                    // Are there any pending events to be surveyed?
                    if (userPendingEvents.IsEmpty)
                    {
                        // Send general survey card for no specific event
                        surveyCard = new SurveyNotForSpecificAction().GetCardAttachment();
                    }
                    else
                    {
                        // Send feedback card for specific action
                        var nextCopilotEvent = userPendingEvents.GetNext() ?? throw new ArgumentOutOfRangeException("Unexpected null next event");

                        // Remember selected action
                        await _userState.CreateProperty<BaseCopilotEvent>(CACHE_NAME_NEXT_ACTION).SetAsync(stepContext.Context, nextCopilotEvent);

                        // Register survey request sent so we don't repeatedly ask for the same event
                        await base.GetSurveyManagerService(async surveyManager => await surveyManager.Loader.LogSurveyRequested(nextCopilotEvent.Event));

                        // Figure out what kind of event it is & what card to send
                        if (nextCopilotEvent is CopilotEventMetadataFile)
                        {
                            surveyCard = new CopilotFileActionSurveyCard((CopilotEventMetadataFile)nextCopilotEvent).GetCardAttachment();
                        }
                        else if (nextCopilotEvent is CopilotEventMetadataMeeting)
                        {
                            surveyCard = new CopilotTeamsActionSurveyCard((CopilotEventMetadataMeeting)nextCopilotEvent).GetCardAttachment();
                        }
                        else
                        {
                            surveyCard = new SurveyNotForSpecificAction().GetCardAttachment();
                        }
                    }

                    // Send survey card
                    var opts = new PromptOptions { Prompt = new Activity { Attachments = new List<Attachment>() { surveyCard }, Type = ActivityTypes.Message } };
                    return await stepContext.PromptAsync(nameof(TextPrompt), opts);
                }
                else
                {
                    await SendMsg(stepContext.Context, "Oops, I can't find any survey data for you. Sorry about that.");
                    return await stepContext.EndDialogAsync();
                }
            }
            else
            {
                await SendMsg(stepContext.Context, "No worries. Ping me if you change your mind later.");
                return await stepContext.EndDialogAsync();
            }
        }
        else
        {
            await SendMsg(stepContext.Context, "Oops, I wasn't expecting that response. Let's pretend this didn't happen...");
            return await stepContext.EndDialogAsync();
        }
    }

    /// <summary>
    /// User responds to initial survey card
    /// </summary>
    private async Task<DialogTurnResult> ProcessSurveyResponse(WaterfallStepContext stepContext, CancellationToken cancellationToken)
    {
        var result = JsonSerializer.Deserialize<SurveyInitialResponse>(stepContext.Context.Activity.Text);

        // Get selected survey, if there is one
        var surveyedEvent = await _userState.CreateProperty<BaseCopilotEvent?>(CACHE_NAME_NEXT_ACTION).GetAsync(stepContext.Context, () => null);

        // Process response
        if (result != null)
        {
            var parsedResponse = result.Response;
            if (parsedResponse == null)
            {
                await SendMsg(stepContext.Context, "Oops, I got a survey response back I can't understand. Sorry about that...");
                return await stepContext.EndDialogAsync();
            }

            // Respond if valid
            if (parsedResponse.ScoreGiven > 0)
            {
                // Send reaction card to initial survey
                var responseCard = new BotReactionCard(parsedResponse.Msg, parsedResponse.IsHappy);
                await stepContext.Context!.SendActivityAsync(MessageFactory.Attachment(responseCard.GetCardAttachment()));

                // Save survey data?
                var chatUserUpn = await base.GetChatUserUPN(stepContext);
                if (string.IsNullOrEmpty(chatUserUpn))
                    await SendMsg(stepContext.Context, "Oops, can't report feedback for anonymous users. Your login is needed even if we don't report on it. Thanks for letting me know anyway.");
                else
                {
                    // Update survey data using the survey manager
                    await base.GetSurveyManagerService(async surveyManager =>
                    {
                        if (surveyedEvent != null)
                        {
                            // Log survey result for specific copilot event
                            await surveyManager.Loader.UpdateSurveyResult(surveyedEvent.Event, parsedResponse.ScoreGiven);
                            await _userState.CreateProperty<BaseCopilotEvent?>(CACHE_NAME_NEXT_ACTION).DeleteAsync(stepContext.Context);
                        }
                        else
                        {
                            // Log survey result for general survey
                            int surveyId = await surveyManager.Loader.LogDisconnectedSurveyResult(parsedResponse.ScoreGiven, chatUserUpn);
                        }
                    });
                }
                return await stepContext.EndDialogAsync();
            }
        }

        // If we're here, the survey response was invalid for one reason or another
        await SendMsg(stepContext.Context, "Oops, I got a survey response back I can't understand. Sorry about that...");
        return await stepContext.EndDialogAsync();
    }
}
