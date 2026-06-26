# SWG Unified Launchpad

One launcher for every Star Wars Galaxies server. Built for the r/swg community to unify a player base scattered across per-server launchers.

Server operators host two small JSON files (a server manifest and a file manifest); players get a single launcher with a moderated server list where each server installs into its own isolated game folder with the full capability of its original launcher:  hash-verified patching, login redirection, per-server settings, live online status and player counts, news headlines, and multi-instance play. Players can also add any unlisted server themselves by pasting its manifest URL ("Add server" button).

## How it fits together

The r/swg moderators host `registry.json`:  the approved server list, each entry pointing at a manifest URL the server operator controls. The launcher fetches the registry, then each manifest, and renders the server list. Installing a server hard-links a shared pristine SWG 14.1 client into `servers\<serverId>\` (near-zero extra disk), downloads the server's custom files with SHA-256 verification, and writes `login.cfg` to point the client at that server. Full details in `docs/ARCHITECTURE.md`; the operator-facing guide is `docs/SERVER_SUBMISSION.md`.

## Building the .exe

Requires the .NET 8 SDK (free, from Microsoft) on Windows. Then double-click:

```
src\publish.cmd
```

That produces `src\publish\SwgLaunchpad.exe`:  a single self-contained file.

For development builds: `cd src && dotnet run --project SwgLaunchpad.App`.

Using Visual Studio 2026? Opening `src\SwgLaunchpad.sln` will detect `src\.vsconfig` and prompt to install any missing components (the .NET desktop workload, .NET 8 runtime, Git, XAML designer tools) automatically.

## First run

Point "Base SWG client" at a clean SWG 14.1 install and click "Use this folder" (one-time import; the launcher checks it actually contains .tre files), then install any server from the list. The registry source defaults to the r/swg GitHub URL:  change `LauncherSettings.DefaultRegistrySource` in Core (or the settings.json under `%LOCALAPPDATA%\SwgLaunchpad\state`) to point at your registry.

## Repository layout

```
docs\ARCHITECTURE.md          design: manifest schemas, patching protocol, folder strategy
docs\SERVER_SUBMISSION.md     guide for server operators (includes Add-server sharing)
registry\registry.json        the moderated server list (host this on GitHub)
registry\examples\            example server manifest + file manifest
src\SwgLaunchpad.Core\        all launcher logic (net8.0 class library)
src\SwgLaunchpad.App\         WPF desktop app (net8.0-windows), themed assets in Assets\
src\publish.cmd               one-click single-file .exe build
tools\generate_file_manifest.py   operators run this to publish a patch
```

## Default servers and branding

The launcher ships with every active project from the community server list (SWGEmu Finalizer, Awakening, Stardust, Empire in Flames, Infinity, mySWG, Reckoning, Sunrunner II, Attack of the Clones, A New Hope, Flurry, SWGReturns, EisleyEmu, CUEmu, Project SWG, SWG Legends, Restoration III) as registry stubs in `registry/manifests/`. Stub entries carry real names, eras, websites, and Discords; operators claim theirs by filling in login host/port and patch URLs via PR. The registry is bundled into the app as an offline fallback, so the full roster shows even before the GitHub registry goes live.

Servers with launcher accounts (Legends-style) get an Account dialog: credentials are DPAPI-encrypted per server, validated against the server's login API when offered, and injected via `{username}`/`{password}` argument placeholders.

## Status

Working prototype, three polish passes in. Implemented: registry + manifest fetch and validation; hash-cached verify/patch with parallel downloads, speed display, cancel, and atomic file replacement; hard-linked per-server folders; login.cfg redirection; options.cfg settings editor; multi-instance launch with per-server opt-out; user-added custom servers with trust labeling; live online/offline status, player counts (`statusUrl`), and news headlines (`newsUrl`) refreshed every 60 s; era filter; single-launcher mutex; window-state memory; r/swg-styled theme, icon, and banner. Not yet: launcher self-update, manifest signing, base-client download for new players, mod manager.
