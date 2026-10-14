# Main dashboard quotation visual parity

User requested implementation after the next-release requirement was recorded. Baseline: 5369564dbe3cb4f3b302718bf17802e5921f5607 (verified release 1.1.511). Isolated attached worktree reused; original primary source edits preserved.

Evidence before implementation: My Work used independent SoftMetric/SoftCard/SoftButton, 38px rounded badges, regular 8.8pt metric captions and 16/19pt values. Quotations used QuotationForecastCard, 42px square icon surfaces, bold 8.2pt captions, 15pt values, 7.5pt subtitles and 30px actions. Existing backlog records acceptance and affected files before editing.

Implementation: shared DashboardVisuals factories for card, metric, integrated heading, action and filter chip; both My Work and Quotations call these factories. My Work retains its data, action callbacks, previews, sorting and page sizes. Narrow metrics use two rows below 1050px available width. The section subtitle reserves action width so it cannot overpaint adjacent controls. Per-control icon clones are disposed with their picture controls.

Validation: Release build passed; nine dashboard regression groups passed. Includes 1440/1000/760px layouts, card/metric types/fonts/icon dimensions, selected filter style, previews, pagination, shortcuts, permissions, technician ownership and empty data. Matching 1710px native captures of DashboardForm and TenderBidForm reviewed side by side; action/subtitle overlap fixed. Final version evidence and screenshots retained in TEST_RESULTS/dashboard-parity* and dashboard-parity*.log.

No new packages. No database migrations, saves, pricing, license or PDF changes. Render-capable fixtures use HVAC_PRO_UI_20261014. No normal application startup or force-close was used.

Publication: 1.1.512 is a candidate. Do not overwrite 1.1.511 or bypass the standing one-product-release-per-rolling-24-hours rule. Earliest next publication: 15 October 2026 20:00:06 UTC (22:00:06 Europe/Berlin), absent an explicit user override. Preserve the prior release and verify remote versions again at publication.

Final validation: 1.1.512 Release build passed; 39 CI-safe PASS plus nine dashboard regression groups PASS. Hosted My Work/Quotations captures at 1710px and My Work at 1000px plus regression snapshots at 1440/1000/760 and empty state reviewed. Narrow toolbar ordering/date width corrected. Candidate packaging guard outputs retained separately; publication is deliberately pending the release window. No new packages.
