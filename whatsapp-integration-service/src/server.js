const http = require("http");
const express = require("express");
const { Server } = require("socket.io");
const { port, socket } = require("./config");
const { createWhatsappRouter } = require("./routes/whatsappWebhook");
const { createPurchaseOrderRouter } = require("./purchase-orders/poRouter");
const { createGeoRouter } = require("./geo-intelligence/geoRoutes");
const liveBridge = require("./liveBridge");
const { sendWhatsAppTemplate } = require("./services/notificationDispatcher");

function createServer() {
  const app = express();

  app.use(
    express.json({
      verify: (req, res, buf) => {
        req.rawBody = buf.toString("utf8");
      }
    })
  );

  app.get("/health", (req, res) => {
    res.json({ ok: true, service: "whatsapp-integration-service" });
  });

  app.use(createWhatsappRouter());
  app.use(createPurchaseOrderRouter());
  app.use(createGeoRouter());

  app.post("/api/whatsapp/send-template", async (req, res) => {
    try {
      const { to, templateName, components, languageCode } = req.body || {};
      const result = await sendWhatsAppTemplate(to, templateName, components, languageCode);
      res.status(200).json({ ok: true, result });
    } catch (error) {
      res.status(500).json({ ok: false, error: error.message });
    }
  });

  const server = http.createServer(app);
  const io = new Server(server, {
    cors: {
      origin: socket.corsOrigin
    }
  });

  io.on("connection", (client) => {
    client.emit("CONNECTED", {
      ok: true,
      service: "whatsapp-integration-service"
    });
  });

  liveBridge.attach(io);
  return { app, server, io };
}

if (require.main === module) {
  const { server } = createServer();
  server.listen(port, () => {
    console.log(`WhatsApp integration service listening on port ${port}`);
  });
}

module.exports = {
  createServer
};
