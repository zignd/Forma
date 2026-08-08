#!/usr/bin/env node

import { cp, mkdir, readdir, readFile, rm, writeFile } from "node:fs/promises";
import path from "node:path";

const options = new Map();
for (let index = 2; index < process.argv.length; index += 2) {
  options.set(process.argv[index], process.argv[index + 1]);
}

const siteRoot = options.get("--site");
const pagesRoot = options.get("--pages");
const channel = options.get("--channel");
const version = options.get("--version");
const maturity = options.get("--maturity");
const revision = options.get("--revision");

if (!siteRoot || !pagesRoot || !channel || !version || !maturity || !revision) {
  throw new Error(
    "Usage: stage-docs-pages.mjs --site <dir> --pages <dir> --channel <dev|vSemVer> " +
      "--version <version> --maturity <label> --revision <commit>",
  );
}

const semverPattern = /^v?(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)(-[0-9A-Za-z]+(?:[.-][0-9A-Za-z]+)*)?(?:\+[0-9A-Za-z]+(?:[.-][0-9A-Za-z]+)*)?$/;
if (channel !== "dev" && !semverPattern.test(channel)) {
  throw new Error(`Documentation channel must be dev or vSemVer: ${channel}`);
}
if (channel !== "dev" && channel !== `v${version}`) {
  throw new Error(`Release channel ${channel} does not match version ${version}.`);
}
if (!/^[0-9a-f]{40}$/.test(revision)) {
  throw new Error(`Documentation revision must be a full Git commit: ${revision}`);
}

const releasePattern = /^v(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)(?:-[0-9A-Za-z]+(?:[.-][0-9A-Za-z]+)*)?(?:\+[0-9A-Za-z]+(?:[.-][0-9A-Za-z]+)*)?$/;
const parseVersion = (value) => {
  const [coreAndPrerelease] = value.slice(1).split("+");
  const [core, prerelease = ""] = coreAndPrerelease.split("-");
  return { core: core.split(".").map(Number), prerelease };
};
const compareVersions = (left, right) => {
  const leftVersion = parseVersion(left);
  const rightVersion = parseVersion(right);
  for (let index = 0; index < 3; index += 1) {
    if (leftVersion.core[index] !== rightVersion.core[index]) {
      return rightVersion.core[index] - leftVersion.core[index];
    }
  }
  if (!leftVersion.prerelease && rightVersion.prerelease) return -1;
  if (leftVersion.prerelease && !rightVersion.prerelease) return 1;
  return rightVersion.prerelease.localeCompare(leftVersion.prerelease, undefined, { numeric: true });
};
const escapeHtml = (value) =>
  value.replaceAll("&", "&amp;").replaceAll("<", "&lt;").replaceAll(">", "&gt;").replaceAll('"', "&quot;");

await mkdir(pagesRoot, { recursive: true });
const targetRoot = path.join(pagesRoot, channel);
const metadataPath = path.join(targetRoot, "site-version.json");
if (channel !== "dev") {
  try {
    const existing = JSON.parse(await readFile(metadataPath, "utf8"));
    if (existing.revision !== revision) {
      throw new Error(
        `Immutable documentation channel ${channel} already belongs to ${existing.revision}.`,
      );
    }
  } catch (error) {
    if (error.code !== "ENOENT") throw error;
  }
}

await rm(targetRoot, { recursive: true, force: true });
await cp(siteRoot, targetRoot, { recursive: true });
await writeFile(
  metadataPath,
  `${JSON.stringify({ schemaVersion: 1, channel, version, maturity, revision }, null, 2)}\n`,
);

const rootEntries = await readdir(pagesRoot, { withFileTypes: true });
for (const entry of rootEntries) {
  const preserved =
    entry.name === ".git" ||
    entry.name === "CNAME" ||
    entry.name === "dev" ||
    (entry.isDirectory() && releasePattern.test(entry.name));
  if (!preserved) await rm(path.join(pagesRoot, entry.name), { recursive: true, force: true });
}

const releaseDirectories = (await readdir(pagesRoot, { withFileTypes: true }))
  .filter((entry) => entry.isDirectory() && releasePattern.test(entry.name))
  .map((entry) => entry.name)
  .sort(compareVersions);
const releases = [];
for (const releaseChannel of releaseDirectories) {
  const metadata = JSON.parse(
    await readFile(path.join(pagesRoot, releaseChannel, "site-version.json"), "utf8"),
  );
  releases.push({
    version: metadata.version,
    channel: releaseChannel,
    maturity: metadata.maturity,
    revision: metadata.revision,
    path: `/Forma/${releaseChannel}/`,
  });
}

let development = null;
try {
  const metadata = JSON.parse(await readFile(path.join(pagesRoot, "dev/site-version.json"), "utf8"));
  development = {
    version: metadata.version,
    maturity: metadata.maturity,
    revision: metadata.revision,
    path: "/Forma/dev/",
  };
} catch (error) {
  if (error.code !== "ENOENT") throw error;
}

const newestRelease = releases[0];
if (!newestRelease && !development) {
  throw new Error("The staged site has neither a release nor development channel.");
}
const redirectPage = (target, title) => `<!doctype html>
<html lang="en"><head><meta charset="utf-8"><meta http-equiv="refresh" content="0; url=${target}">
<link rel="canonical" href="${target}"><title>${escapeHtml(title)}</title></head>
<body><p><a href="${target}">Continue to ${escapeHtml(title)}</a></p></body></html>
`;

// `latest/` mirrors the newest release in full so unversioned deep links resolve and stay stable.
await rm(path.join(pagesRoot, "latest"), { recursive: true, force: true });
if (newestRelease) {
  await cp(path.join(pagesRoot, newestRelease.channel), path.join(pagesRoot, "latest"), { recursive: true });
} else {
  await mkdir(path.join(pagesRoot, "latest"), { recursive: true });
  await writeFile(path.join(pagesRoot, "latest/index.html"), redirectPage(development.path, "development Forma documentation"));
}
const defaultPath = newestRelease ? "/Forma/latest/" : development.path;
await writeFile(path.join(pagesRoot, "index.html"), redirectPage(defaultPath, "Forma documentation"));
await writeFile(path.join(pagesRoot, ".nojekyll"), "");
await writeFile(
  path.join(pagesRoot, "versions.json"),
  `${JSON.stringify({ schemaVersion: 1, defaultPath, development, releases }, null, 2)}\n`,
);

await mkdir(path.join(pagesRoot, "versions"), { recursive: true });
const releaseItems = releases.length
  ? releases
      .map(
        (release) =>
          `<li><a href="${release.path}">Forma ${escapeHtml(release.version)}</a> ` +
          `(${escapeHtml(release.maturity)})</li>`,
      )
      .join("\n")
  : "<li>No public release documentation has been staged yet.</li>";
const developmentItem = development
  ? `<p><a href="${development.path}">Development preview ${escapeHtml(development.version)}</a></p>`
  : "";
await writeFile(
  path.join(pagesRoot, "versions/index.html"),
  `<!doctype html>
<html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width">
<title>Forma documentation versions</title></head><body><main>
<h1>Forma documentation versions</h1>${developmentItem}<h2>Releases</h2><ul>${releaseItems}</ul>
</main></body></html>
`,
);

await writeFile(
  path.join(pagesRoot, "404.html"),
  `<!doctype html><html lang="en"><head><meta charset="utf-8"><title>Forma documentation redirect</title></head>
<body><p>Resolving this documentation URL...</p><script>
fetch('/Forma/versions.json').then(response => response.json()).then(manifest => {
  const relative = location.pathname.replace(/^\\/Forma\\/?/, '');
  const known = /^(dev|latest|versions|v\\d+\\.\\d+\\.\\d+(?:-[0-9A-Za-z.-]+)?)\\//.test(relative);
  if (!known) location.replace(manifest.defaultPath + relative + location.search + location.hash);
});
</script></body></html>
`,
);

console.log(`Staged Forma documentation channel ${channel}; default path is ${defaultPath}.`);