using System.ComponentModel;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Text.Json;

namespace Cally;

internal static class ReleaseUpdates
{
    internal const string Page = "https://github.com/NomanNasirMinhas/Cally/releases/latest";
    const string Endpoint = "https://api.github.com/repos/NomanNasirMinhas/Cally/releases/latest";
    static readonly HttpClient Client = new() { Timeout = TimeSpan.FromSeconds(12) };
    internal static readonly Version Installed = Version.Parse(typeof(App).Assembly.GetName().Version!.ToString(3));

    internal static async Task<Version?> Fetch()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, Endpoint);
        request.Headers.UserAgent.ParseAdd($"Cally/{Installed}");
        request.Headers.Accept.ParseAdd("application/vnd.github+json");
        using var response = await Client.SendAsync(request).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync().ConfigureAwait(false));
        if (!json.RootElement.TryGetProperty("tag_name", out var tag) ||
            tag.ValueKind != JsonValueKind.String ||
            !Version.TryParse(tag.GetString()?.TrimStart('v','V'), out var version) ||
            version.Build < 0 || version.Revision >= 0)
            throw new FormatException("Unrecognized release version.");
        return version;
    }
}

internal static class WebPage
{
    internal static void Open(string address)
    {
        try { Process.Start(new ProcessStartInfo(address) { UseShellExecute=true }); }
        catch (Exception error) when (error is Win32Exception or InvalidOperationException)
        { CallyDialog.Prompt("Browser unavailable", $"Open this address in your browser:\n{address}"); }
    }
}
