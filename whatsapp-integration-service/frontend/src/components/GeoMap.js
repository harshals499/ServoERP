import React, { useEffect, useMemo } from "react";
import L from "leaflet";
import { CircleMarker, MapContainer, Popup, TileLayer, useMap } from "react-leaflet";
import "leaflet/dist/leaflet.css";
import "leaflet.heat";

const INDIA_CENTER = [20.5937, 78.9629];

function HeatLayer({ points }) {
  const map = useMap();

  useEffect(() => {
    if (!map) {
      return undefined;
    }

    const heatPoints = (points || []).map((point) => [point.lat, point.lng, point.intensity || 0.35]);
    const layer = L.heatLayer(heatPoints, {
      radius: 28,
      blur: 24,
      maxZoom: 12,
      minOpacity: 0.35,
      gradient: {
        0.2: "#38bdf8",
        0.45: "#facc15",
        0.7: "#fb923c",
        1.0: "#ef4444"
      }
    }).addTo(map);

    return () => {
      map.removeLayer(layer);
    };
  }, [map, points]);

  return null;
}

function FitBounds({ points, selectedZoneId }) {
  const map = useMap();

  useEffect(() => {
    if (!map || !points?.length) {
      return;
    }

    const selected = points.find((point) => point.zoneId === selectedZoneId);
    if (selected) {
      map.flyTo([selected.lat, selected.lng], Math.max(map.getZoom(), 12), {
        duration: 0.6
      });
      return;
    }

    const bounds = L.latLngBounds(points.map((point) => [point.lat, point.lng]));
    map.fitBounds(bounds.pad(0.2), { animate: false });
  }, [map, points, selectedZoneId]);

  return null;
}

function markerStyle(point, selectedZoneId) {
  if (point.zoneId === selectedZoneId) {
    return {
      color: "#0f172a",
      fillColor: "#38bdf8",
      fillOpacity: 0.95,
      weight: 3
    };
  }

  if (point.demandScore >= 80) {
    return {
      color: "#7f1d1d",
      fillColor: "#ef4444",
      fillOpacity: 0.9,
      weight: 2
    };
  }

  if (point.demandScore >= 60) {
    return {
      color: "#78350f",
      fillColor: "#f59e0b",
      fillOpacity: 0.85,
      weight: 2
    };
  }

  return {
    color: "#1e3a8a",
    fillColor: "#3b82f6",
    fillOpacity: 0.8,
    weight: 2
  };
}

export default function GeoMap({ points = [], selectedZoneId, onZoneSelect, center }) {
  const mapCenter = useMemo(() => {
    if (center?.lat && center?.lng) {
      return [center.lat, center.lng];
    }
    if (points.length > 0) {
      return [points[0].lat, points[0].lng];
    }
    return INDIA_CENTER;
  }, [center, points]);

  return (
    <div className="relative h-[540px] overflow-hidden rounded-2xl border border-slate-800 bg-slate-900 shadow-2xl">
      <MapContainer center={mapCenter} zoom={5} zoomControl={false} className="h-full w-full">
        <TileLayer
          attribution='&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors'
          url="https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png"
        />
        <HeatLayer points={points} />
        <FitBounds points={points} selectedZoneId={selectedZoneId} />

        {points.map((point) => (
          <CircleMarker
            key={point.zoneId}
            center={[point.lat, point.lng]}
            radius={Math.max(12, Math.min(28, 10 + (point.jobCount || 0)))}
            pathOptions={markerStyle(point, selectedZoneId)}
            eventHandlers={{
              click: () => onZoneSelect?.(point.zoneId)
            }}
          >
            <Popup>
              <div className="min-w-[220px]">
                <div className="text-sm font-semibold text-slate-900">{point.label}</div>
                <div className="mt-2 text-xs text-slate-600">Demand score: {point.demandScore} ({point.demandLevel})</div>
                <div className="mt-1 text-xs text-slate-600">Jobs: {point.jobCount}</div>
                <div className="mt-1 text-xs text-slate-600">Breakdown rate: {point.breakdownRate}%</div>
                <button
                  type="button"
                  className="mt-3 rounded-lg bg-sky-600 px-3 py-2 text-xs font-medium text-white"
                  onClick={() => onZoneSelect?.(point.zoneId)}
                >
                  View zone analysis
                </button>
              </div>
            </Popup>
          </CircleMarker>
        ))}
      </MapContainer>

      <div className="pointer-events-none absolute left-4 top-4 rounded-xl bg-slate-950/80 px-4 py-3 text-xs text-slate-200 shadow-lg ring-1 ring-white/10">
        <div className="font-semibold text-white">Service Density Heatmap</div>
        <div className="mt-1">Click a zone marker to load in-depth analysis.</div>
      </div>
    </div>
  );
}
