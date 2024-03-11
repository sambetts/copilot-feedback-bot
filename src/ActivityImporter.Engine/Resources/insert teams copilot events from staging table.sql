INSERT INTO online_meetings(created, meeting_id, name)
	SELECT distinct meeting_created_utc, imports.meeting_id, imports.meeting_name 
	FROM debug_import_staging_copilot_teams imports
	left join 
		online_meetings on online_meetings.meeting_id = imports.meeting_id
	where 
		not exists(select top 1 created, meeting_id, name from online_meetings 
			where created = imports.meeting_created_utc 
				and created = imports.meeting_created_utc 
				and imports.meeting_name = [name]
		)

insert into event_meta_copilot_meetings (event_id, meeting_id, app_host)
	SELECT imports.event_id
		  ,online_meetings.id as meetingId
		  ,app_host
	  FROM debug_import_staging_copilot_teams imports
	  inner join online_meetings on online_meetings.meeting_id = imports.meeting_id
		and online_meetings.name = imports.meeting_name
		and online_meetings.created = imports.meeting_created_utc
		 
