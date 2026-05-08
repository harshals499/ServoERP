import React, { useEffect, useMemo, useState } from "react";
import GeoMap from "../components/GeoMap";
import ZoneInsightsPanel from "../components/ZoneInsightsPanel";

const RANGE_OPTIONS = [
  { value: "7d", label: "Last 7 Days" },
  { value: "30d", label: "Last 30 Days" },
  { value: "90d", label: "Last 90 Days" }
];

function buildQuery(filters) {
  const params = new URLSearchParams();
  Object.entries(filters).forEach(([key, value]) => {
    if (value) {
      params.set(key, value);
    }
  });
  return params.toString();
}

function CardShell({ title, children }) {
  return (
    <section className="rounded-2xl border border-slate-800 bg-white/95 p-5 shadow-xl shadow-slate-950/10">
      <h3 className="text-lg font-semibold text-slate-900">{title}</h3>
      <div className="mt-4">{children}</div>
    </section>
  );
}

function ListCard({ title, items = [], onZoneSelect, renderMeta }) {
  return (
    <CardShell title={title}>
      <div className="space-y-3">
        {items.map((item, index) => (
          <button
            key={`${title}-${item.zoneId}`}
            type="button"
            onClick={() => onZoneSelect(item.zoneId)}
            className="flex w-full items-start justify-between rounded-xl border border-slate-200 px-4 py-3 text-left transition hover:border-sky-300 hover:bg-sky-50"
          >
            <div>
              <div className="text-sm font-semibold text-slate-900">
                {index + 1}. {item.label}
              </div>
              <div className="mt-1 text-xs text-slate-500">{renderMeta(item)}</div>
            </div>
          </button>
        ))}
        {items.length === 0 && <div className="text-sm text-slate-500">No data matches the current filters.</div>}
      </div>
    </CardShell>
  );
}

export default function GeoIntelligencePage() {
  const [filters, setFilters] = useState({
    city: "",
    serviceType: "",
    technicianId: "",
    dateRange: "30d"
  });
  const [heatmapData, setHeatmapData] = useState(null);
  const [selectedZoneId, setSelectedZoneId] = useState("");
  const [zoneInsights, setZoneInsights] = useState(null);
  const [loadingHeatmap, setLoadingHeatmap] = useState(true);
  const [loadingInsights, setLoadingInsights] = useState(false);
  const [error, setError] = useState("");

  useEffect(() => {
    let active = true;

    async function loadHeatmap() {
      setLoadingHeatmap(true);
      setError("");

      try {
        const response = await fetch(`/geo/heatmap-data?${buildQuery(filters)}`);
        const payload = await response.json();
        if (!response.ok || !payload.ok) {
          throw new Error(payload.error || "Unable to load geo-intelligence data.");
        }

        if (!active) {
          return;
        }

        setHeatmapData(payload);
        const nextZoneId = payload.zones?.[0]?.zoneId || "";
        setSelectedZoneId((current) => {
          const currentExists = payload.zones?.some((zone) => zone.zoneId === current);
          return currentExists ? current : nextZoneId;
        });
      } catch (loadError) {
        if (active) {
          setError(loadError.message);
        }
      } finally {
        if (active) {
          setLoadingHeatmap(false);
        }
      }
    }

    loadHeatmap();
    return () => {
      active = false;
    };
  }, [filters.city, filters.serviceType, filters.technicianId, filters.dateRange]);

  useEffect(() => {
    if (!selectedZoneId) {
      return;
    }

    let active = true;

    async function loadZoneInsights() {
      setLoadingInsights(true);

      try {
        const response = await fetch(`/geo/zone-insights?${buildQuery({ ...filters, zoneId: selectedZoneId })}`);
        const payload = await response.json();
        if (!response.ok || !payload.ok) {
          throw new Error(payload.error || "Unable to load zone analysis.");
        }

        if (active) {
          setZoneInsights(payload.zone);
        }
      } catch (loadError) {
        if (active) {
          setError(loadError.message);
        }
      } finally {
        if (active) {
          setLoadingInsights(false);
        }
      }
    }

    loadZoneInsights();
    return () => {
      active = false;
    };
  }, [filters.city, filters.serviceType, filters.technicianId, filters.dateRange, selectedZoneId]);

  const filterOptions = useMemo(() => {
    const zones = heatmapData?.zones || [];
    return {
      cities: [...new Set(zones.map((zone) => zone.city).filter(Boolean))],
      serviceTypes: [...new Set(zones.flatMap((zone) => zone.serviceTypes || []).filter(Boolean))],
      technicians: [...new Set((zoneInsights?.recommendedTechnicians || []).map((tech) => tech.technicianId).filter(Boolean))]
    };
  }, [heatmapData, zoneInsights]);

  const selectedZoneSummary = useMemo(
    () => heatmapData?.zones?.find((zone) => zone.zoneId === selectedZoneId) || null,
    [heatmapData, selectedZoneId]
  );

  function exportZoneSnapshot() {
    if (!zoneInsights) {
      return;
    }

    const rows = [
      ["Zone", zoneInsights.label],
      ["Demand Score", zoneInsights.demandScore],
      ["Breakdown Rate", `${zoneInsights.breakdownRate}%`],
      ["Average Travel Time", `${zoneInsights.avgTravelTime} mins`],
      ["Open Jobs", zoneInsights.openJobs],
      ["Completed Jobs", zoneInsights.completedJobs],
      ["Top Issue", zoneInsights.topIssue]
    ];

    const csv = rows.map((row) => row.map((cell) => `"${String(cell).replace(/"/g, '""')}"`).join(",")).join("\n");
    const blob = new Blob([csv], { type: "text/csv;charset=utf-8;" });
    const url = URL.createObjectURL(blob);
    const link = document.createElement("a");
    link.href = url;
    link.download = `${zoneInsights.label.replace(/[^a-z0-9]+/gi, "-").toLowerCase()}-snapshot.csv`;
    link.click();
    URL.revokeObjectURL(url);
  }

  function openFollowUp() {
    if (!zoneInsights) {
      return;
    }

    const note = encodeURIComponent(
      `Geo-Intelligence follow-up for ${zoneInsights.label}\nDemand score: ${zoneInsights.demandScore}\nBreakdown rate: ${zoneInsights.breakdownRate}%\nTop issue: ${zoneInsights.topIssue}`
    );
    window.location.href = `mailto:?subject=${encodeURIComponent(`Zone Follow Up - ${zoneInsights.label}`)}&body=${note}`;
  }

  return (
    <div className="min-h-screen bg-slate-100">
      <div className="border-b border-slate-200 bg-slate-950 text-white">
        <div className="mx-auto max-w-[1600px] px-6 py-6">
          <div className="flex flex-col gap-5 xl:flex-row xl:items-center xl:justify-between">
            <div>
              <div className="text-xs font-semibold uppercase tracking-[0.25em] text-sky-300">Analytics</div>
              <h1 className="mt-2 text-4xl font-bold tracking-tight">Geo-Intelligence</h1>
              <p className="mt-2 max-w-3xl text-sm text-slate-300">
                Live location analytics for service demand, breakdown concentration, and technician allocation across Indian cities.
              </p>
            </div>

            <div className="grid gap-3 md:grid-cols-2 xl:grid-cols-4">
              <select
                value={filters.dateRange}
                onChange={(event) => setFilters((current) => ({ ...current, dateRange: event.target.value }))}
                className="rounded-xl border border-white/10 bg-white/10 px-4 py-3 text-sm text-white outline-none ring-0"
              >
                {RANGE_OPTIONS.map((option) => (
                  <option key={option.value} value={option.value} className="text-slate-900">
                    {option.label}
                  </option>
                ))}
              </select>

              <select
                value={filters.city}
                onChange={(event) => setFilters((current) => ({ ...current, city: event.target.value }))}
                className="rounded-xl border border-white/10 bg-white/10 px-4 py-3 text-sm text-white outline-none ring-0"
              >
                <option value="" className="text-slate-900">All Cities</option>
                {filterOptions.cities.map((city) => (
                  <option key={city} value={city} className="text-slate-900">{city}</option>
                ))}
              </select>

              <select
                value={filters.serviceType}
                onChange={(event) => setFilters((current) => ({ ...current, serviceType: event.target.value }))}
                className="rounded-xl border border-white/10 bg-white/10 px-4 py-3 text-sm text-white outline-none ring-0"
              >
                <option value="" className="text-slate-900">All Service Types</option>
                {filterOptions.serviceTypes.map((serviceType) => (
                  <option key={serviceType} value={serviceType} className="text-slate-900">{serviceType}</option>
                ))}
              </select>

              <input
                value={filters.technicianId}
                onChange={(event) => setFilters((current) => ({ ...current, technicianId: event.target.value }))}
                placeholder="Technician ID"
                className="rounded-xl border border-white/10 bg-white/10 px-4 py-3 text-sm text-white placeholder:text-slate-400"
              />
            </div>
          </div>
        </div>
      </div>

      <div className="mx-auto max-w-[1600px] px-6 py-6">
        {error && (
          <div className="mb-6 rounded-2xl border border-rose-200 bg-rose-50 px-4 py-3 text-sm text-rose-700">
            {error}
          </div>
        )}

        {heatmapData?.summary && (
          <div className="mb-6 grid gap-4 md:grid-cols-2 xl:grid-cols-4">
            <CardShell title="Total Jobs">
              <div className="text-3xl font-bold text-slate-900">{heatmapData.summary.totalJobs}</div>
              <div className="mt-2 text-sm text-slate-500">service visits in selected range</div>
            </CardShell>
            <CardShell title="Active Zones">
              <div className="text-3xl font-bold text-slate-900">{heatmapData.summary.activeZones}</div>
              <div className="mt-2 text-sm text-slate-500">zones with mapped service demand</div>
            </CardShell>
            <CardShell title="Average Demand Score">
              <div className="text-3xl font-bold text-slate-900">{heatmapData.summary.avgDemandScore}</div>
              <div className="mt-2 text-sm text-slate-500">blended demand pressure across visible zones</div>
            </CardShell>
            <CardShell title="Average Travel Time">
              <div className="text-3xl font-bold text-slate-900">{heatmapData.summary.avgTravelTime} mins</div>
              <div className="mt-2 text-sm text-slate-500">dispatch travel baseline</div>
            </CardShell>
          </div>
        )}

        <div className="grid gap-6 xl:grid-cols-[minmax(0,1.65fr)_420px]">
          <div className="space-y-6">
            {loadingHeatmap ? (
              <div className="h-[540px] animate-pulse rounded-2xl bg-slate-300/50" />
            ) : (
              <GeoMap
                points={heatmapData?.points || []}
                center={heatmapData?.summary?.center}
                selectedZoneId={selectedZoneId}
                onZoneSelect={setSelectedZoneId}
              />
            )}

            {selectedZoneSummary && (
              <div className="grid gap-4 md:grid-cols-4">
                <CardShell title="Demand Score">
                  <div className="text-3xl font-bold text-slate-900">{selectedZoneSummary.demandScore}</div>
                  <div className="mt-2 text-sm text-slate-500">{selectedZoneSummary.demandLevel} activity pressure</div>
                </CardShell>
                <CardShell title="Job Volume">
                  <div className="text-3xl font-bold text-slate-900">{selectedZoneSummary.jobCount}</div>
                  <div className="mt-2 text-sm text-slate-500">service jobs in range</div>
                </CardShell>
                <CardShell title="Breakdown Rate">
                  <div className="text-3xl font-bold text-slate-900">{selectedZoneSummary.breakdownRate}%</div>
                  <div className="mt-2 text-sm text-slate-500">{selectedZoneSummary.breakdownCount} breakdown-driven visits</div>
                </CardShell>
                <CardShell title="Travel Time">
                  <div className="text-3xl font-bold text-slate-900">{selectedZoneSummary.avgTravelTime} mins</div>
                  <div className="mt-2 text-sm text-slate-500">average dispatch travel</div>
                </CardShell>
              </div>
            )}
          </div>

          <ZoneInsightsPanel
            insights={zoneInsights}
            loading={loadingInsights}
            onExport={exportZoneSnapshot}
            onFollowUp={openFollowUp}
          />
        </div>

        <div className="mt-6 grid gap-6 xl:grid-cols-4">
          <ListCard
            title="Top Demand Areas"
            items={heatmapData?.cards?.topDemandAreas || []}
            onZoneSelect={setSelectedZoneId}
            renderMeta={(item) => `Demand score ${item.demandScore} - ${item.totalJobs} jobs`}
          />
          <ListCard
            title="Rising Breakdown Zones"
            items={heatmapData?.cards?.risingBreakdownZones || []}
            onZoneSelect={setSelectedZoneId}
            renderMeta={(item) => `${item.deltaPct}% change - ${item.breakdownCount} breakdown jobs`}
          />
          <ListCard
            title="Travel Efficiency Alerts"
            items={heatmapData?.cards?.travelEfficiencyAlerts || []}
            onZoneSelect={setSelectedZoneId}
            renderMeta={(item) => `${item.avgTravelTime} mins - ${item.recommendation}`}
          />
          <ListCard
            title="AMC Upsell Opportunities"
            items={heatmapData?.cards?.amcUpsellOpportunities || []}
            onZoneSelect={setSelectedZoneId}
            renderMeta={(item) => `Score ${item.opportunityScore} - ${item.breakdownCount} repeat breakdown cases`}
          />
        </div>
      </div>
    </div>
  );
}

/*
Integration notes:

1. Route
   <Route path="/geo-intelligence" element={<GeoIntelligencePage />} />

2. Sidebar section
   {
     section: "Analytics",
     items: [{ label: "Geo-Intelligence", to: "/geo-intelligence", icon: "map" }]
   }

3. Frontend packages expected
   npm install react-leaflet leaflet leaflet.heat

4. Backend integration
   The page expects the Express service to expose:
   GET /geo/heatmap-data
   GET /geo/zone-insights
*/
