const DEFAULT_COLLECTION = process.env.MONGO_SERVICE_COLLECTION || "service_jobs";
const DEFAULT_TIMEZONE = process.env.GEO_TIMEZONE || "Asia/Kolkata";

const INDIA_CENTER = {
  lat: 20.5937,
  lng: 78.9629
};

function getMongoDb(req) {
  if (req.app?.locals?.mongoDb && typeof req.app.locals.mongoDb.collection === "function") {
    return req.app.locals.mongoDb;
  }

  if (req.app?.locals?.mongoClient && typeof req.app.locals.mongoClient.db === "function") {
    const dbName = process.env.MONGO_DB_NAME;
    return dbName ? req.app.locals.mongoClient.db(dbName) : req.app.locals.mongoClient.db();
  }

  const error = new Error(
    "MongoDB connection not attached. Assign your existing Mongo connection to app.locals.mongoDb or app.locals.mongoClient."
  );
  error.statusCode = 503;
  throw error;
}

function getCollection(req) {
  return getMongoDb(req).collection(DEFAULT_COLLECTION);
}

function parseDateRange(query) {
  const now = new Date();
  const rangeKey = String(query.dateRange || "30d").toLowerCase();

  if (rangeKey === "custom") {
    const startDate = query.startDate ? new Date(query.startDate) : new Date(now.getFullYear(), now.getMonth(), now.getDate() - 29);
    const endDate = query.endDate ? new Date(query.endDate) : now;
    endDate.setHours(23, 59, 59, 999);
    return { key: rangeKey, startDate, endDate };
  }

  const dayMap = {
    "7d": 7,
    "14d": 14,
    "30d": 30,
    "60d": 60,
    "90d": 90
  };

  const days = dayMap[rangeKey] || 30;
  const startDate = new Date(now);
  startDate.setDate(startDate.getDate() - (days - 1));
  startDate.setHours(0, 0, 0, 0);

  const endDate = new Date(now);
  endDate.setHours(23, 59, 59, 999);
  return { key: rangeKey, startDate, endDate };
}

function shiftRange(range) {
  const startDate = new Date(range.startDate);
  const endDate = new Date(range.endDate);
  const duration = endDate.getTime() - startDate.getTime();
  const previousEnd = new Date(startDate.getTime() - 1);
  const previousStart = new Date(previousEnd.getTime() - duration);
  return {
    key: `${range.key}-previous`,
    startDate: previousStart,
    endDate: previousEnd
  };
}

function buildMatch({ city, serviceType, technicianId, startDate, endDate, zone }) {
  const match = {
    timestamp: {
      $gte: startDate,
      $lte: endDate
    }
  };

  if (city) {
    match.city = city;
  }

  if (serviceType) {
    match.serviceType = serviceType;
  }

  if (technicianId) {
    match.technicianId = technicianId;
  }

  if (zone?.city) {
    match.city = zone.city;
  }

  if (zone?.area) {
    match.area = zone.area;
  }

  return match;
}

function safePercent(value, total) {
  if (!total) {
    return 0;
  }
  return Number(((value / total) * 100).toFixed(1));
}

function round(value, digits = 1) {
  const factor = Math.pow(10, digits);
  return Math.round((Number(value) || 0) * factor) / factor;
}

function demandLevel(score) {
  if (score >= 80) return "Critical";
  if (score >= 60) return "High";
  if (score >= 40) return "Moderate";
  return "Stable";
}

function zoneLabel(city, area) {
  if (city && area) {
    return `${area}, ${city}`;
  }
  return area || city || "Unknown Zone";
}

function encodeZoneId(city, area) {
  return Buffer.from(JSON.stringify({ city: city || "", area: area || "" })).toString("base64url");
}

function decodeZoneId(value) {
  try {
    const json = Buffer.from(String(value || ""), "base64url").toString("utf8");
    return JSON.parse(json);
  } catch (error) {
    const fallback = Buffer.from(String(value || ""), "base64").toString("utf8");
    return JSON.parse(fallback);
  }
}

function aggregateProjection() {
  return {
    city: { $ifNull: ["$city", "Unassigned City"] },
    area: { $ifNull: ["$area", "Unassigned Area"] },
    serviceType: { $ifNull: ["$serviceType", "General Service"] },
    technicianId: { $ifNull: ["$technicianId", "Unassigned"] },
    status: { $ifNull: ["$status", "Open"] },
    timestamp: "$timestamp",
    customerName: { $ifNull: ["$clientName", { $ifNull: ["$customerName", "Unknown Client"] }] },
    workOrderId: { $ifNull: ["$workOrderId", "$jobId"] },
    travelTimeMinutes: {
      $convert: {
        input: {
          $ifNull: [
            "$travelTimeMinutes",
            {
              $ifNull: [
                "$travel.minutes",
                {
                  $ifNull: [
                    "$metrics.travelMinutes",
                    {
                      $ifNull: ["$actualTravelMinutes", 0]
                    }
                  ]
                }
              ]
            }
          ]
        },
        to: "double",
        onError: 0,
        onNull: 0
      }
    },
    lat: {
      $convert: {
        input: {
          $ifNull: [
            "$coordinates.lat",
            {
              $ifNull: [
                "$coordinates.latitude",
                {
                  $cond: [{ $isArray: "$coordinates" }, { $arrayElemAt: ["$coordinates", 1] }, null]
                }
              ]
            }
          ]
        },
        to: "double",
        onError: null,
        onNull: null
      }
    },
    lng: {
      $convert: {
        input: {
          $ifNull: [
            "$coordinates.lng",
            {
              $ifNull: [
                "$coordinates.longitude",
                {
                  $cond: [{ $isArray: "$coordinates" }, { $arrayElemAt: ["$coordinates", 0] }, null]
                }
              ]
            }
          ]
        },
        to: "double",
        onError: null,
        onNull: null
      }
    }
  };
}

function aggregateDecorators() {
  return {
    statusLower: { $toLower: "$status" },
    serviceTypeLower: { $toLower: "$serviceType" },
    isBreakdown: {
      $cond: [
        {
          $or: [
            { $regexMatch: { input: { $toLower: "$serviceType" }, regex: "repair|breakdown|fault|gas|complaint" } },
            { $regexMatch: { input: { $toLower: "$status" }, regex: "breakdown|repeat|escalat" } }
          ]
        },
        1,
        0
      ]
    },
    isClosed: {
      $cond: [{ $regexMatch: { input: { $toLower: "$status" }, regex: "complete|closed|resolved|done" } }, 1, 0]
    },
    isOpen: {
      $cond: [{ $regexMatch: { input: { $toLower: "$status" }, regex: "open|pending|assigned|progress|scheduled" } }, 1, 0]
    }
  };
}

async function getZoneAggregates(collection, match) {
  return collection
    .aggregate([
      { $match: match },
      { $project: aggregateProjection() },
      { $match: { lat: { $ne: null }, lng: { $ne: null } } },
      { $addFields: aggregateDecorators() },
      {
        $group: {
          _id: {
            city: "$city",
            area: "$area"
          },
          lat: { $avg: "$lat" },
          lng: { $avg: "$lng" },
          jobCount: { $sum: 1 },
          breakdownCount: { $sum: "$isBreakdown" },
          openJobs: { $sum: "$isOpen" },
          completedJobs: { $sum: "$isClosed" },
          avgTravelTime: { $avg: "$travelTimeMinutes" },
          serviceTypes: { $addToSet: "$serviceType" },
          statuses: { $addToSet: "$status" }
        }
      },
      { $sort: { jobCount: -1 } }
    ])
    .toArray();
}

function enrichZones(rows, previousLookup = new Map()) {
  const maxJobs = rows.reduce((max, row) => Math.max(max, row.jobCount || 0), 0) || 1;

  return rows.map((row) => {
    const city = row._id.city;
    const area = row._id.area;
    const zoneId = encodeZoneId(city, area);
    const breakdownRate = safePercent(row.breakdownCount || 0, row.jobCount || 0);
    const openRate = safePercent(row.openJobs || 0, row.jobCount || 0);
    const avgTravelTime = round(row.avgTravelTime || 0, 1);
    const previous = previousLookup.get(zoneId);
    const previousBreakdowns = previous?.breakdownCount || 0;
    const currentBreakdowns = row.breakdownCount || 0;
    const breakdownDeltaPct =
      previousBreakdowns > 0 ? round(((currentBreakdowns - previousBreakdowns) / previousBreakdowns) * 100, 1) : currentBreakdowns > 0 ? 100 : 0;

    const volumeScore = (row.jobCount / maxJobs) * 55;
    const breakdownScore = (breakdownRate / 100) * 25;
    const travelScore = Math.min(avgTravelTime, 60) / 60 * 10;
    const openScore = (openRate / 100) * 10;
    const demandScore = Math.min(100, Math.round(volumeScore + breakdownScore + travelScore + openScore));

    return {
      zoneId,
      city,
      area,
      label: zoneLabel(city, area),
      lat: round(row.lat, 6),
      lng: round(row.lng, 6),
      jobCount: row.jobCount || 0,
      breakdownCount: currentBreakdowns,
      breakdownRate,
      openJobs: row.openJobs || 0,
      completedJobs: row.completedJobs || 0,
      avgTravelTime,
      demandScore,
      demandLevel: demandLevel(demandScore),
      intensity: Math.max(0.15, round((row.jobCount / maxJobs) * 1.2, 2)),
      serviceTypes: row.serviceTypes || [],
      statuses: row.statuses || [],
      breakdownDeltaPct
    };
  });
}

async function buildHeatmapPayload(req) {
  const collection = getCollection(req);
  const range = parseDateRange(req.query || {});
  const previousRange = shiftRange(range);
  const filters = {
    city: req.query.city || "",
    serviceType: req.query.serviceType || "",
    technicianId: req.query.technicianId || ""
  };

  const [currentRows, previousRows] = await Promise.all([
    getZoneAggregates(collection, buildMatch({ ...filters, startDate: range.startDate, endDate: range.endDate })),
    getZoneAggregates(collection, buildMatch({ ...filters, startDate: previousRange.startDate, endDate: previousRange.endDate }))
  ]);

  const previousLookup = new Map(previousRows.map((row) => [encodeZoneId(row._id.city, row._id.area), row]));

  const zones = enrichZones(currentRows, previousLookup);
  const sortedByDemand = [...zones].sort((a, b) => b.demandScore - a.demandScore);
  const sortedByBreakdownDelta = [...zones].sort((a, b) => b.breakdownDeltaPct - a.breakdownDeltaPct);
  const sortedByTravel = [...zones].sort((a, b) => b.avgTravelTime - a.avgTravelTime);
  const sortedByUpsell = [...zones].sort((a, b) => (b.breakdownRate * b.jobCount) - (a.breakdownRate * a.jobCount));

  return {
    ok: true,
    filters: {
      ...filters,
      dateRange: range.key,
      startDate: range.startDate,
      endDate: range.endDate
    },
    summary: {
      totalJobs: zones.reduce((sum, zone) => sum + zone.jobCount, 0),
      activeZones: zones.length,
      avgDemandScore: round(zones.reduce((sum, zone) => sum + zone.demandScore, 0) / Math.max(zones.length, 1), 1),
      avgTravelTime: round(zones.reduce((sum, zone) => sum + zone.avgTravelTime, 0) / Math.max(zones.length, 1), 1),
      center: zones.length > 0
        ? {
            lat: round(zones.reduce((sum, zone) => sum + zone.lat, 0) / zones.length, 6),
            lng: round(zones.reduce((sum, zone) => sum + zone.lng, 0) / zones.length, 6)
          }
        : INDIA_CENTER
    },
    points: zones.map((zone) => ({
      zoneId: zone.zoneId,
      lat: zone.lat,
      lng: zone.lng,
      intensity: zone.intensity,
      demandScore: zone.demandScore,
      demandLevel: zone.demandLevel,
      label: zone.label,
      city: zone.city,
      area: zone.area,
      jobCount: zone.jobCount,
      breakdownRate: zone.breakdownRate,
      avgTravelTime: zone.avgTravelTime
    })),
    zones,
    cards: {
      topDemandAreas: sortedByDemand.slice(0, 5).map((zone) => ({
        zoneId: zone.zoneId,
        label: zone.label,
        city: zone.city,
        demandScore: zone.demandScore,
        totalJobs: zone.jobCount
      })),
      risingBreakdownZones: sortedByBreakdownDelta.slice(0, 5).map((zone) => ({
        zoneId: zone.zoneId,
        label: zone.label,
        deltaPct: zone.breakdownDeltaPct,
        breakdownCount: zone.breakdownCount
      })),
      travelEfficiencyAlerts: sortedByTravel.slice(0, 5).map((zone) => ({
        zoneId: zone.zoneId,
        label: zone.label,
        avgTravelTime: zone.avgTravelTime,
        recommendation: zone.avgTravelTime > 45 ? "Consider route optimization or local technician allocation." : "Travel time is within expected range."
      })),
      amcUpsellOpportunities: sortedByUpsell.slice(0, 5).map((zone) => ({
        zoneId: zone.zoneId,
        label: zone.label,
        opportunityScore: Math.min(100, Math.round(zone.breakdownRate * 0.7 + zone.jobCount * 2.5)),
        breakdownCount: zone.breakdownCount,
        suggestion: "Target repeat breakdown customers with AMC renewal outreach."
      }))
    }
  };
}

async function getHeatmapData(req, res) {
  try {
    const payload = await buildHeatmapPayload(req);
    res.json(payload);
  } catch (error) {
    res.status(error.statusCode || 500).json({
      ok: false,
      error: error.message
    });
  }
}

async function getZoneInsights(req, res) {
  try {
    if (!req.query.zoneId) {
      return res.status(400).json({ ok: false, error: "Missing required query parameter: zoneId" });
    }

    const zone = decodeZoneId(req.query.zoneId);
    const collection = getCollection(req);
    const range = parseDateRange(req.query || {});
    const match = buildMatch({
      city: req.query.city || "",
      serviceType: req.query.serviceType || "",
      technicianId: req.query.technicianId || "",
      startDate: range.startDate,
      endDate: range.endDate,
      zone
    });

    const [summary] = await collection
      .aggregate([
        { $match: match },
        { $project: aggregateProjection() },
        { $addFields: aggregateDecorators() },
        {
          $group: {
            _id: null,
            totalJobs: { $sum: 1 },
            breakdownCount: { $sum: "$isBreakdown" },
            openJobs: { $sum: "$isOpen" },
            completedJobs: { $sum: "$isClosed" },
            avgTravelTime: { $avg: "$travelTimeMinutes" }
          }
        }
      ])
      .toArray();

    if (!summary?.totalJobs) {
      return res.status(404).json({ ok: false, error: "No jobs found for the selected zone and filter set." });
    }

    const [serviceTypeBreakdown, statusBreakdown, recommendedTechnicians, recentJobs, trend] = await Promise.all([
      collection.aggregate([{ $match: match }, { $project: aggregateProjection() }, { $group: { _id: "$serviceType", count: { $sum: 1 } } }, { $sort: { count: -1 } }]).toArray(),
      collection.aggregate([{ $match: match }, { $project: aggregateProjection() }, { $group: { _id: "$status", count: { $sum: 1 } } }, { $sort: { count: -1 } }]).toArray(),
      collection
        .aggregate([
          { $match: match },
          { $project: aggregateProjection() },
          { $addFields: aggregateDecorators() },
          {
            $group: {
              _id: "$technicianId",
              jobsHandled: { $sum: 1 },
              completedJobs: { $sum: "$isClosed" },
              avgTravelTime: { $avg: "$travelTimeMinutes" },
              focusTypes: { $addToSet: "$serviceType" }
            }
          },
          { $sort: { completedJobs: -1, jobsHandled: -1 } },
          { $limit: 5 }
        ])
        .toArray(),
      collection.aggregate([{ $match: match }, { $project: aggregateProjection() }, { $sort: { timestamp: -1 } }, { $limit: 10 }]).toArray(),
      collection
        .aggregate([
          { $match: match },
          { $project: aggregateProjection() },
          { $addFields: aggregateDecorators() },
          {
            $group: {
              _id: {
                bucket: {
                  $dateToString: {
                    format: "%Y-%m-%d",
                    date: "$timestamp",
                    timezone: DEFAULT_TIMEZONE
                  }
                }
              },
              totalJobs: { $sum: 1 },
              breakdowns: { $sum: "$isBreakdown" },
              avgTravelTime: { $avg: "$travelTimeMinutes" }
            }
          },
          { $sort: { "_id.bucket": 1 } }
        ])
        .toArray()
    ]);

    const breakdownRate = safePercent(summary.breakdownCount, summary.totalJobs);
    const openRate = safePercent(summary.openJobs, summary.totalJobs);
    const avgTravelTime = round(summary.avgTravelTime || 0, 1);
    const demandScore = Math.min(100, Math.round((summary.totalJobs * 3) + (breakdownRate * 0.35) + (openRate * 0.15) + (Math.min(avgTravelTime, 60) * 0.2)));

    const topIssue = serviceTypeBreakdown[0]?._id || "General Service";
    const insights = [];
    if (breakdownRate >= 30) {
      insights.push("Breakdown frequency is elevated. Prioritize root-cause fixes and preventive maintenance outreach.");
    }
    if (avgTravelTime >= 40) {
      insights.push("Travel time is high for this zone. Consider rebalancing technician coverage or dispatch windows.");
    }
    if (openRate >= 35) {
      insights.push("Open job pressure is building in this zone. Escalate scheduling before SLA risk grows.");
    }
    if (insights.length === 0) {
      insights.push("Zone performance is stable. Keep monitoring demand mix and technician allocation.");
    }

    res.json({
      ok: true,
      zone: {
        zoneId: req.query.zoneId,
        city: zone.city,
        area: zone.area,
        label: zoneLabel(zone.city, zone.area),
        demandScore,
        demandLevel: demandLevel(demandScore),
        breakdownRate,
        avgTravelTime,
        totalJobs: summary.totalJobs,
        openJobs: summary.openJobs,
        completedJobs: summary.completedJobs,
        topIssue,
        insights,
        serviceTypeBreakdown: serviceTypeBreakdown.map((row) => ({
          serviceType: row._id,
          count: row.count
        })),
        statusBreakdown: statusBreakdown.map((row) => ({
          status: row._id,
          count: row.count
        })),
        recommendedTechnicians: recommendedTechnicians.map((row) => ({
          technicianId: row._id,
          jobsHandled: row.jobsHandled,
          completedJobs: row.completedJobs,
          completionRate: safePercent(row.completedJobs, row.jobsHandled),
          avgTravelTime: round(row.avgTravelTime || 0, 1),
          specialization: (row.focusTypes || []).slice(0, 3).join(", ")
        })),
        recentJobs: recentJobs.map((row) => ({
          id: row.workOrderId || `${row.city}-${row.area}-${row.timestamp}`,
          workOrderId: row.workOrderId,
          customerName: row.customerName,
          serviceType: row.serviceType,
          status: row.status,
          technicianId: row.technicianId,
          timestamp: row.timestamp,
          travelTimeMinutes: round(row.travelTimeMinutes || 0, 1)
        })),
        trend: trend.map((row) => ({
          bucket: row._id.bucket,
          totalJobs: row.totalJobs,
          breakdowns: row.breakdowns,
          avgTravelTime: round(row.avgTravelTime || 0, 1)
        }))
      }
    });
  } catch (error) {
    res.status(error.statusCode || 500).json({
      ok: false,
      error: error.message
    });
  }
}

module.exports = {
  getHeatmapData,
  getZoneInsights
};
