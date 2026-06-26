# Listing your server on the SWG Unified Launchpad

Getting your server into the launcher takes three things: host two JSON files, then submit one registry entry. Your players keep every capability of your current launcher:  patching, login redirection, settings:  through one shared launcher.

## 1. Host your server manifest

Create a `manifest.json` at a stable HTTPS URL you control (your patch server, your website, even a GitHub repo). Template:

```json
{
  "manifestVersion": 1,
  "serverId": "your-server-id",
  "name": "Your Server Name",
  "description": "One or two sentences shown in the server list.",
  "era": "Pre-CU",
  "website": "https://yourserver.org",
  "discord": "https://discord.gg/yourserver",
  "bannerUrl": "https://patch.yourserver.org/banner.png",
  "newsUrl": "https://yourserver.org/news.rss",
  "statusUrl": "https://login.yourserver.org/status.xml",
  "login": { "host": "login.yourserver.org", "port": 44453 },
  "client": {
    "executable": "SWGEmu.exe",
    "arguments": "",
    "allowMultiInstance": true
  },
  "files": {
    "baseUrl": "https://patch.yourserver.org/files/",
    "fileManifestUrl": "https://patch.yourserver.org/files.json"
  }
}
```

Rules: `serverId` is permanent (lowercase letters, digits, hyphens):  it names your players' install folder, so pick it once. All URLs must be HTTPS. `era` should be one of `Pre-CU`, `CU`, `NGE`, `Hybrid`. Set `allowMultiInstance` to `false` if your rules forbid dual-boxing; the launcher will enforce it.

Accounts: if players log in at the in-game login screen (SWGEmu style), omit `auth` or use `"auth": { "mode": "none" }`. If your launcher collects credentials (Legends style), use:

```json
"auth": {
  "mode": "launcher",
  "registerUrl": "https://yourserver.org/register",
  "passwordResetUrl": "https://yourserver.org/reset",
  "loginUrl": "https://yourserver.org/api/login"
}
```

The launcher shows an Account dialog, stores credentials DPAPI-encrypted on the player's PC, optionally validates them against `loginUrl` (POST form `username`/`password`, any 2xx = valid), and substitutes `{username}`/`{password}` placeholders in `client.arguments` at launch if your client accepts credential arguments.

Already listed as a default? The Launchpad ships with a stub entry for every active project from the community server list. Find yours in `registry/manifests/`, fill in your real login host/port and patch URLs, and open a PR to claim it.

Optional but recommended: `statusUrl` powers the live player count on your server card. Point it at anything that returns your population:  SWGEmu-style status XML (any element with a `connected`/`count` attribute or named `online`/`population`/`users`) or plain JSON like `{ "online": 142 }`. `newsUrl` (RSS or Atom) shows your latest headline on the card. Both fail silently if unreachable:  they never block patching.

## 2. Host your file manifest

`files.json` lists every file your server adds to or changes from the stock SWG 14.1 client:  your .tre files, your client executable, your configs. Players download each file from `baseUrl + path`.

```json
{
  "generated": "2026-06-12T00:00:00Z",
  "files": [
    { "path": "yourserver_main.tre", "size": 48211344, "sha256": "9f86d081884c7d65..." }
  ]
}
```

Don't write this by hand:  run the generator against your patch directory:

```
python tools/generate_file_manifest.py "C:\path\to\your\patch\files" > files.json
```

Regenerate and re-upload `files.json` every time you patch. That's your entire release process; players get the update on next launch. Paths are relative, forward slashes, no `..`:  the launcher rejects manifests that try to write outside the install folder.

## 3. Submit to the registry

Open a pull request (or message the r/swg mods) adding one entry to `registry.json`:

```json
{ "serverId": "your-server-id", "name": "Your Server Name", "manifestUrl": "https://patch.yourserver.org/manifest.json", "status": "verified" }
```

Moderators verify the manifest loads, the hashes check out, and the client connects, then merge. You never need to touch the registry again:  all future updates happen through the two files you host.

Not listed yet, or running a private server? Players can still reach you today: the launcher's "Add server" button accepts your manifest URL directly. Share it in your Discord:  anyone who pastes it gets your server in their list immediately, marked "custom". Registry listing just makes you visible to everyone by default.

## What players experience

They pick your server in the launcher; it creates an isolated folder for your server (stock client files are hard-linked from a shared base install, so your server costs them only your custom files), downloads your files with hash verification, writes `login.cfg` pointing at your login server, and launches your executable from that folder. Settings are per-server. Other servers' files can never collide with yours.

## Delisting

Registry entries can be removed by the moderators at any time (broken manifests, malicious files, community standards). Removal only hides the server from the list:  players' installed folders are untouched.
