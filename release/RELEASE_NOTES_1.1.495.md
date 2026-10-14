# ServoERP 1.1.495 release notes
Release date: 2026-10-06

## User-facing summary
Customers can download installers and updates anonymously from the public ServoERP-Releases repository while application source remains private.

## Features and fixes
- Default update feed now points to harshals499/ServoERP-Releases.
- Builds migrate the old private repository URL to the public feed.
- Local release scripts and GitHub Actions target the public releases repository.
- Includes existing workspace changes from 1.1.494: clean client configuration, first-run setup, generic company branding and protection against replacing an existing licence through Start Trial.

## Affected modules
Desktop update service, configuration, release tooling and CI. Existing client packaging and licensing changes are included in this workspace build.

## Database or migration impact
None introduced by this update-feed change. Existing first-run client setup uses the client configuration.

## Configuration and dependency changes
Update URL: https://github.com/harshals499/ServoERP-Releases.
No packages added. GitHub Actions requires RELEASES_TOKEN with contents read/write access to the public releases repository. No token is included in the desktop app.

## Compatibility or breaking changes
Older installed builds cannot discover this new feed through the private repository. Install this version once, or set both App/GitHubRepositoryUrl and App/VersionCheckUrl to the public repository URL. Future versions use this feed.

## Security impact
Source repository remains private. Installers and compiled update packages are intentionally public. Client configuration, licences, database files and logs are excluded by the packaging preparation script. No new third-party service is used.

## Deployment/update steps
Download ServoERP.Desktop-win-Setup.exe from the public release and install with the application closed. Preserve existing database and configuration. Configure RELEASES_TOKEN in the private source repository before using the modified GitHub Actions workflow. Local publishing uses the authenticated gh account.

## Validation performed
Release build succeeded with zero warnings and errors. SOURCE_CODE/bin/Release/HVAC_Pro_Desktop.exe exists. CiSafe smoke suite passed. Packaging and anonymous download verification are recorded below when complete.

## Known limitations
No interactive customer installation or restart/apply test has been performed. Existing pending workspace changes are included in the desktop build; this is not a source-only feed change. Workflow changes remain local until committed/pushed, and CI publishing requires the repository-scoped token.

## Rollback guidance
Close ServoERP and reinstall the previous approved installer while preserving customer database and configuration. Use the public 1.1.495 feed only with compatible packages; no database rollback is required by this feed change.

## Package validation
Velopack setup, full update package, portable archive and feeds built successfully; artifact check passed. Package filename audit found no excluded mutable configuration, licence, database or log files. Built update URL smoke check passed. Installer is unsigned; Windows may display publisher warnings.

## Publication verification
Published https://github.com/harshals499/ServoERP-Releases/releases/tag/v1.1.495. Unauthenticated GitHub API returned v1.1.495; installer, full update package and feed returned HTTP 200. Anonymous feed parsing found version 1.1.495. Source repository visibility verified PRIVATE and release repository PUBLIC.
