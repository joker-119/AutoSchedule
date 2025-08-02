namespace AutoSchedule.Services;

using Android.Content;
using Android.Database;
using Android.Net;
using Android.Provider;

using AutoSchedule.Models;
using AutoSchedule.Resources;

public class CalendarService
{
    private readonly Context ctx = Android.App.Application.Context;

    public long GetCalendarIdForAccount(string email)
    {
        string[] projection =
        {
            CalendarContract.Calendars.InterfaceConsts.Id,
            CalendarContract.Calendars.InterfaceConsts.AccountName,
            CalendarContract.Calendars.InterfaceConsts.CalendarDisplayName,
            CalendarContract.Calendars.InterfaceConsts.CalendarAccessLevel,
        };

        const int aclContributor = 500; // can insert events

        string where =
            $"{CalendarContract.Calendars.InterfaceConsts.AccountName}=? AND " +
            $"{CalendarContract.Calendars.InterfaceConsts.Visible}=1 AND " +
            $"{CalendarContract.Calendars.InterfaceConsts.SyncEvents}=1 AND " +
            $"{CalendarContract.Calendars.InterfaceConsts.CalendarAccessLevel}>={aclContributor}";

        string[] args = [email];

        using ICursor? cursor = ctx.ContentResolver?.Query(
            CalendarContract.Calendars.ContentUri!, projection, where, args, null);

        if (cursor?.MoveToFirst() ?? false)
            return cursor.GetLong(0);

        throw new InvalidOperationException(
            $"No writable calendar found for {email}.");
    }

    public UpsertResult UpsertShift(Shift s, long calendarId)
    {
        // range covering the day (local TZ) for "find existing"
        long dayStart = new DateTimeOffset(s.start.Date).ToUnixTimeMilliseconds();
        long dayEnd = new DateTimeOffset(s.start.Date.AddDays(1)).ToUnixTimeMilliseconds();

        string[] projection =
        {
            CalendarContract.Events.InterfaceConsts.Id,
            CalendarContract.Events.InterfaceConsts.Dtstart,
            CalendarContract.Events.InterfaceConsts.Dtend,
            CalendarContract.Events.InterfaceConsts.Title,
        };

        const string where = $"{CalendarContract.Events.InterfaceConsts.CalendarId}=? AND " +
                             $"{CalendarContract.Events.InterfaceConsts.Dtstart}>=? AND " +
                             $"{CalendarContract.Events.InterfaceConsts.Dtstart}<?";

        string[] args = [calendarId.ToString(), dayStart.ToString(), dayEnd.ToString()];

        using ICursor? cur = ctx.ContentResolver?.Query(
            CalendarContract.Events.ContentUri!, projection, where, args, null);

        // simple heuristic: assume at most one work-shift per day
        if (cur?.MoveToFirst() ?? false)
        {
            long id = cur.GetLong(0);
            long prevStart = cur.GetLong(1);
            long prevEnd = cur.GetLong(2);
            string prevTitle = cur.GetString(3)!;

            bool sameTimes = prevStart == Shift.ToUnixEpochMillis(s.start)
                             && prevEnd == Shift.ToUnixEpochMillis(s.end);
            bool sameTitle = prevTitle.Equals(s.location, StringComparison.OrdinalIgnoreCase);

            if (sameTimes && sameTitle) 
                return UpsertResult.Unchanged;

            // update
            ContentValues values = new ContentValues();
            values.Put(CalendarContract.Events.InterfaceConsts.Dtstart, Shift.ToUnixEpochMillis(s.start));
            values.Put(CalendarContract.Events.InterfaceConsts.Dtend, Shift.ToUnixEpochMillis(s.end));
            values.Put(CalendarContract.Events.InterfaceConsts.Title, s.location);

            Uri updateUri = ContentUris.WithAppendedId(CalendarContract.Events.ContentUri!, id);
            ctx.ContentResolver?.Update(updateUri, values, null, null);

            return UpsertResult.Updated;
        }

        // no existing row → insert
        ContentValues insertValues = new();
        insertValues.Put(CalendarContract.Events.InterfaceConsts.CalendarId, calendarId);
        insertValues.Put(CalendarContract.Events.InterfaceConsts.Dtstart, Shift.ToUnixEpochMillis(s.start));
        insertValues.Put(CalendarContract.Events.InterfaceConsts.Dtend, Shift.ToUnixEpochMillis(s.end));
        insertValues.Put(CalendarContract.Events.InterfaceConsts.EventTimezone, Java.Util.TimeZone.Default?.ID);
        insertValues.Put(CalendarContract.Events.InterfaceConsts.Title, s.location);

        ctx.ContentResolver?.Insert(CalendarContract.Events.ContentUri!, insertValues);
        return UpsertResult.Added;
    }
    
    public IEnumerable<Shift> FindShiftsInRange(long calendarId, DateTime rangeStartLocal, DateTime rangeEndLocal)
    {
        // convert to UTC-milliseconds because CalendarProvider stores times that way
        long fromUtc = new DateTimeOffset(rangeStartLocal).ToUnixTimeMilliseconds();
        long toUtc = new DateTimeOffset(rangeEndLocal).ToUnixTimeMilliseconds();

        string[] projection =
        [
            CalendarContract.Events.InterfaceConsts.Dtstart,
            CalendarContract.Events.InterfaceConsts.Dtend,
            CalendarContract.Events.InterfaceConsts.Title,
            CalendarContract.Events.InterfaceConsts.Deleted
        ];

        const string where = $"{CalendarContract.Events.InterfaceConsts.CalendarId}=? AND " +
                             $"{CalendarContract.Events.InterfaceConsts.Dtstart}>=? AND " +
                             $"{CalendarContract.Events.InterfaceConsts.Dtstart}<?";

        string[] args =
        {
            calendarId.ToString(),
            fromUtc.ToString(),
            toUtc.ToString(),
        };

        using ICursor? cur = ctx.ContentResolver?.Query(
            CalendarContract.Events.ContentUri!, projection, where, args, null);

        while (cur?.MoveToNext() ?? false)
        {
            // skip rows the user deleted
            if (cur.GetInt(3) != 0) 
                continue;
            
            DateTime start = DateTimeOffset.FromUnixTimeMilliseconds(cur.GetLong(0)).LocalDateTime;
            DateTime end = DateTimeOffset.FromUnixTimeMilliseconds(cur.GetLong(1)).LocalDateTime;
            string title = cur.GetString(2) ?? "Work";

            yield return new Shift(title, start.DayOfWeek, start, end);
        }
    }
}