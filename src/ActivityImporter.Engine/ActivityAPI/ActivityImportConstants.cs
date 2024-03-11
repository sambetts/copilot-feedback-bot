namespace ActivityImporter.Engine.ActivityAPI;


public static class ActivityImportConstants
{

    // workload strings - https://msdn.microsoft.com/en-us/office-365/office-365-management-activity-api-schema
    public static string WORKLOAD_SP { get { return "SharePoint"; } }
    public static string WORKLOAD_COPILOT { get { return "Copilot"; } }

    public static string WORKLOAD_OD { get { return "OneDrive"; } }

    public static string WORKLOAD_EXCHANGE { get { return "Exchange"; } }

    public static string WORKLOAD_AZURE_AD { get { return "AzureActiveDirectory"; } }


    public static string COPILOT_CONTEXT_TYPE_TEAMSMEETING { get { return "TeamsMeeting"; } }
    public static string[] ACTIVITY_CONTENT_TYPES { get { return ["Audit.SharePoint", "Audit.General"]; } }


#if DEBUG

    public const string STAGING_TABLE_ACTIVITY_SP = "debug_import_staging_events_sp";

#else
    public const string STAGING_TABLE_ACTIVITY_SP = "##import_staging_event_lookups";

#endif


#if DEBUG

    public const string STAGING_TABLE_COPILOT_SP = "debug_import_staging_copilot_sp";

#else
    public const string STAGING_TABLE_COPILOT_SP = "##debug_import_staging_copilot_sp";

#endif

#if DEBUG

    public const string STAGING_TABLE_COPILOT_TEAMS = "debug_import_staging_copilot_teams";

#else 
    public const string STAGING_TABLE_COPILOT_TEAMS = "##debug_import_staging_copilot_teams";

#endif
}
