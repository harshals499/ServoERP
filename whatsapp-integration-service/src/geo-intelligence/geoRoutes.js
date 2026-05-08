const express = require("express");
const { getHeatmapData, getZoneInsights } = require("./GeoController");

function createGeoRouter() {
  const router = express.Router();

  router.get("/geo/heatmap-data", getHeatmapData);
  router.get("/geo/zone-insights", getZoneInsights);

  return router;
}

module.exports = {
  createGeoRouter
};
