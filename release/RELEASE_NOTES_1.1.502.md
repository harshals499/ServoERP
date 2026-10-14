# ServoERP 1.1.502
Release date: 11/10/2026

## User-facing summary
Published desktop updates download automatically while ServoERP is running and online, without pressing Install.

## Features and fixes
Check immediately on startup and every five minutes while logged in. Retry failed checks or downloads at that interval. Banner dismissal no longer disables automatic downloads. Avoid repeatedly downloading the same ready version. Use asynchronous checks and downloads. Installation continues when the app closes.

## Affected modules
Main application shell and desktop update service.

## Database or migration impact
None.

## Configuration and dependency changes
Automatic update policy v2 enables checks, background download and installation on exit once for existing clients; later user opt-outs are respected. Persisted hourly check timestamps no longer delay background discovery. No packages added.

## Compatibility or breaking changes
None. Existing public updater methods and GitHub release channel retained.

## Security impact
None. Existing package verification retained. No new outbound services.

## Deployment/update steps
Publish the complete version-matched Velopack feed and packages. Existing clients whose updater requires a click must install this version once. Future published releases download automatically while the corrected client is running, logged in and online.

## Validation performed
Release build succeeded; HVAC_Pro_Desktop.exe exists. Reflection regression checks passed immediate startup, interval throttling, periodic retry, one-time policy upgrade and embedded version-matched notes. CI smoke tests passed, including automatic-update policy and embedded release notes checks. Release notes and Velopack package/feed guards passed with nonblank version-matched notes. Customer-PC update installation and interactive UI smoke testing have not been performed.

## Known limitations
Polling is not instantaneous; discovery normally takes up to five minutes plus feed propagation and download time. Closed, logged-out or offline clients wait until running and online. Customer-PC rollout and live cross-version installation have not been performed.

## Rollback guidance
Reinstall the previous supported desktop installer. No business-data migration to reverse. Automatic update preferences can be disabled in Settings.
