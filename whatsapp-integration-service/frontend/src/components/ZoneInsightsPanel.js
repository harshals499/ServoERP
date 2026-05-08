import React from "react";

function StatRow({ label, value, tone = "slate" }) {
  const toneClass = {
    red: "text-rose-300",
    amber: "text-amber-300",
    green: "text-emerald-300",
    sky: "text-sky-300",
    slate: "text-slate-200"
  }[tone] || "text-slate-200";

  return (
    <div className="flex items-center justify-between border-b border-white/10 py-3 text-sm">
      <span className="text-slate-300">{label}</span>
      <span className={`font-semibold ${toneClass}`}>{value}</span>
    </div>
  );
}

function Section({ title, children }) {
  return (
    <section className="rounded-2xl border border-white/10 bg-slate-900/80 p-5 shadow-lg">
      <div className="mb-4 text-sm font-semibold uppercase tracking-[0.18em] text-slate-400">{title}</div>
      {children}
    </section>
  );
}

export default function ZoneInsightsPanel({ insights, loading, onExport, onFollowUp }) {
  if (loading) {
    return (
      <div className="space-y-4">
        <div className="h-40 animate-pulse rounded-2xl bg-slate-800/70" />
        <div className="h-64 animate-pulse rounded-2xl bg-slate-800/70" />
        <div className="h-40 animate-pulse rounded-2xl bg-slate-800/70" />
      </div>
    );
  }

  if (!insights) {
    return (
      <div className="rounded-2xl border border-dashed border-slate-700 bg-slate-900/70 p-8 text-sm text-slate-300">
        Pick a city zone on the map or from the cards below to load operational analysis here.
      </div>
    );
  }

  const demandTone = insights.demandScore >= 80 ? "red" : insights.demandScore >= 60 ? "amber" : "green";
  const travelTone = insights.avgTravelTime >= 45 ? "amber" : "green";

  return (
    <div className="space-y-4">
      <section className="rounded-2xl border border-sky-500/30 bg-gradient-to-br from-slate-950 via-slate-900 to-slate-950 p-6 shadow-2xl">
        <div className="flex items-start justify-between gap-4">
          <div>
            <div className="text-xs font-semibold uppercase tracking-[0.24em] text-sky-300">Zone Insights</div>
            <h2 className="mt-2 text-2xl font-semibold text-white">{insights.label}</h2>
            <p className="mt-2 text-sm text-slate-300">Demand, breakdown pressure, and dispatch quality for the selected zone.</p>
          </div>
          <span className={`rounded-full px-3 py-1 text-xs font-semibold ${demandTone === "red" ? "bg-rose-500/15 text-rose-300" : demandTone === "amber" ? "bg-amber-500/15 text-amber-300" : "bg-emerald-500/15 text-emerald-300"}`}>
            {insights.demandLevel}
          </span>
        </div>

        <div className="mt-6 grid gap-3 sm:grid-cols-2">
          <div className="rounded-xl border border-white/10 bg-white/5 p-4">
            <div className="text-xs uppercase tracking-[0.2em] text-slate-400">Demand Score</div>
            <div className="mt-2 text-3xl font-bold text-white">{insights.demandScore}</div>
          </div>
          <div className="rounded-xl border border-white/10 bg-white/5 p-4">
            <div className="text-xs uppercase tracking-[0.2em] text-slate-400">Top Issue</div>
            <div className="mt-2 text-lg font-semibold text-white">{insights.topIssue}</div>
          </div>
        </div>

        <div className="mt-6 flex flex-wrap gap-3">
          <button type="button" onClick={onExport} className="rounded-xl bg-sky-600 px-4 py-2 text-sm font-semibold text-white shadow-lg shadow-sky-900/30">
            Export Zone Snapshot
          </button>
          <button type="button" onClick={onFollowUp} className="rounded-xl border border-white/15 bg-white/5 px-4 py-2 text-sm font-semibold text-slate-100">
            Follow Up Action
          </button>
        </div>
      </section>

      <Section title="Operational Snapshot">
        <StatRow label="Breakdown Rate" value={`${insights.breakdownRate}%`} tone={insights.breakdownRate >= 30 ? "red" : "green"} />
        <StatRow label="Average Travel Time" value={`${insights.avgTravelTime} mins`} tone={travelTone} />
        <StatRow label="Open Jobs" value={insights.openJobs} tone={insights.openJobs >= insights.completedJobs ? "amber" : "green"} />
        <StatRow label="Completed Jobs" value={insights.completedJobs} tone="sky" />
      </Section>

      <Section title="Smart Insights">
        <ul className="space-y-3 text-sm text-slate-200">
          {(insights.insights || []).map((item) => (
            <li key={item} className="rounded-xl border border-white/10 bg-white/5 px-4 py-3">
              {item}
            </li>
          ))}
        </ul>
      </Section>

      <Section title="Recommended Technicians">
        <div className="space-y-3">
          {(insights.recommendedTechnicians || []).map((tech) => (
            <div key={tech.technicianId} className="rounded-xl border border-white/10 bg-white/5 p-4">
              <div className="flex items-center justify-between gap-3">
                <div>
                  <div className="font-semibold text-white">{tech.technicianId}</div>
                  <div className="mt-1 text-xs text-slate-400">{tech.specialization || "General coverage"}</div>
                </div>
                <div className="text-right text-sm">
                  <div className="font-semibold text-emerald-300">{tech.completionRate}% complete</div>
                  <div className="text-slate-400">{tech.jobsHandled} jobs handled</div>
                </div>
              </div>
            </div>
          ))}
          {(!insights.recommendedTechnicians || insights.recommendedTechnicians.length === 0) && (
            <div className="rounded-xl border border-dashed border-white/10 px-4 py-5 text-sm text-slate-400">
              No technician history found for this zone yet.
            </div>
          )}
        </div>
      </Section>

      <Section title="Demand Mix">
        <div className="space-y-3">
          {(insights.serviceTypeBreakdown || []).slice(0, 5).map((item) => (
            <div key={item.serviceType} className="rounded-xl border border-white/10 bg-white/5 px-4 py-3">
              <div className="flex items-center justify-between gap-4">
                <span className="text-sm text-slate-200">{item.serviceType}</span>
                <span className="text-sm font-semibold text-white">{item.count}</span>
              </div>
            </div>
          ))}
        </div>
      </Section>

      <Section title="Trend">
        <div className="space-y-3">
          {(insights.trend || []).slice(-6).map((item) => (
            <div key={item.bucket} className="rounded-xl border border-white/10 bg-white/5 px-4 py-3">
              <div className="flex items-center justify-between gap-4 text-sm">
                <span className="text-slate-300">{item.bucket}</span>
                <span className="font-semibold text-white">{item.totalJobs} jobs</span>
              </div>
              <div className="mt-2 text-xs text-slate-400">
                {item.breakdowns} breakdowns - {item.avgTravelTime} mins avg travel
              </div>
            </div>
          ))}
        </div>
      </Section>

      <Section title="Recent Jobs">
        <div className="overflow-x-auto">
          <table className="min-w-full text-left text-sm">
            <thead className="text-slate-400">
              <tr>
                <th className="pb-3 pr-4 font-medium">Work Order</th>
                <th className="pb-3 pr-4 font-medium">Client</th>
                <th className="pb-3 pr-4 font-medium">Service</th>
                <th className="pb-3 pr-4 font-medium">Status</th>
                <th className="pb-3 font-medium">Travel</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-white/10 text-slate-200">
              {(insights.recentJobs || []).map((job) => (
                <tr key={job.id}>
                  <td className="py-3 pr-4">{job.workOrderId || "-"}</td>
                  <td className="py-3 pr-4">{job.customerName}</td>
                  <td className="py-3 pr-4">{job.serviceType}</td>
                  <td className="py-3 pr-4">{job.status}</td>
                  <td className="py-3">{job.travelTimeMinutes} mins</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </Section>
    </div>
  );
}
