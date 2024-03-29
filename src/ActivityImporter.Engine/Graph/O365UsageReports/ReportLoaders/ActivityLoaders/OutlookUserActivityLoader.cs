﻿using ActivityImporter.Engine.Graph.O365UsageReports.Models;
using Entities.DB;
using Entities.DB.Entities.UsageReports;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ActivityImporter.Engine.Graph.O365UsageReports.ReportLoaders.ActivityLoaders
{
    public class OutlookUserActivityLoader : AbstractActivityLoader<OutlookUsageActivityLog, OutlookUserActivityUserRecord>
    {
        public OutlookUserActivityLoader(ManualGraphCallClient client, ILogger logger)
            : base(client, logger)
        {
        }

        public override string ReportGraphURL => "https://graph.microsoft.com/beta/reports/getEmailActivityUserDetail";

        protected override void PopulateReportSpecificMetadata(OutlookUsageActivityLog todaysLog, OutlookUserActivityUserRecord userActivityReportPage)
        {
            todaysLog.MeetingCreated = userActivityReportPage.MeetingCreated;
            todaysLog.ReadCount = userActivityReportPage.ReadCount;
            todaysLog.ReceiveCount = userActivityReportPage.ReceiveCount;
            todaysLog.SendCount = userActivityReportPage.SendCount;
            todaysLog.MeetingInteracted = userActivityReportPage.MeetingInteracted;

        }

        protected override long CountActivity(OutlookUserActivityUserRecord activityPage)
        {
            if (activityPage is null)
            {
                throw new ArgumentNullException(nameof(activityPage));
            }

            long count = 0;
            count += activityPage.ReadCount;
            count += activityPage.ReceiveCount;
            count += activityPage.SendCount;
            count += activityPage.MeetingCreated;
            count += activityPage.MeetingInteracted;

            return count;
        }
        public override DbSet<OutlookUsageActivityLog> GetTable(DataContext context) => context.OutlookUsageActivityLogs;

    }
}
