using Ical.Net;
using Ical.Net.CalendarComponents;
using Ical.Net.DataTypes;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Cally;

public record Feed(Guid Id, string Provider, string Name, string Url);
public record Meeting(Guid FeedId, string Account, DateTime Start, DateTime End, bool AllDay, string Title, string Place, string? Join);
public sealed class Preferences
{
    public List<Feed> Feeds { get; set; } = [];
    public bool Private { get; set; }
    public bool Expanded { get; set; } = true;
    public double X { get; set; } = 64;
    public double Y { get; set; } = 60;
    public double Width { get; set; } = 430;
    public Guid? Filter { get; set; }
    static readonly string FilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Cally", "settings.bin");
    public static Preferences Load() => File.Exists(FilePath)
        ? JsonSerializer.Deserialize<Preferences>(ProtectedData.Unprotect(File.ReadAllBytes(FilePath), null, DataProtectionScope.CurrentUser)) ?? new()
        : new();
    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        var bytes = ProtectedData.Protect(JsonSerializer.SerializeToUtf8Bytes(this), null, DataProtectionScope.CurrentUser);
        File.WriteAllBytes(FilePath + ".new", bytes);
        File.Move(FilePath + ".new", FilePath, true);
    }
}
public static class Calendars
{
    static readonly HttpClient Client = new() { Timeout = TimeSpan.FromSeconds(25), MaxResponseContentBufferSize = 16 * 1024 * 1024 };
    static readonly Regex Conference = new(@"https://(?:meet\.google\.com|teams\.(?:microsoft\.com|live\.com)|(?:[\w-]+\.)?zoom\.us)/[^\s<>""']+", RegexOptions.IgnoreCase);
    public static async Task<List<Meeting>> Read(Feed feed)
    {
        var content = await Client.GetStringAsync(feed.Url).ConfigureAwait(false);
        if (!content.TrimStart('\uFEFF', ' ', '\t', '\r', '\n').StartsWith("BEGIN:VCALENDAR", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Expected an iCalendar feed.");
        return await Task.Run(() => Parse(content, feed, DateTime.Today)).ConfigureAwait(false);
    }
    public static string Failure(Feed feed, Exception error) => error switch
    {
        HttpRequestException { StatusCode: System.Net.HttpStatusCode.NotFound } when feed.Provider == "Google" =>
            "Link not found (404). Use the secret address in iCal format.",
        HttpRequestException { StatusCode: System.Net.HttpStatusCode.ExpectationFailed } =>
            "Request rejected (417). Test the published ICS link in a private browser window.",
        HttpRequestException { StatusCode: System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden } =>
            "Access denied. Check the calendar's sharing settings.",
        HttpRequestException { StatusCode: {} code } => $"Feed request failed (HTTP {(int)code}).",
        HttpRequestException => "Could not reach the calendar server. Check the connection.",
        OperationCanceledException => "The calendar server took too long to respond.",
        InvalidDataException => "The link did not return an iCalendar feed. Check the subscription link.",
        _ => "Cally could not read the calendar data."
    };
    public static List<Meeting> Parse(string content, Feed feed, DateTime day)
    {
        var calendar = Calendar.Load(content) ?? throw new InvalidDataException();
        var result = new List<Meeting>();
        foreach (var occurrence in calendar.GetOccurrences<CalendarEvent>(new CalDateTime(day.AddDays(-1).ToUniversalTime())).TakeWhileBefore(new CalDateTime(day.AddDays(1).ToUniversalTime())))
        {
            var ev = (CalendarEvent)occurrence.Source;
            if (string.Equals(ev.Status, "CANCELLED", StringComparison.OrdinalIgnoreCase)) continue;
            var period = occurrence.Period;
            DateTime Local(CalDateTime time) => time.IsFloating || !time.HasTime ? time.Value : time.AsUtc.ToLocalTime();
            var start = Local(period.StartTime);
            var end = period.EffectiveEndTime is {} finish ? Local(finish) : start;
            if (end <= day && start < day || start >= day.AddDays(1)) continue;
            var hidden = ev.Class?.ToUpperInvariant() is "PRIVATE" or "CONFIDENTIAL";
            var contentWithLink = $"{ev.Properties.Get<string>("X-GOOGLE-CONFERENCE")} {ev.Url} {ev.Location} {ev.Description}";
            var match = Conference.Match(System.Net.WebUtility.HtmlDecode(contentWithLink));
            var join = match.Success && !hidden ? match.Value.TrimEnd('.', ',', ')') : null;
            var place = hidden ? "" : ev.Location ?? "";
            if (join != null && (place.Length == 0 || place.StartsWith("https://", StringComparison.OrdinalIgnoreCase)))
                place = new Uri(join).Host.Contains("teams.") ? "Teams" : new Uri(join).Host.Contains("google.") ? "Google Meet" : "Zoom";
            result.Add(new(feed.Id, feed.Name, start, end, !period.StartTime.HasTime, hidden ? "Busy" : ev.Summary ?? "Untitled event", place, join));
        }
        return result;
    }
}
