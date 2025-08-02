namespace AutoSchedule.ViewModels;

using System.Collections.ObjectModel;
using System.Text.RegularExpressions;

using AutoSchedule.Models;
using AutoSchedule.Resources;
using AutoSchedule.Services;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

public partial class ImportViewModel : ObservableObject
{
    private static readonly Dictionary<string, DayOfWeek> DowMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["MON"] = DayOfWeek.Monday,
        ["TUE"] = DayOfWeek.Tuesday,
        ["WED"] = DayOfWeek.Wednesday,
        ["THU"] = DayOfWeek.Thursday,
        ["FRI"] = DayOfWeek.Friday,
        ["SAT"] = DayOfWeek.Saturday,
        ["SUN"] = DayOfWeek.Sunday,
    };
    
    private static readonly Regex DateNumRx = DateRegex();

    private static readonly Regex DayOnlyRx =
        DowRegex();

    private static readonly Regex RangeRx =
        TimeRegex();
    
    private static readonly Regex LocationRx =
        LocRegex();

    private readonly OcrService ocr;
    private readonly CalendarService cal;
    private readonly AlarmService alarmSvc;
    
    public ImportViewModel(OcrService ocr, CalendarService cal, AlarmService alarm)
    {
        this.ocr = ocr;
        this.cal = cal;
        
        alarmSvc = alarm;
        DraftShifts.CollectionChanged += (_, _) => OnPropertyChanged(nameof(ShiftCount));
    }
    
    public ObservableCollection<Shift> DraftShifts { get; } = [];
    
    public int ShiftCount => DraftShifts.Count;
    
    private static Page RootPage => Application.Current!.MainPage!;

    private static IEnumerable<Shift> Parse(string raw)
    {
        List<Header> headers = [];
        int? pendingDayNum = null;

        // 1️⃣ pass: build header list (date-num + weekday)
        foreach (string line in raw.Split('\n').Select(s => s.Trim()))
        {
            Match dNum = DateNumRx.Match(line);
            if (dNum.Success)
            {
                pendingDayNum = int.Parse(dNum.Groups[1].Value);
                continue;
            }

            if (pendingDayNum is null || !DayOnlyRx.IsMatch(line) || !DowMap.TryGetValue(line.ToUpperInvariant(), out DayOfWeek dow))
                continue;
            
            headers.Add(new() { DayNum = pendingDayNum.Value, Dow = dow });
            pendingDayNum = null;
        }

        if (headers.Count == 0)
            yield break; // nothing recognised

        // 2️⃣ second pass: attach time ranges *in order* to headers
        int headerIdx = 0;
        foreach (string line in raw.Split('\n').Select(s => s.Trim()))
        {
            if (headerIdx >= headers.Count)
                break;          // all headers done

            // ── Location line?  (always follows the range line for the SAME header)
            Match loc = LocationRx.Match(line);
            if (loc.Success)
            {
                headers[headerIdx].Location = loc.Groups[1].Value.Trim();
                headerIdx++;                               // header is now complete
                continue;                                  // next input line
            }

            // ── Time-range line?
            Match rng = RangeRx.Match(line);
            if (!rng.Success)
                continue;
            
            headers[headerIdx].Start = rng.Groups[1].Value;
            headers[headerIdx].End = rng.Groups[2].Value;
        }

        // 3️⃣ build full DateTime values, handling month rollover + overnight shifts
        Header first = headers[0];
        DateTime today = DateTime.Today;
        DateTime startMonth = today.AddMonths(-1);                 // previous month
        DateTime firstDate = default;
        bool found = false;

        // prev-month, this-month, next-month
        for (int i = 0; i < 3 && !found; i++)
        {
            DateTime candidate = new(startMonth.Year, startMonth.Month, first.DayNum);
            if (candidate.DayOfWeek == first.Dow)
            {
                firstDate = candidate;
                found = true;
            }
            
            startMonth = startMonth.AddMonths(1);
        }

        if (!found)
            firstDate = new(today.Year, today.Month, first.DayNum); // fall-back

        DateTime cursor = new(firstDate.Year, firstDate.Month, 1); // month we’ll advance
        int lastDayNum = 0;

        foreach (Header h in headers)
        {
            if (h.Start is null || h.End is null) 
                continue; // header had no time

            // month rollover
            if (h.DayNum < lastDayNum) 
                cursor = cursor.AddMonths(1);
            
            lastDayNum = h.DayNum;

            DateTime baseDate = new(cursor.Year, cursor.Month, h.DayNum);
            if (!TryMakeDateTime(baseDate, h.Start, out DateTime start) ||
                !TryMakeDateTime(baseDate, h.End, out DateTime end))
                continue;

            if (end <= start) 
                end = end.AddDays(1); // overnight

            yield return new(h.Location, h.Dow, start, end);
        }
    }

// helper: base-date + “hh:mm AM/PM” ➜ DateTime
    private static bool TryMakeDateTime(DateTime baseDate, string timeStr, out DateTime result)
    {
        if (!DateTime.TryParse(timeStr, out DateTime t))
        {
            result = default;
            return false;
        }

        result = new(baseDate.Year, baseDate.Month, baseDate.Day, t.Hour, t.Minute, 0);
        return true;
    }
    
    private static async Task<bool> EnsureCalendarPermissionAsync()
    {
        PermissionStatus writeStatus = await Permissions.CheckStatusAsync<Permissions.CalendarWrite>();
        if (writeStatus != PermissionStatus.Granted)
            writeStatus = await Permissions.RequestAsync<Permissions.CalendarWrite>();
        PermissionStatus readStatus = await Permissions.CheckStatusAsync<Permissions.CalendarRead>();
        if (readStatus != PermissionStatus.Granted)
            readStatus = await Permissions.RequestAsync<Permissions.CalendarRead>();

        return writeStatus == PermissionStatus.Granted && readStatus == PermissionStatus.Granted;
    }

    [GeneratedRegex(@"^(Mon|Tue|Wed|Thu|Fri|Sat|Sun)$", RegexOptions.IgnoreCase, "en-US")]
    private static partial Regex DowRegex();
    
    [GeneratedRegex(@"(\d{1,2}:\d{2}\s?[AP]M)\s*-\s*(\d{1,2}:\d{2}\s?[AP]M)", RegexOptions.IgnoreCase, "en-US")]
    private static partial Regex TimeRegex();
    
    [GeneratedRegex(@"^Location:\s*(.+)$", RegexOptions.IgnoreCase, "en-US")]
    private static partial Regex LocRegex();
    
    [GeneratedRegex(@"^\s*(\d{1,2})\s*$")]
    private static partial Regex DateRegex();

    [RelayCommand]
    private async Task ImportAsync()
    {
        try
        {
            DraftShifts.Clear();
            FileResult? file = await FilePicker.PickAsync(new()
            {
                PickerTitle = "Select schedule screenshot",
                FileTypes = FilePickerFileType.Images,
            });

            if (file is null)
                return;

            await using Stream stream = await file.OpenReadAsync();
            string raw = await ocr.ExtractTextAsync(stream);

            foreach (Shift shift in Parse(raw))
                DraftShifts.Add(shift);

            if (DraftShifts.Count == 0)
                await RootPage.DisplayAlert("Nothing recognized", "Couldn't detect any day/time lines in that screenshot.", "OK");
        }
        catch (Exception e)
        {
            System.Diagnostics.Debug.WriteLine(e.Message);
            await RootPage.DisplayAlert("Import Failed", e.Message, "OK");
        }
    }

    [RelayCommand]
    private async Task CommitAsync()
    {
        if (DraftShifts.Count == 0)
            return;

        if (!await EnsureCalendarPermissionAsync())
        {
            await RootPage.DisplayAlert(
                "Permission denied",
                "Cannot add events without calendar permission.",
                "OK");
            return;
        }

        long id = cal.GetCalendarIdForAccount("amathor929@gmail.com");

        int added = 0, updated = 0, unchanged = 0;

        foreach (Shift s in DraftShifts)
        {
            switch (cal.UpsertShift(s, id))
            {
                case UpsertResult.Added: added++; break;
                case UpsertResult.Updated: updated++; break;
                case UpsertResult.Unchanged: unchanged++; break;
            }
        }

        // TODO: Fix this so that it works properly :(
        // alarmSvc.RefreshAlarmForToday(id);
        await RootPage.DisplayAlert(
            "Shifts Sync",
            $"{added} added\n{updated} updated\n{unchanged} unchanged",
            "OK");
    }
}