namespace Web.Bots.Cards;

public class SurveyFollowUpQuestionsCard : BaseAdaptiveCard
{
    public SurveyFollowUpQuestionsCard()
    {
    }

    public override string GetCardContent()
    {
        var json = ReadResource(BotConstants.SurveyFollowUpQuestions);

        return json;
    }
}
