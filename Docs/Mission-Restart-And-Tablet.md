# Mission restart and live tablet

## Team defeat

`MarsFusionSpawner` monitors connected players on the scene authority. It requires a valid player object and initialized health for every participant. Empty rooms, loading scenes, lobby scenes and completed missions do not trigger defeat.

When everyone is dead, a replicated three-second countdown starts. A living or still-loading participant cancels the countdown. After it expires, each peer despawns its own runtime objects and acknowledges preparation. The scene authority waits for all remaining peers and uses the existing runner to unload and load the same scene.

`MissionRestartController` lives on that runner, so scene unloading cannot destroy the reload task. Newly loaded scene objects restore resources, enemies, crafting and installed ship parts. New avatars restore health, stamina, ammunition, inventories and pending actions. Names, assigned session colors and player preferences remain intact. No disconnect or lobby transition is used for defeat.

## Tablet data

`TabletMissionTracker` reads replicated `CraftingTable`, `ShipRepairStation`, inventory and repair-action state. It does not parse console messages or maintain a second authoritative mission state.

- The current table selects Marte or Neptuno automatically.
- Checklist rows independently mark **fabricated** and **installed** for each component.
- Recipes show actual `RecipeData` ingredient/result sprites and local inventory amounts.
- Crafting and installed counts are shared team progress. Inventory counts belong to the tablet owner.
- Active installation progress, interrupted installation, fabrication, installation and pickups produce tablet activity feedback.
- The activity log updates in place. When the tablet is closed, a short notification points to Tab; the tablet never opens automatically.
- Other planets remain reference catalogs, without invented live completion or stock counts.

The recipe catalog references the original recipe assets for all nine recipes. Gameplay costs and tablet ingredients therefore use the same data.

## Validation

Validated in the Editor using Fusion Single in both `Mars` and `b_NeptuneExploration`:

1. Added valid recipe materials through inventory APIs.
2. Crafted and installed the first component through the existing crafting/repair APIs.
3. Confirmed the tracker and checklist showed 1/3 fabricated and 1/3 installed.
4. Inspected rendered recipes/checklists at 1600×900, including ingredient/result sprites and inventory counts.
5. Triggered lethal damage with leftover inventory and partial repair progress.
6. Confirmed the same runner reloaded the same level, spawned a new avatar and restored empty inventory and 0/3 crafting/repair progress. Marte also checked explicitly for a single avatar after respawn.

The Neptune visual inspection was paused to isolate it from enemy damage; the reset test ran with simulation resumed. Editor scenes were restored afterwards, preserving pre-existing unsaved Mars changes.

Two-client Shared-mode synchronization and scene-authority departure during reload have not been exercised. Runtime captures are stored outside Assets and are not part of the changeset.
