# Changelog

## 0.25.3 — Alt menu controls

- Hold Left Alt to open the existing attachment cards with cursor control; release to close.
- Choose Hold or Toggle in F12. Toggle keeps Alt+R available for mounting positions.
- Add a short bottom control hint. Keep the original 0.25 card layout, animation timing and attachment operations.

## 0.27.0 — Saved experiment, not the current build

- Add middle-mouse Hold and Toggle activation with automatic cursor capture. Hold is the default; O and Left Alt remain available.
- Open one compact list on hover. Show up to three carried alternatives, with wheel scrolling, the equipped attachment at the bottom and a separate Remove current action.
- Add native mounting-position arrows alongside Alt+R, carried-source labels and action feedback.
- Preserve the original projected card angles, weapon-following anchors, standalone animation timing and guarded native inventory path.
- Cancel stale mouse presses after scrolling, changing slots, refreshing observations or closing the interface; retain mouse-release draining.

See [verification and live checks](docs/PROGRESS_0.27.0.md). These changes describe the local experimental build; this repository currently publishes its documentation.

## 0.25.2 — Current presentation baseline

Add firing-hand presentation for exact handgun templates and the fitted RShG-2/flare routes. Expand coverage to 194 registered templates, 129 authored bundles and 26 aliases. Coverage is implementation support, not a live visual pass for every weapon.

## 0.25.0 — Standalone presentation

Separate attachment presentation from native weapon inspection using a private sampled skeleton and an independent presentation clock. Restore presentation before gameplay handoff and native inventory submission.
