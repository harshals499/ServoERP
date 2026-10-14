# ServoERP 1.1.488

Release date: 2026-10-05

## Summary, features and fixes
Restored all twenty original application screenshots across the website at the owner's explicit request. Updated gallery wording, alternate text and cache keys. The creator section and original portrait are preserved.

## Affected modules
Marketing homepage, module showcase and screenshot gallery. VERSION and desktop assembly version tracking updated.

## Database and migrations
None.

## Configuration and dependencies
None. Existing installer download metadata is preserved.

## Compatibility and breaking changes
None. Image URLs retain their paths with new cache keys.

## Security impact
The owner explicitly confirmed publication of the original screenshots with company names, contacts and financial details visible. No blur or AI editing is applied. Configuration, dependencies and unrelated private captures remain excluded from the static deployment.

## Deployment and update steps
The allowlisted release/website-1.1.488 directory was deployed to Cloudflare Pages project royal-moon-64ef, production branch ServoERP. Deployment: https://ae2fdad7.royal-moon-64ef.pages.dev. No desktop installer update is required.

## Validation
All twenty staged screenshot hashes match the original captures. Release MSBuild succeeded and SOURCE_CODE/bin/Release/HVAC_Pro_Desktop.exe exists. Four existing website tests passed. Public gallery browser verification is recorded in DEPLOYMENT_1.1.488.md.

## Known limitations
Original captures include actual business information and application empty/configuration states. Public desktop download metadata remains at existing installer version 1.1.486.

## Rollback
Restore Cloudflare Pages deployment 7b9c90db (https://7b9c90db.royal-moon-64ef.pages.dev) to return to redacted screenshots while preserving the creator section. No database rollback is required.
