#!/usr/bin/env node
import { createReadStream, statSync } from "node:fs";
import { basename, resolve } from "node:path";

const args = parseArgs(process.argv.slice(2));
const required = ["repo", "tag", "name", "artifacts"];
for (const key of required) {
  if (!args[key]) {
    fail(`Missing --${key}.`);
  }
}

const token = args.token || process.env.GITHUB_TOKEN || process.env.GH_TOKEN;
if (!token) {
  fail("Missing GitHub token. Set GITHUB_TOKEN or pass --token from a trusted shell.");
}

const [owner, repoName] = args.repo.split("/");
if (!owner || !repoName) {
  fail("--repo must be owner/name, for example harshals499/ServoERP.");
}

const artifactPaths = args.artifacts
  .split(";")
  .map((item) => item.trim())
  .filter(Boolean)
  .map((item) => resolve(item));

if (artifactPaths.length === 0) {
  fail("No artifacts were provided.");
}

const apiBase = "https://api.github.com";
const commonHeaders = {
  Authorization: `Bearer ${token}`,
  Accept: "application/vnd.github+json",
  "X-GitHub-Api-Version": "2022-11-28",
  "User-Agent": "ServoERP-Release-Node-Uploader",
};

const release = await getOrCreateRelease();
console.log(`Release ready: ${release.html_url}`);

const existingAssets = new Map((release.assets || []).map((asset) => [asset.name, asset]));
for (const artifactPath of artifactPaths) {
  const name = basename(artifactPath);
  if (existingAssets.has(name)) {
    const asset = existingAssets.get(name);
    await githubFetch(asset.url, { method: "DELETE" });
    console.log(`Replaced existing asset: ${name}`);
  }

  const size = statSync(artifactPath).size;
  const uploadUrl = release.upload_url.replace(/\{.*$/, "");
  const url = `${uploadUrl}?name=${encodeURIComponent(name)}`;
  await githubFetch(url, {
    method: "POST",
    headers: {
      "Content-Type": "application/octet-stream",
      "Content-Length": String(size),
    },
    body: createReadStream(artifactPath),
    duplex: "half",
  });
  console.log(`Uploaded: ${name}`);
}

console.log("GitHub release upload completed.");

async function getOrCreateRelease() {
  const encodedTag = encodeURIComponent(args.tag);
  const byTagUrl = `${apiBase}/repos/${owner}/${repoName}/releases/tags/${encodedTag}`;
  const existing = await githubFetch(byTagUrl, { allow404: true });
  if (existing) {
    return existing;
  }

  const body = args.body || `ServoERP client update ${args.tag}`;
  return await githubFetch(`${apiBase}/repos/${owner}/${repoName}/releases`, {
    method: "POST",
    json: {
      tag_name: args.tag,
      target_commitish: args.target || "main",
      name: args.name,
      body,
      draft: args.draft === "true",
      prerelease: args.prerelease === "true",
    },
  });
}

async function githubFetch(url, options = {}) {
  const headers = { ...commonHeaders, ...(options.headers || {}) };
  let body = options.body;
  if (options.json) {
    headers["Content-Type"] = "application/json";
    body = JSON.stringify(options.json);
  }

  const response = await fetch(url, {
    method: options.method || "GET",
    headers,
    body,
    duplex: options.duplex,
  });

  if (options.allow404 && response.status === 404) {
    return null;
  }

  if (!response.ok) {
    const text = await response.text();
    fail(`GitHub API ${response.status} ${response.statusText}: ${text}`);
  }

  if (response.status === 204) {
    return {};
  }

  return await response.json();
}

function parseArgs(items) {
  const parsed = {};
  for (let i = 0; i < items.length; i++) {
    const item = items[i];
    if (!item.startsWith("--")) {
      continue;
    }

    const key = item.slice(2);
    const next = items[i + 1];
    if (!next || next.startsWith("--")) {
      parsed[key] = "true";
      continue;
    }

    parsed[key] = next;
    i++;
  }

  return parsed;
}

function fail(message) {
  console.error(message);
  process.exit(1);
}
