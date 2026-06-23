# Save Button Audit

Date: 2026-06-23

Static audit from WinForms source plus `/savebuttontest` runtime verification where covered. Status is based on source wiring, Release build success, and the save-button smoke report at `TEST_RESULTS/save-button-smoke-20260623-152155.txt`.

| Form/Page | Button name/text | Click handler | Data saved | Status | Error/Notes |
|---|---|---|---|---|---|
| `AddAMCEquipmentForm` | `_save` / save equipment | `SaveEquipment()` | AMC equipment details | Working | Wired inline; validates equipment name; catches exceptions. |
| `AddAMCForm` | `_btnSave` / `Save AMC` or `Save Changes` | `SaveAMC()` | AMC contract | Working | Save button disabled/restored; validation and duplicate handling present. |
| `BackupSettingsForm` | `_btnSave` / `Save Settings` | `SaveClicked` | Backup settings | Working | Wired in Designer; catches exceptions. |
| `ChangePasswordForm` | `_btnSave` / save password | `SavePassword()` | User password | Working | Wired inline; button state restored. |
| `ClientDetailPage` | `save` / save | `SaveClient()` | Client profile | Working | Header and footer save buttons call same method. |
| `ClientDetailPage` | `footerSave` / save | `SaveClient()` | Client profile | Working | Duplicate entry point, same save path. |
| `ClientDetailPage` | `saveTeam` / save team | `SaveTeam()` | Client team/contact rows | Needs review | Wired; runtime behavior not smoke-tested in this pass. |
| `ClientManagementForm` | local `save` / `Save` | async editor lambda | Client record | Working | Inline save logic; duplication candidate. |
| `ClientManagementForm` | local `save` / dialog OK | inline lambda | Dialog selection/input | Needs review | Appears to close dialog with OK; not necessarily persistent save. |
| `ConnectionSetupForm` | `btnSave` / `Save` | `SaveConnection()` | SQL connection settings | Working | Wired inline; affects configuration, not schema. |
| `ContractManagementForm` | `save` | `BtnSave_Click` | Contract/AMC record | Working | Multiple save buttons point to same handler. |
| `ContractManagementForm` | `saveContract` | `BtnSave_Click` | Contract/AMC record | Working | Duplicate entry point, same handler. |
| `CreateAccountForm` | `create` / `Create Account` | `CreateAccount()` | User account | Working | Wired inline. |
| `DevTeamDashboardForm` | `_btnNewTask` | `SubmitNewTask()` | Dev/team task | Needs review | Internal/dev module; not smoke-tested. |
| `EmployeeForm` | save/create/update buttons | form-local handlers | Employee/payroll-related data | Working | `/savebuttontest` passed create, update, and salary profile persistence. |
| `InventoryForm` | save/add/update buttons | form-local handlers | Stock/items/inventory data | Working | `/savebuttontest` passed create and update persistence. |
| `InvoiceForm` | save/create/update buttons | form-local handlers | Invoice and line items | Working | `/savebuttontest` passed create and update persistence. |
| `JobManagementForm` | save/create/update buttons | form-local handlers | Job/service data | Working | `/savebuttontest` passed create and update persistence. |
| `MasterDataForm` | save/add/import controls | form-local handlers | Master data/import mappings | Needs review | Large form; import workflows should be tested separately. |
| `PaymentForm` | save/add/update buttons | form-local handlers | Payment records | Working | `/savebuttontest` passed payment record and invoice status refresh. |
| `PurchaseForm` | save/create/update/add buttons | form-local handlers | Purchase orders/supplier data | Working | `/savebuttontest` passed create and update persistence. |
| `SettingsForm` | save/apply buttons | form-local handlers | App/company/integration settings | Needs review | Large form; avoid touching license/machine ID behavior. |
| `TallyIntegrationForm` | save/apply connection controls | form-local handlers | Tally settings | Needs review | Includes default localhost URL and SQL setting upsert; no schema change made. |
| `TenderBidForm` | `_btnSaveQuote` / save quotation | `SaveAsync()` | Quotation/tender bid | Working | Save button wired in two places; ensure duplicate handler is intentional for active view. |
| `VendorForm` | `_btnSave` / `Save` | `SaveVendorAsync(true)` | Supplier/vendor record | Working | Wired inline; validation present; duplication candidate. |
| `WhatsAppHubForm` | send/mark buttons | `SendCurrentMessage`, `MarkPendingMessageSent` | WhatsApp activity state/message status | Needs review | Does not auto-send via API; user-facing behavior guarded. |

## Findings

- Save button naming is inconsistent: `_btnSave`, `btnSave`, local `save`, `saveContract`, `_btnSaveQuote`.
- Click handlers mix named methods, inline lambdas, async lambdas, and Designer wiring.
- The primary save flows covered by `/savebuttontest` passed.
- Several secondary add/submit buttons still require manual UI smoke testing because static scan cannot prove every inline dialog/action path.
- No not-wired save button was proven by static scan.

## Recommended Standard

- Field name: `_btnSave` for primary persistent save button.
- Handler name: `Save<Entity>Async` for async save methods.
- Click wiring: `_btnSave.Click += async (s, e) => await Save<Entity>Async();`
- Button state: disable in a shared save runner and restore in `finally`.
- Validation: FluentValidation where model-backed; professional ServoERP wording for user-facing messages.
