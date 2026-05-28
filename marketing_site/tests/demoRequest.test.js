import assert from "node:assert/strict";
import test from "node:test";
import { DatabaseSync } from "node:sqlite";

import {
  createDemoRequestService,
  validateDemoRequest,
} from "../src/demoRequest.js";
import { DemoRequestRepository } from "../src/demoRequestRepository.js";

const validPayload = {
  name: "Asha Mehta",
  companyName: "ABC Cooling Solutions Pvt. Ltd.",
  email: "asha@example.com",
  phoneNumber: "+91 98765 43210",
  businessType: "HVAC",
  message: "We manage 250 AMCs and need dispatch, GST invoicing, and inventory.",
};

test("validates and normalizes required demo request fields", () => {
  const result = validateDemoRequest({
    ...validPayload,
    name: "  Asha Mehta  ",
    companyName: "  ABC Cooling Solutions Pvt. Ltd.  ",
    email: " ASHA@EXAMPLE.COM ",
    website: "",
  });

  assert.equal(result.ok, true);
  assert.equal(result.value.name, "Asha Mehta");
  assert.equal(result.value.companyName, "ABC Cooling Solutions Pvt. Ltd.");
  assert.equal(result.value.email, "asha@example.com");
  assert.equal(result.value.status, "Pending");
});

test("rejects missing required fields, invalid email, and honeypot spam", () => {
  assert.equal(validateDemoRequest({ ...validPayload, email: "bad" }).ok, false);
  assert.equal(validateDemoRequest({ ...validPayload, name: "" }).ok, false);
  assert.equal(validateDemoRequest({ ...validPayload, website: "https://spam.test" }).ok, false);
});

test("persists demo requests with timestamp and Pending status", () => {
  const database = new DatabaseSync(":memory:");
  const repository = new DemoRequestRepository(database);

  const saved = repository.save({
    ...validPayload,
    status: "Pending",
    source: "website",
    ipAddress: "127.0.0.1",
    userAgent: "node-test",
  });

  assert.equal(saved.id, 1);
  assert.equal(saved.status, "Pending");
  assert.match(saved.createdAt, /^\d{4}-\d{2}-\d{2}T/);

  const rows = repository.list();
  assert.equal(rows.length, 1);
  assert.equal(rows[0].companyName, validPayload.companyName);
  assert.equal(rows[0].status, "Pending");
});

test("submits a valid request, saves it, and sends admin plus customer emails", async () => {
  const database = new DatabaseSync(":memory:");
  const repository = new DemoRequestRepository(database);
  const sentMessages = [];
  const service = createDemoRequestService({
    repository,
    sendMail: async (message) => {
      sentMessages.push(message);
      return { messageId: `test-${sentMessages.length}` };
    },
  });

  const result = await service.submit(validPayload, {
    ipAddress: "127.0.0.1",
    userAgent: "node-test",
  });

  assert.equal(result.ok, true);
  assert.equal(result.message, "Thank you! Our team will contact you shortly.");
  assert.equal(repository.list().length, 1);
  assert.equal(sentMessages.length, 2);
  assert.equal(sentMessages[0].to, "support@servoerp.in");
  assert.equal(sentMessages[1].to, validPayload.email);
});
