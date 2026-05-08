# ServoERP License Issuer

Private internal tool for issuing signed offline ServoERP license files.

## Do not ship

Never copy these files to a customer PC, installer, ISO, VM image, Git repo, or support bundle:

- `C:\ServoERP-LicenseAuthority\private-key.xml`
- `C:\ServoERP-LicenseAuthority\Tools\ServoERP.LicenseIssuer.exe`
- `C:\ServoERP-LicenseAuthority\issued-licenses.json`

## Customer flow

1. Customer opens ServoERP.
2. Customer goes to `Settings > License Management`.
3. Customer clicks `Copy Device ID`.
4. Customer sends the device fingerprint to ServoERP support.
5. Use `ServoERP License Issuer` on the desktop to issue a `.servoerp-license` file.
6. Send only the `.servoerp-license` file to the customer.
7. Customer imports it through `Activate / Renew > Import Offline File`.

## Authority files

- Private key: `C:\ServoERP-LicenseAuthority\private-key.xml`
- Public key: `C:\ServoERP-LicenseAuthority\public-key.xml`
- Issued licenses: `C:\ServoERP-LicenseAuthority\licenses`
- Issue history: `C:\ServoERP-LicenseAuthority\issued-licenses.json`

The ServoERP app verifies license files using the public key compiled into `OfflineLicenseValidator`.
