﻿using Newtonsoft.Json;

namespace ActivityImporter.Engine.Graph.O365UsageReports.Models
{
    public class TeamsUserActivityUserRecord : AbstractUserActivityUserRecordWithUpn
    {
        [JsonProperty("reportRefreshDate")]
        public string ReportRefreshDate { get; set; } = null!;

        [JsonProperty("isLicensed")]
        public bool IsLicensed { get; set; }


        [JsonProperty("isDeleted")]
        public bool IsDeleted { get; set; }

        [JsonProperty("deletedDate")]
        public string DeletedDate { get; set; } = null!;

        [JsonProperty("assignedProducts")]
        public string[] AssignedProducts { get; set; } = new string[] { };

        [JsonProperty("teamChatMessageCount")]
        public int TeamChatMessageCount { get; set; }

        [JsonProperty("privateChatMessageCount")]
        public int PrivateChatMessageCount { get; set; }

        [JsonProperty("callCount")]
        public int CallCount { get; set; }

        [JsonProperty("meetingCount")]
        public int MeetingCount { get; set; }

        [JsonProperty("meetingsOrganizedCount")]
        public int MeetingsOrganizedCount { get; set; }

        [JsonProperty("meetingsAttendedCount")]
        public int MeetingsAttendedCount { get; set; }

        [JsonProperty("adHocMeetingsOrganizedCount")]
        public int AdHocMeetingsOrganizedCount { get; set; }

        [JsonProperty("adHocMeetingsAttendedCount")]
        public int AdHocMeetingsAttendedCount { get; set; }

        [JsonProperty("scheduledOneTimeMeetingsOrganizedCount")]
        public int ScheduledOneTimeMeetingsOrganizedCount { get; set; }

        [JsonProperty("scheduledOneTimeMeetingsAttendedCount")]
        public int ScheduledOneTimeMeetingsAttendedCount { get; set; }

        [JsonProperty("scheduledRecurringMeetingsOrganizedCount")]
        public int ScheduledRecurringMeetingsOrganizedCount { get; set; }

        [JsonProperty("scheduledRecurringMeetingsAttendedCount")]
        public int ScheduledRecurringMeetingsAttendedCount { get; set; }

        [JsonProperty("audioDuration")] public string AudioDuration { get; set; } = null!;

        [JsonProperty("videoDuration")] public string VideoDuration { get; set; } = null!;

        [JsonProperty("screenShareDuration")] public string ScreenShareDuration { get; set; } = null!;

        [JsonProperty("hasOtherAction")]
        public bool HasOtherAction { get; set; }

        [JsonProperty("reportPeriod")] public string ReportPeriod { get; set; } = null!;
    }

}
