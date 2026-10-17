---
name: unity-interface-design
description: Design and implement Unity game interfaces, including in-world tablets, widget dashboards, HUDs and inventories, using the project's existing UI system and live Editor validation.
---

# Unity interface design

Inspect the current UI framework, input and multiplayer ownership before choosing components. Prefer the existing stack; keep presentation separate from mission, inventory and crafting state.

For a tablet, establish a physical silhouette with bezel, rounded screen, restrained depth and a coherent status bar. A widget home screen should show useful summaries immediately and let cards expand into details. Do not replace requested widgets with a grid of generic app icons.

Use the supplied references for hierarchy, palette and content. Preserve exact recipe ingredients, amounts and planet associations. Store this content in editable data assets. Distinguish catalog knowledge from owned quantities; do not invent inventory counts, mission progress or working crafting actions.

Use a small set of consistent spacings, readable text sizes and distinct interactive states. Ensure button hit areas are generous and content wraps without clipping. Match the game's art direction; a graybox may still have deliberate composition and typography. Prefer lightweight native geometry for simple shapes and icons over new image dependencies.

Keep the local interface private to its owner. Replicate only relevant world presentation, such as holding the tablet. Opening and closing must restore the previous movement, camera and cursor state; do not pause multiplayer time. Prepare hooks for supplied character animations without claiming missing animations are implemented.

Validate compilation, opening, closing, rapid toggling and section navigation in the Editor. Inspect the rendered interface at a representative landscape resolution. If an MCP screenshot path causes recursive PlayerLoop errors, avoid that path and use explicit camera rendering for visual checks. State whether multi-client behavior has actually been exercised.

Keep changes isolated to the requested prototype or player variant. Follow the repository's branch and numbered commit conventions, and avoid committing local verification captures.
