# Global header 1.1.513 — design QA

Final result: passed for the requested global-header scope.

Selected reference: codex-clipboard-bbea4c22-0e74-4aaa-9e4c-c1f63a7e188b.png. The user's existing logo artwork is used instead of the generated geometric symbol; sidebar logo and source remain unchanged, as explicitly requested.

Comparison evidence: TEST_RESULTS/header-513-review/reference-and-built.png combines the density-normalized reference header and built native header in one image. Six header contact sheets cover 32 distinct surfaces. Captures are in header-513-final-all, header-513-final-extra and header-513-verified; latest verified captures supersede earlier ones for the eight listed editor/Payment surfaces. Native chrome is excluded from the focused header comparison. Narrow and editor captures live in TEST_RESULTS/headers-1.1.513. Scrolled AMC/equipment screenshots live in header-513-scrolled. Whole-app context uses the existing sidebar control in header-513-full; reflection hosting extracts the test host's executable icon, so sidebar branding preservation is additionally established by unchanged MainForm.cs and app_logo_sidebar.png source, not by that host icon.

Required fidelity surfaces reviewed: Segoe UI typography with larger brand/search/account text; broad horizontal search spacing and a wrapped search row at narrow widths; white/navy/pale-blue tokens and thin divider; original transparent logo at preserved aspect ratio plus library search/bell/chevron icons; exact global search prompt and Ctrl+K capsule, actual signed-in identity/role and actual module content. Existing Indian financial terminology remains; mock USD amounts are not copied.

Iteration findings and fixes:
- Initial nested-editor content covered the header: corrected WinForms docking order; subsequent captures show content below chrome.
- Header addition squeezed long editor fields: retain content height inside a scrolling viewport. Increased AMC local heading/field minimum space; bottom-scroll screenshot shows Notes, Save and Cancel.
- First header typography/spacing was smaller than the target: increased sizes and search span, recaptured and compared together.
- Payments' recursive button styling boxed the bell: protect header-owned button styles; latest Payment capture and regression show borderless notification control.
- Scrollbar feedback caused unnecessary horizontal editor scrolling: content follows viewport width. Nested editor regressions pass at 430/600/1440px.

Functional evidence: 39 CI-safe checks; nine dashboard regression groups; 21 header titles at four widths; 25 real main/detail constructors; nested editor layout/idempotence/original input/action/disabled-state tests at three widths; account-menu routing and User Guide Ctrl+K/cancel checks. Tests use the explicitly disposable HVAC_PRO_UI_20261014 connection scope and do not invoke customer business-save/delete actions. Release build succeeds and EXE exists.

Scope and limits: this is native desktop implementation, not a website prototype. The selected header applies through the shared header builder and base page/form hooks. Embedded pages inherit enclosing chrome; transient alerts/search/confirmation/auth/license/document-preview dialogs retain focused layouts. No new services, packages, SQL schema or PDF header changes. Representative captures do not exhaust every DPI, role, data length or nested workflow state. No unresolved blocking defect was observed in the reviewed header states. Existing module-body layouts outside this request remain.
