---
name: Play button next to timecode
overview: Move the play/pause control from the top header to the transport bar, placed immediately to the left of the timecode display, and use a symmetric play icon.
todos:
  - id: transport-play
    content: Add play/pause button to TransportBarPanel (left of time area)
  - id: remove-header-play
    content: Remove play/pause from HeaderBarPanel
  - id: wire-game
    content: Wire transport play/pause in BeatSyncGame; remove header wiring
isProject: false
---

# Play button next to timecode

## Goal

- **Location**: Play/pause button in the **transport bar** (second row), immediately to the **left of the timecode** (time display). Order: `[Volume] [BPM − value +] [Play/Pause] [Time]`.
- **Icon**: Use a **symmetric** right-pointing play triangle (point at vertical center); keep the existing pause (two bars) icon.

## 1. [TransportBarPanel.cs](AGX-Beat-Sync/UI/TransportBarPanel.cs)

- **Layout**: Introduce a play/pause button between BPM and time:
  - Add constant, e.g. `PlayPauseButtonWidth = 28`.
  - Add `GetPlayPauseButtonRect()`: same vertical centering as BPM/time (`Bounds.Y + (Bounds.Height - 24) / 2`, height 24), x = `GetBpmArea().Right + Padding`, width = `PlayPauseButtonWidth`.
  - Change `GetTimeAreaRect()` so the time area starts at `GetPlayPauseButtonRect().Right + Padding` instead of `bpm.Right + Padding`.
- **Callback**: Add `Action? OnPlayPauseToggle { get; set; }`. Transport is already set by the game, so `IsPlaying` can be read from `Transport?.IsPlaying`.
- **Update**: In `Update`, when `Input.MouseLeftPressed` and `ContainsPoint`, if click is in `GetPlayPauseButtonRect()`, call `OnPlayPauseToggle?.Invoke()` (before or after existing BPM/time handling).
- **Draw**: In `DrawContent`, before drawing the time area:
  - Draw the play/pause button background (e.g. same style as BPM minus/plus: `new Color(48, 52, 58)` or slightly different for hover).
  - Draw play icon (symmetric triangle) or pause icon (two vertical bars) based on `Transport?.IsPlaying`. Use the same pixel-rect approach as the current header; implement **symmetric** play triangle: for each row, `dist = Min(row, h - 1 - row)`, `lineWidth = Min(w, 1 + (2 * dist * (w - 1)) / (h / 2))`, draw centered at `x = cx - lineWidth / 2`.
- **GetHoverText**: Add a case for `GetPlayPauseButtonRect().Contains(mouse)` returning "Play (Space)" / "Pause (Space)".

## 2. [HeaderBarPanel.cs](AGX-Beat-Sync/UI/HeaderBarPanel.cs)

- Remove play/pause entirely: delete `PlayPauseButtonWidth`, `_playPauseButtonRect`, `IsPlaying`, `OnPlayPauseToggle`, the play/pause rect computation in `Update`, the click branch for `_playPauseButtonRect`, the play/pause draw block and `DrawPlayIcon`/`DrawPauseIcon` helpers, and the play/pause branch in `GetHoverText`.

## 3. [BeatSyncGame.cs](AGX-Beat-Sync/BeatSyncGame.cs)

- **Remove** header play/pause wiring: delete the assignment to `_headerBarPanel.IsPlaying` and the `_headerBarPanel.OnPlayPauseToggle` lambda (the block that toggles Transport and _audio).
- **Add** transport bar play/pause: set `_transportBar.OnPlayPauseToggle` to that same lambda (toggle `Transport.Play()`/`Pause()` and `_audio.Play()`/`Pause()`/`Seek`; call `InspectorDrawer.InvalidateLabelCache()` when starting). No need to set `IsPlaying` on the transport bar—it reads from `Transport.IsPlaying` when drawing.

## Result

- Transport bar order: Volume | BPM | **Play/Pause** | Time.
- Play icon is a proper symmetric right-pointing triangle; pause unchanged.
- Header bar only has File and Edit.
