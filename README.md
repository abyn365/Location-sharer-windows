# LocationSharer for Windows

A consent-first Windows tray app written in C# / .NET 8 that:

- shows a tray icon and a visible Start / Stop sharing control
- asks for explicit user consent on first launch
- uses the Windows Location API when available
- falls back to IP geolocation when Windows location is unavailable or denied
- posts updates to your endpoint as JSON
- publishes as a self-contained single-file Windows EXE

The project is intentionally transparent: the app stays visible in the notification area, and sharing can be stopped at any time from the main window or tray menu.

## What gets sent

The app sends JSON like:

```json
{
  "city": "Paris",
  "country": "France",
  "secret": "YOUR_SECRET",
  "source": "windows_location",
  "timestampUtc": "2026-06-24T00:00:00Z",
  "latitude": 48.8566,
  "longitude": 2.3522,
  "accuracyMeters": 50
}
```

Your endpoint may ignore the extra fields if it only needs `city`, `country`, and `secret`.

## Build

Install:

- .NET 8 SDK
- Windows 10/11 SDK support in Visual Studio or the Windows desktop workload
- Optional: WiX Toolset if you want the installer project

Publish a single-file EXE:

```powershell
dotnet publish .\src\LocationSharer\LocationSharer.csproj -c Release -r win-x64
```

The project is configured for self-contained single-file publishing. Exact output size depends on the runtime, trimming, and signing settings.

## Installer

The `installer/` folder contains a WiX-based MSI scaffold and a GitHub Actions workflow for build/sign packaging. The scheduled task is created only during installation when the installer option is selected.

## Endpoint example

The app is compatible with an endpoint shaped like:

```bash
curl -X POST https://your-domain.com/api/location \
  -H "Content-Type: application/json" \
  -d '{"city":"Paris","country":"France","secret":"'"$LOCATION_SECRET"'"}'
```

The sample app sends the same `secret` field and includes optional metadata.
