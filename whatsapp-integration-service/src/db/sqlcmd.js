const { execFile } = require("child_process");
const { promisify } = require("util");
const { db } = require("../config");

const execFileAsync = promisify(execFile);

function quoteSql(value) {
  if (value == null) {
    return "NULL";
  }
  if (value instanceof Date && !Number.isNaN(value.getTime())) {
    return `CONVERT(DATETIME, '${value.toISOString().replace("T", " ").slice(0, 19)}', 120)`;
  }
  if (typeof value === "number" && Number.isFinite(value)) {
    return String(value);
  }
  if (typeof value === "boolean") {
    return value ? "1" : "0";
  }
  return `N'${String(value).replace(/'/g, "''")}'`;
}

function buildArgs(query) {
  return [
    "-S",
    db.instance,
    "-d",
    db.database,
    "-E",
    "-b",
    "-y",
    "0",
    "-w",
    "65535",
    "-Q",
    `SET NOCOUNT ON; ${query}`
  ];
}

function extractFirstJsonBlock(text) {
  const start = Math.min(
    ...["{", "["]
      .map((token) => text.indexOf(token))
      .filter((index) => index >= 0)
  );

  if (!Number.isFinite(start)) {
    return null;
  }

  const open = text[start];
  const close = open === "{" ? "}" : "]";
  const stack = [];
  let inString = false;
  let escaped = false;

  for (let i = start; i < text.length; i++) {
    const ch = text[i];

    if (inString) {
      if (escaped) {
        escaped = false;
      } else if (ch === "\\") {
        escaped = true;
      } else if (ch === "\"") {
        inString = false;
      }
      continue;
    }

    if (ch === "\"") {
      inString = true;
      continue;
    }

    if (ch === "{" || ch === "[") {
      stack.push(ch);
      continue;
    }

    if (ch === "}" || ch === "]") {
      const last = stack.pop();
      if (!last) {
        return null;
      }

      const expected = last === "{" ? "}" : "]";
      if (ch !== expected) {
        return null;
      }

      if (stack.length === 0 && ch === close && start === 0) {
        return text.slice(start, i + 1);
      }

      if (stack.length === 0) {
        return text.slice(start, i + 1);
      }
    }
  }

  return null;
}

async function runSql(query) {
  const executable = process.env.SQLCMD_PATH || "sqlcmd";
  const { stdout } = await execFileAsync(executable, buildArgs(query), {
    windowsHide: true,
    maxBuffer: 1024 * 1024 * 25
  });
  return String(stdout || "").trim();
}

async function queryJson(query) {
  const output = await runSql(query);
  if (!output) {
    return null;
  }

  const trimmed = output.trim();
  if (!trimmed) {
    return null;
  }

  try {
    return JSON.parse(trimmed);
  } catch {
    const firstBlock = extractFirstJsonBlock(trimmed);
    if (firstBlock) {
      return JSON.parse(firstBlock);
    }
    throw new Error(`sqlcmd JSON parse failed: ${trimmed.slice(0, 400)}`);
  }
}

async function queryScalar(query) {
  const output = await runSql(query);
  if (!output) {
    return null;
  }

  const lines = output.split(/\r?\n/).map((line) => line.trim()).filter(Boolean);
  if (lines.length === 0) {
    return null;
  }

  const last = lines[lines.length - 1];
  if (last === "NULL") {
    return null;
  }

  if (/^-?\d+(?:\.\d+)?$/.test(last)) {
    return Number(last);
  }

  return last;
}

module.exports = {
  queryJson,
  queryScalar,
  quoteSql,
  runSql
};
