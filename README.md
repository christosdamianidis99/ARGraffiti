# ARGraffiti

ARGraffiti is a mobile AR graffiti demo for Unity. You scan a surface, paint in AR, save locally, and reopen a gallery that replays your saved pieces at the exact pose they were drawn.

## How it works
- **Scan & lock a plane:** Tap **Scan**, then **Select Surface** to lock to one detected plane; all other planes hide so you paint on a single surface.
- **Paint:** Long-press the Graffiti/brush button to start painting; toggle circle/square brush and colors. Stroke history supports undo/redo.
- **Save locally:** Tap **Save** to capture the strokes. Saves PNG + thumbnail plus pose data under `Application.persistentDataPath/graffiti/<user>`.
- **Gallery in AR:** Tap **Gallery** to show saved pieces in-world at their recorded poses (no cloud/auth). Only a back and delete button are shown; delete removes the last shown item and returns to scanning.
- **No login:** Google sign-in and login scenes are removed; everything runs offline/local.

## Project setup
- Unity: Built with AR Foundation (ARCore/ARKit). Ensure `AR Session`, `AR Session Origin`, `AR Camera` with `ARRaycastManager`, `ARPlaneManager`, `ARAnchorManager` are in the AR scene.
- Scenes: Start directly in the AR main scene (login scene removed).
- UI wiring: `AppStateControllerPhone` expects references for scan/select/graffiti/save/gallery buttons, reticle, `PhonePainter`, and optional `Panel_Gallery` with `Button_Back`, `Button_Delete`, and `Loading` child. If missing, it auto-builds a minimal overlay at runtime.

## Build notes
- All persistence is local; repository stored under `Application.persistentDataPath/graffiti/`.
- Google Sign-In packages are unused; you can exclude them from builds if desired.
