const { EventEmitter } = require("events");

class LiveBridge extends EventEmitter {
  attach(io) {
    this.on("WHATSAPP_UPDATE", (payload) => {
      io.emit("WHATSAPP_UPDATE", payload);
    });
  }
}

module.exports = new LiveBridge();
