import { createReadStream, existsSync, mkdirSync } from "node:fs";
import { createServer } from "node:http";
import { extname, join, normalize } from "node:path";
import { DatabaseSync } from "node:sqlite";
import { fileURLToPath } from "node:url";
import nodemailer from "nodemailer";

import { createDemoRequestService } from "./demoRequest.js";
import { DemoRequestRepository } from "./demoRequestRepository.js";

const rootDir = normalize(join(fileURLToPath(import.meta.url), "..", ".."));
const dataDir = process.env.DEMO_REQUEST_DATA_DIR || join(rootDir, "data");
const databasePath = process.env.DEMO_REQUEST_DB_PATH || join(dataDir, "demo-requests.sqlite");
const port = Number(process.env.PORT || 3000);

mkdirSync(dataDir, { recursive: true });

const contentTypes = {
  ".css": "text/css; charset=utf-8",
  ".html": "text/html; charset=utf-8",
  ".js": "text/javascript; charset=utf-8",
  ".json": "application/json; charset=utf-8",
  ".png": "image/png",
  ".jpg": "image/jpeg",
  ".jpeg": "image/jpeg",
  ".svg": "image/svg+xml",
  ".txt": "text/plain; charset=utf-8",
};

function createMailer() {
  if (!process.env.SMTP_HOST) {
    if (process.env.DEMO_REQUEST_ALLOW_STUB_EMAIL !== "true") {
      return async () => {
        throw new Error("SMTP_HOST is not configured.");
      };
    }

    return async (message) => {
      console.warn("SMTP_HOST is not configured. Demo request email was not sent.", message.subject);
      return { messageId: "smtp-not-configured" };
    };
  }

  const transporter = nodemailer.createTransport({
    host: process.env.SMTP_HOST,
    port: Number(process.env.SMTP_PORT || 587),
    secure: process.env.SMTP_SECURE === "true",
    auth:
      process.env.SMTP_USER && process.env.SMTP_PASS
        ? { user: process.env.SMTP_USER, pass: process.env.SMTP_PASS }
        : undefined,
  });

  return (message) =>
    transporter.sendMail({
      from: process.env.DEMO_REQUEST_FROM_EMAIL || process.env.SMTP_USER,
      ...message,
    });
}

const database = new DatabaseSync(databasePath);
const repository = new DemoRequestRepository(database);
const service = createDemoRequestService({
  repository,
  sendMail: createMailer(),
});

function sendJson(response, statusCode, body) {
  response.writeHead(statusCode, {
    "Content-Type": "application/json; charset=utf-8",
    "Cache-Control": "no-store",
  });
  response.end(JSON.stringify(body));
}

async function readJson(request) {
  let body = "";
  for await (const chunk of request) {
    body += chunk;
    if (body.length > 32_000) {
      throw new Error("Request body is too large.");
    }
  }
  return body ? JSON.parse(body) : {};
}

function serveStatic(request, response) {
  const url = new URL(request.url, `http://${request.headers.host}`);
  const requestedPath = url.pathname === "/" ? "/index.html" : url.pathname;
  const resolvedPath = normalize(join(rootDir, requestedPath));

  if (!resolvedPath.startsWith(rootDir) || !existsSync(resolvedPath)) {
    response.writeHead(404, { "Content-Type": "text/html; charset=utf-8" });
    createReadStream(join(rootDir, "404.html")).pipe(response);
    return;
  }

  response.writeHead(200, {
    "Content-Type": contentTypes[extname(resolvedPath)] || "application/octet-stream",
  });
  createReadStream(resolvedPath).pipe(response);
}

const server = createServer(async (request, response) => {
  const url = new URL(request.url, `http://${request.headers.host}`);

  if (url.pathname === "/api/demo-request") {
    if (request.method !== "POST") {
      sendJson(response, 405, { ok: false, error: "Method not allowed." });
      return;
    }

    try {
      const payload = await readJson(request);
      const result = await service.submit(payload, {
        ipAddress: request.socket.remoteAddress,
        userAgent: request.headers["user-agent"],
      });
      sendJson(response, result.statusCode, result.ok ? result : result);
    } catch (error) {
      sendJson(response, 500, {
        ok: false,
        error: "We could not submit your request right now. Please try again shortly.",
      });
    }
    return;
  }

  if (request.method !== "GET" && request.method !== "HEAD") {
    sendJson(response, 405, { ok: false, error: "Method not allowed." });
    return;
  }

  serveStatic(request, response);
});

server.listen(port, () => {
  console.log(`ServoERP marketing site running at http://localhost:${port}`);
});
