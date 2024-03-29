﻿using System.ComponentModel.DataAnnotations.Schema;

namespace Entities.DB.Entities.UsageReports
{
    /// <summary>
    /// What a user has been upto in Teams overrall, on a given date
    /// https://docs.microsoft.com/en-us/graph/api/reportroot-getteamsuseractivityuserdetail?view=graph-rest-beta#response
    /// </summary>
    [Table("teams_user_activity_log")]
    public class GlobalTeamsUserUsageLog : UserRelatedAbstractUsageActivity
    {

        [Column("private_chat_count")]
        public long PrivateChatMessageCount { get; set; }

        [Column("team_chat_count")]
        public long TeamChatMessageCount { get; set; }

        [Column("calls_count")]
        public long CallCount { get; set; }

        [Column("meetings_count")]
        public long MeetingCount { get; set; }

        [Column("adhoc_meetings_attended_count")]
        public long AdHocMeetingsAttendedCount { get; set; }

        [Column("adhoc_meetings_organized_count")]
        public long AdHocMeetingsOrganizedCount { get; set; }

        [Column("meetings_attended_count")]
        public long MeetingsAttendedCount { get; set; }

        [Column("meetings_organized_count")]
        public long MeetingsOrganizedCount { get; set; }

        [Column("scheduled_onetime_meetings_attended_count")]
        public long ScheduledOneTimeMeetingsAttendedCount { get; set; }

        [Column("scheduled_onetime_meetings_organized_count")]
        public long ScheduledOneTimeMeetingsOrganizedCount { get; set; }

        [Column("scheduled_recurring_meetings_attended_count")]
        public long ScheduledRecurringMeetingsAttendedCount { get; set; }

        [Column("scheduled_recurring_meetings_organized_count")]
        public long ScheduledRecurringMeetingsOrganizedCount { get; set; }

        [Column("audio_duration_seconds")]
        public int AudioDurationSeconds { get; set; }

        [Column("video_duration_seconds")]
        public int VideoDurationSeconds { get; set; }

        [Column("screenshare_duration_seconds")]
        public int ScreenShareDurationSeconds { get; set; }
    }

}
