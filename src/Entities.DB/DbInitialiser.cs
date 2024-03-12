﻿using Entities.DB.Entities;
using Entities.DB.Entities.AuditLog;
using Microsoft.Extensions.Logging;

namespace Entities.DB;

public class DbInitialiser
{
    /// <summary>
    /// Ensure created and with base data
    /// </summary>
    public static async Task EnsureInitialised(DataContext context, ILogger logger, string? defaultUserUPN)
    {
        var createdNewDb = await context.Database.EnsureCreatedAsync();

        if (createdNewDb)
        {
            logger.LogInformation("Database created");
            if (defaultUserUPN != null)
            {
                logger.LogInformation("Creating default user");
                var defaultUser = new User
                {
                    UserPrincipalName = defaultUserUPN
                };
                context.Users.Add(defaultUser);

                // Add base lookup data
                logger.LogInformation("Adding base lookup data");

                // Add some base activity types
                var activityTypeDoc = new CopilotActivityType { Name = CopilotActivityType.Document };
                var activityTypeMeeting = new CopilotActivityType { Name = CopilotActivityType.Meeting };
                var activityTypeEmail = new CopilotActivityType { Name = CopilotActivityType.Email };
                var activityTypeChat = new CopilotActivityType { Name = CopilotActivityType.Chat };
                var activityTypeOther = new CopilotActivityType { Name = CopilotActivityType.Other };
                context.CopilotActivityTypes.AddRange([activityTypeChat, activityTypeDoc, activityTypeEmail, activityTypeMeeting, activityTypeOther]);

                // Add some base activities
                context.CopilotActivities.Add(new CopilotActivity { Name = "Draft email", ActivityType = activityTypeEmail });
                context.CopilotActivities.Add(new CopilotActivity { Name = "Summarise email", ActivityType = activityTypeEmail });

                var editDoc = new CopilotActivity { Name = "Edit Document", ActivityType = activityTypeDoc };       // Need this later
                context.CopilotActivities.Add(editDoc);
                context.CopilotActivities.Add(new CopilotActivity { Name = "Summarise Document", ActivityType = activityTypeDoc });

                var getHighlights = new CopilotActivity { Name = "Get highlights", ActivityType = activityTypeMeeting };       // Need this later
                context.CopilotActivities.Add(getHighlights);
                context.CopilotActivities.Add(new CopilotActivity { Name = "Get decisions made", ActivityType = activityTypeMeeting });
                context.CopilotActivities.Add(new CopilotActivity { Name = "Get open items", ActivityType = activityTypeMeeting });

                context.CopilotActivities.Add(new CopilotActivity { Name = "Ask question", ActivityType = activityTypeChat });
                context.CopilotActivities.Add(new CopilotActivity { Name = "Other", ActivityType = activityTypeOther });

#if DEBUG
                DirtyTestDataHackInserts(context, logger, editDoc, getHighlights);
                
#endif
                await context.SaveChangesAsync();
            }
            else
            {
                logger.LogWarning("No default user set, skipping base data");
            }
        }
    }

    private static void DirtyTestDataHackInserts(DataContext context, ILogger logger, CopilotActivity editDocCopilotActivity, CopilotActivity getHighlightsCopilotActivity)
    {
        var rnd = new Random();
        logger.LogInformation("Adding debugging test data");

        var testCompany = new CompanyName { Name = "Contoso" };
        var testJobTitle = new UserJobTitle { Name = "Tester" };
        var testOfficeLocation = new UserOfficeLocation { Name = "Test Office" };


        // Generate some fake departments
        var allDepartments = new List<UserDepartment>();
        List<string> departments =
        [
            "Marketing",
                    "Sales",
                    "Finance",
                    "Human Resources",
                    "Research and Development",
                    "Production",
                    "Quality Assurance",
                    "Customer Service",
                    "Logistics",
                    "Legal",
                ];
        allDepartments.AddRange(departments.Select(d => new UserDepartment { Name = d }));
        context.UserDepartments.AddRange(allDepartments);

        // Add some fake users
        var allUsers = new List<User>();
        for (int i = 0; i < 10; i++)
        {
            var testUser = new User
            {
                UserPrincipalName = $"user-{i}",
                Department = allDepartments[rnd.Next(0, allDepartments.Count - 1)]
            };
            allUsers.Add(testUser);
        }
        context.Users.AddRange(allUsers);

        // Add fake meetings
        var allEvents = new List<CommonAuditEvent>();
        var meetingNames = new List<string>()
                {
                    "Project X: Final Review",
                    "Weekly Team Sync",
                    "Monthly Team Sync",
                    "Customer Feedback Session",
                    "Brainstorming for New Campaign",
                    "Quarterly Performance Review",
                    "Budget Planning Meeting",
                    "Product Launch Strategy",
                    "Training Workshop",
                    "Social Media Analytics",
                    "Happy Hour with Colleagues 🍻"
                };

        var allMeetingEvents = new List<CopilotEventMetadataMeeting>();     // Needed for when we just add teams event feedback, so we don't have exactly 50-50 meetings and files
        
        foreach (var m in meetingNames)
        {
            var testMeetingEvent = new CopilotEventMetadataMeeting
            {
                Event = new CommonAuditEvent
                {
                    User = allUsers[rnd.Next(0, allUsers.Count - 1)],
                    Id = Guid.NewGuid(),
                    TimeStamp = DateTime.Now.AddDays(allMeetingEvents.Count * -1),
                    Operation = new EventOperation { Name = m }
                },
                AppHost = "DevBox",
                OnlineMeeting = new OnlineMeeting { Name = m, MeetingId = "Join Link" }
            };
            context.CopilotEventMetadataMeetings.Add(testMeetingEvent);
            allEvents.Add(testMeetingEvent.Event);
            allMeetingEvents.Add(testMeetingEvent);
        }

        var filenames = new List<string>()
                {
                    "Report.docx",
                    "Invoice.pdf",
                    "Presentation.pptx",
                    "Resume.docx",
                    "Budget.xlsx",
                    "Contract.pdf",
                    "Proposal.docx",
                    "Agenda.docx",
                    "Newsletter.pdf",
                    "Summary.pptx"
                };

        // Fake file events
        var site = new Entities.SP.Site { UrlBase = "https://devbox.sharepoint.com" };
        foreach (var f in filenames)
        {
            var testFileName = new SPEventFileName { Name = f };
            var testFileEvent = new CopilotEventMetadataFile
            {
                Event = new CommonAuditEvent
                {
                    User = allUsers[rnd.Next(0, allUsers.Count - 1)],
                    Id = Guid.NewGuid(),
                    TimeStamp = DateTime.Now.AddDays(allEvents.Count * -2),
                    Operation = new EventOperation { Name = f }
                },
                FileName = testFileName,
                FileExtension = GetSPEventFileExtension(f.Split('.').Last()),
                Url = new Entities.SP.Url { FullUrl = $"https://devbox.sharepoint.com/Docs/{f}" },
                Site = site,
                AppHost = "DevBox",
            };
            context.CopilotEventMetadataFiles.Add(testFileEvent);
            allEvents.Add(testFileEvent.Event);
        }

        // Add some "averagely happy" fake survey responses for meetings and documents
        const int daysback = 60;
        for (int i = 0; i < daysback; i++)
        {
            AddMeetingAndFileEvent(DateTime.Now, i, 2, 4, context, allUsers, rnd, editDocCopilotActivity, getHighlightsCopilotActivity, "Averagely happy", allEvents[rnd.Next(0, allEvents.Count - 1)]);
            AddMeetingAndFileEvent(DateTime.Now, i, 2, 4, context, allUsers, rnd, editDocCopilotActivity, getHighlightsCopilotActivity, "Averagely happy", allEvents[rnd.Next(0, allEvents.Count - 1)]);
        }

        // Add some "very unhappy" fake survey responses from earlier on
        for (int i = 0; i < 5; i++)
        {
            AddMeetingAndFileEvent(DateTime.Now.AddDays(daysback * -1), i, 0, 1, context, allUsers, rnd, editDocCopilotActivity, getHighlightsCopilotActivity, "Not happy", allEvents[rnd.Next(0, allEvents.Count - 1)]);
        }
        // Add some "very happy" fake survey responses for meetings and documents. Use Teams events for the feedback
        for (int i = 0; i < 10; i++)
        {
            AddMeetingAndFileEvent(DateTime.Now, i, 4, 5, context, allUsers, rnd, editDocCopilotActivity, getHighlightsCopilotActivity, "Very happy", 
                allMeetingEvents[rnd.Next(0, allMeetingEvents.Count - 1)].Event);
        }
    }

    static List<SPEventFileExtension> _sPEventFileExtensions = new List<SPEventFileExtension>();
    static SPEventFileExtension GetSPEventFileExtension(string ext)
    { 
        var e = _sPEventFileExtensions.FirstOrDefault(e => e.Name == ext);
        if (e == null)
        {
            e = new SPEventFileExtension { Name = ext };
            _sPEventFileExtensions.Add(e);
        }
        return e;
    }

    private static void AddMeetingAndFileEvent(DateTime from, int i, int ratingFrom, int ratingTo, DataContext context, List<User> allUsers,
        Random rnd, CopilotActivity docActivity, CopilotActivity meetingActivity, string responseCommentPrefix, CommonAuditEvent related)
    {
        var dt = from.AddDays(i * -1);
        var testFileOpResponse = new UserSurveyResponse
        {
            Comment = $"{responseCommentPrefix} - copilot helped with file {i}",
            EstimatedTimeSavedMinutes = rnd.Next(i),
            Rating = rnd.Next(ratingFrom, ratingTo),
            Requested = dt,
            Responded = dt.AddMinutes(i),
            User = allUsers[rnd.Next(0, allUsers.Count)],
        };
        context.SurveyResponses.Add(testFileOpResponse);
        context.SurveyResponseActivityTypes.Add(new UserSurveyResponseActivityType { CopilotActivity = docActivity, UserSurveyResponse = testFileOpResponse });

        var testMeetingResponse = new UserSurveyResponse
        {
            Comment = $"{responseCommentPrefix} - copilot helped with meeting {i}",
            EstimatedTimeSavedMinutes = 60 - rnd.Next(i),
            Rating = rnd.Next(1, 5),
            Requested = dt,
            Responded = dt.AddMinutes(i),
            User = allUsers[rnd.Next(0, allUsers.Count)],
            RelatedEvent = related
        };
        context.SurveyResponses.Add(testMeetingResponse);
        context.SurveyResponseActivityTypes.Add(new UserSurveyResponseActivityType { CopilotActivity = meetingActivity, UserSurveyResponse = testMeetingResponse });
    }
}