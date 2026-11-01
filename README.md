# Cally

![Cally desktop widget showing a 12-minute countdown to a Teams meeting](Assets/cally-banner.png)

A Windows desktop calendar widget with a countdown and collapsible daily agenda. Requires Windows and the .NET 9 Desktop Runtime. Build with the .NET 9 SDK using `dotnet build -c Release`. Launch with `dotnet run` or open the built Cally.exe.

Choose **Connect a calendar**, select the provider, name the calendar and paste its subscription link. Add more calendars through **All accounts**. Google accepts the secret iCal address. Outlook accepts a published ICS link. iCloud accepts a public calendar link. Outlook and iCloud published calendars can be read by anyone who has their link. Cally does not publish calendars or change provider sharing settings.

Cally refreshes feeds every two minutes. Use the refresh button beside the account selector to fetch all connected calendars immediately, even when Today is collapsed. Feed URLs and preferences are protected with Windows user-scoped encryption. Calendar contents stay in memory. Private and confidential events appear as Busy. The account menu and system tray offer a privacy toggle.

Drag the header to move the widget. Use the account menu to adjust width, refresh, or disconnect the selected calendar. Select a meeting for details. The desktop layer relies on Windows Explorer's icon view. If unavailable, Cally reports that state and opens as a normal window.

Right-click the widget, click the account selector, or left- or right-click the tray icon to open Cally’s menu. **Launch at Windows sign-in** registers the current executable for this Windows user. Extract portable builds to a permanent folder before enabling this option.

### Windows distribution

A push to `master` runs **Package Cally for Windows** in GitHub Actions. Download the **Cally-win-x64** artifact from the workflow run, extract the portable ZIP and run Cally.exe with its accompanying files. The package includes the .NET desktop runtime.
