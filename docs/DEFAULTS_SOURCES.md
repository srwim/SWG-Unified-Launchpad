# Default manifest data:  what's verified, what isn't

The bundled manifests in `registry/manifests/` ship with as much real data as is publicly verifiable without operator involvement. This file records where each fact came from so moderators can re-verify or correct via PR. Optional fields (`newsUrl`, `statusUrl`) fail silently in the launcher, so convention-based values are safe; `login` and `files` values are load-bearing, so unverified ones are flagged in each manifest's description.

## Verified (with source)

SWGEmu Finalizer: login host `login.swgemu.com:44453`:  stable for over a decade, documented across [SWGEmu's own forums](https://www.swgemu.com/forums/showthread.php?t=85484); port range 44450–44465 TCP/UDP per [SWG-Source docs](https://github.com/SWG-Source/swg-main/wiki/Enabling-External-Access-To-Your-Server). News feed is the standard vBulletin RSS endpoint.

SWG Awakening: Discord (`discord.gg/QxQSXwY`), register URL, and open-source launcher ([CycloneAwakening/SWGAwakening](https://github.com/CycloneAwakening/SWGAwakening)) all from [their Play Now page](https://swgawakening.com/connect). News feed is the standard phpBB endpoint.

SWG Infinity: description facts (largest Pre-CU, 800+ weekly players, launcher downloads full client:  no discs needed), Discord, and register flow from [their Play Now page](https://www.swginfinity.com/play-now).

SWG Legends: launcher signs in with forum credentials per [their wiki](https://swglegends.com/wiki/index.php?title=SWG_Legends_Launcher); register/getting-started and support URLs from swglegends.com.

Restoration III: project description and 1.0 details from press coverage; website swgr.org. Launcher-login model confirmed by their launcher UI.

Server roster itself: the [community server list](https://swgemulator.fandom.com/wiki/Servers) (mirror of the subreddit wiki), which lists only AGPL-compliant projects.

## Convention-based (flagged unverified in the manifest)

Login hosts for all other servers follow the community convention `login.<domain>:44453`. Most SWGEmu-based servers use exactly this, but each needs operator confirmation:  the manifest descriptions say so.

## Operator-only (placeholders)

Patch `baseUrl`/`fileManifestUrl` for every server: these point at `patch.example.invalid` until the operator publishes a file manifest (or a moderator generates one from the server's public launcher config:  most are AGPL/open-source, so the patch endpoints are usually in their launcher repos: Awakening, Stardust, EiF, Infinity, Reckoning, Sunrunner, AotC, and Flurry all link source from the server list).

## Maintenance

To claim or correct an entry: edit `registry/manifests/<serverId>.json`, remove the bracketed flags from the description, and open a PR. The bundled copy inside the launcher updates at the next release; the live registry updates immediately.
