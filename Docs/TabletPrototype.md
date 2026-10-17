# Tablet mission guide prototype

Branch: `feature/tablet-mission-guide`.

Open `Assets/00_Scenes/j_TabletPrototype.unity`, enter Play mode, and select **Start Host**. The scene uses `j_TabletPlayer`, a variant of the Mars player. The original Mars scene and player prefab are unchanged. The prototype uses loopback UDP port **7778** for both Host and Client.

**Tab** opens or closes the equipped tablet. The interface rises from the bottom over 0.32 seconds. Click a mission, recipe, mineral or objective widget to expand it; the bottom dock also changes sections. Neptuno, Marte and Saturno buttons browse the planet records. Movement, jumping, camera look and world interactions stay locked until the closing transition finishes. The world continues running.

The equipment prototype has one slot initialized with `j_MissionTablet.asset`; it is not a general inventory or pickup system. The nine user-supplied recipes are stored in `j_PlanetRecipeCatalog.asset`. Minerals represent known catalog types, not owned quantities. Mission progression, resource discovery and actual crafting remain pending.

`TabletGuide` replicates the open/closed state with an owner-writable NetworkVariable. Observing clients instantiate the graybox tablet at the astronaut right hand; only its owner creates the screen interface. Draw/stow animation is pending: connect `onTabletRaised` and `onTabletLowered` when supplied. Current presentation changes model visibility without an arm animation.

## Skin and design skill

`j_PixelSciFiTabletSkin.asset` references cyan 9-sliced panels, button/tab states and ship/repair/mining/check icons from `Assets/PixelUISciFiFree`. Body text retains the existing font for Spanish accents and readability. Twelve planet/section combinations were rendered without text overflow. Review screenshots are excluded from Git.

`Docs/skills/unity-interface-design/SKILL.md` contains the reusable skill, also installed in the user skill directory.

## Validation and network shutdown

Host spawn, opening, both example tabs and control restoration were checked during the first prototype. The latest UI was also reviewed on an isolated canvas. A separate two-client test for remote presentation and late joining is still pending.

Port 7777 was retained by an earlier Unity session inside the Editor process. `GameNetworkManager` now leaves the base Netcode Awake/OnDestroy lifecycle intact and uses the base singleton so its transport can shut down normally. After this correction, two complete Play / Host / Stop cycles on **7778** spawned a player successfully, with no console errors on the second cycle.
