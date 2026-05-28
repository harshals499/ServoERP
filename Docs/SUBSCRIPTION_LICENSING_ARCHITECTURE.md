# ServoERP Subscription Licensing Architecture

ServoERP uses one installer for every client. Access is controlled by the ServoERP license server and a locally cached signed/protected subscription snapshot.

## Client Flow

1. First launch validates the local license cache.
2. If activation is required, the user enters:
   - Company Code
   - License Key
3. ServoERP sends the device fingerprint and app version to the license server.
4. The server validates subscription status and active device count.
5. If allowed, the server registers the device and returns the current entitlement snapshot.
6. ServoERP stores the snapshot in a machine-protected cache and uses it for startup/module checks.

## Subscription Rules

| Plan | Max Devices | Offline Grace |
| --- | ---: | ---: |
| Starter AMC - ₹10,000/year | 3 | 7 days |
| Growth AMC - ₹18,000/year | 7 | 7 days |
| Business AMC - ₹30,000/year | 15 | 7 days |

The server is the authority for:

- CompanyId
- CompanyCode
- PlanName
- SubscriptionStartDateUtc
- SubscriptionEndDateUtc
- SubscriptionStatus
- MaxDevicesAllowed
- MaxUsers
- ModulesEnabled
- OfflineGraceDays
- ActivatedDeviceCount

## Client Configuration

`HVACPro.config`:

```xml
<Licensing>
  <ActivationUrl>https://servoerp.in/api/license/activate</ActivationUrl>
  <ValidationUrl>https://servoerp.in/api/license/validate</ValidationUrl>
</Licensing>
```

## Activation Request

`POST /api/license/activate`

```json
{
  "companyCode": "MSE-2026",
  "licenseKey": "XXXX-XXXX-XXXX",
  "licenseKeyHash": "sha256/base64",
  "machineFingerprintHash": "device hash",
  "deviceName": "CLIENT-PC-01",
  "appVersion": "1.0.24.0",
  "clientUtc": "2026-05-11T10:00:00Z"
}
```

The server must reject activation when:

- company does not exist
- license key hash does not match
- subscription is expired or suspended
- active device count is already at MaxDevicesAllowed
- device is blocked/deactivated

## Validation Request

`POST /api/license/validate`

```json
{
  "companyId": "company-guid",
  "companyCode": "MSE-2026",
  "licenseKeyHash": "sha256/base64",
  "activatedDeviceId": "device-guid-or-fingerprint",
  "machineFingerprintHash": "device hash",
  "deviceName": "CLIENT-PC-01",
  "appVersion": "1.0.24.0",
  "clientUtc": "2026-05-11T10:00:00Z"
}
```

## Success Response

```json
{
  "success": true,
  "message": "Subscription active.",
  "companyId": "company-guid",
  "companyCode": "MSE-2026",
  "companyName": "MSE Cooling Solutions",
  "licenseKeyHash": "sha256/base64",
  "planType": "Pro",
  "planName": "Growth AMC",
  "subscriptionStatus": "Active",
  "subscriptionStartDateUtc": "2026-05-01T00:00:00Z",
  "subscriptionEndDateUtc": "2027-05-01T00:00:00Z",
  "serverTimeUtc": "2026-05-11T10:00:00Z",
  "maxCompanies": 1,
  "maxDevicesAllowed": 7,
  "maxUsers": 7,
  "offlineGraceDays": 7,
  "machineFingerprintHash": "device hash",
  "activatedDeviceId": "device-guid-or-fingerprint",
  "activatedDeviceCount": 4,
  "supportLevel": "Priority",
  "billingCycle": "Annual",
  "currency": "INR",
  "priceAmount": 18000,
  "renewalPriceAmount": 18000,
  "isLaunchOffer": false,
  "modulesEnabled": ["Dashboard", "Clients", "Invoices", "Purchases", "Inventory", "ServiceDesk", "WorkOrders", "Reports", "Settings"]
}
```

## Client Enforcement

ServoERP freezes access when:

- no license cache exists
- local cache was copied to another machine
- device count exceeds server allowance
- server returns Expired or Suspended
- subscription expiry plus grace has passed
- an online subscription has not validated successfully within OfflineGraceDays
- system clock rollback is detected

Settings, Dashboard, and Reports remain minimally accessible for recovery and support where existing permissions allow.

## Security Rules

- The client sends the plain license key only during activation over HTTPS.
- The local app stores the license key hash, not the plain license key.
- The local cache is protected with Windows DPAPI machine scope.
- Device fingerprints are checked on every startup.
- Activation, validation, failures, and tamper events are logged.
- Server-side device deactivation must mark the device inactive; the next validation should return failure or a reduced entitlement snapshot.
