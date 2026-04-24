# lamat

A WPF typing/keyboard practice application for learning the Jarai keyboard layout, built with .NET 10 and C#.

## Project Structure

- `lamat/Models/` — Data models (practice items, key steps, keyboard info, Jarai key entries)
- `lamat/Services/` — Business logic (session management, evaluators, data loading, layout service)
- `lamat/Controls/` — Custom WPF controls (visual keyboard)
- `lamat/Data/` — Practice data files and keyboard layout definition
- `lamat/App.xaml` — Global dark theme styles and brushes

## UI Architecture

The app starts at a **home screen** (`HomePanel`) and navigates into a **practice shell** (`PracticePanel`) when a mode is selected. Both panels live in the root `Grid` of `MainWindow`; visibility is toggled between them. A "← Home" button in the practice shell returns to the home screen.

## Practice Modes

### Word Practice (active)
- Loads `word-practice.json` — each item has a Jarai `displayText` and a list of `KeyStep`s (physical key IDs)
- User presses keys one at a time; evaluated against the expected sequence
- Shift is a separate step: user must hold Shift while pressing the next key; releasing Shift early reverts to the Shift step
- Uses `PreviewKeyDown` (not `KeyDown`) to capture physical keys before Keyman/IME processes them
- "Your Input" field shows Keyman-converted Jarai characters via `PreviewTextInput`
- A `JaraiKeyboardControl` renders the full keyboard with Jarai characters on each key and highlights the key(s) to press

### Sentence Practice (active)
- Loads `sentence-practice.json` — one sentence per line (plain text, not JSON)
- User types each space-separated word into a TextBox and presses Space/Enter to advance

## Key Services

- `KeySequenceSessionService` — tracks current item/step index for word practice; supports `RevertStep()`
- `InputSequenceEvaluator` — compares physical key ID against expected step (case-insensitive)
- `KeyboardHintService` — formats hint text (e.g. "Shift" for modifier steps); `GetKeysToHighlight()` returns key IDs for the visual keyboard to highlight
- `JaraiLayoutService` — loads `jarai-keyboard-layout.json`; provides `GetNormalLabel(keyId)` and `GetShiftedLabel(keyId)`
- `SentenceSessionService` — tracks sentence/word index; supports `AdvanceWord()`
- `SentenceEvaluator` — Unicode-normalised word comparison
- `PracticeDataLoader` — loads word practice from JSON, sentence practice line-by-line

## Key Controls

### `JaraiKeyboardControl` (`lamat/Controls/`)
- Programmatically builds a 4-row QWERTY keyboard on `Initialize(JaraiLayoutService)`
- All four rows are 720px wide (special key widths calculated precisely so rows align)
- Each key shows the Jarai character (large, centered) and the English key identifier (small, top-left)
- Special keys (Tab, Caps, Bksp, Enter, Shift) use `SurfaceBrush` background and muted label
- `SetHighlights(string[] keyIds)` highlights the specified keys in `AccentBrush`
- Called from `RefreshKeySequenceUI()` via `_keyboardHintService.GetKeysToHighlight()`

## Key Data Format (`word-practice.json`)

```json
{
  "items": [
    {
      "displayText": "ជិះ",
      "steps": [
        { "keyId": "LeftShift" },
        { "keyId": "C" },
        { "keyId": "I" },
        { "keyId": "LeftShift" },
        { "keyId": "H" }
      ]
    }
  ]
}
```

Key IDs are WPF `Key` enum names: letter keys (`A`–`Z`), digit keys (`D0`–`D9`), and Oem keys (`OemSemicolon`, `OemQuotes`, `OemComma`, `OemPeriod`, `OemQuestion`, `OemMinus`, `OemPlus`, `OemTilde`, `OemOpenBrackets`, `OemCloseBrackets`, `OemPipe`). Modifier keys (`LeftShift`, `RightShift`, `LeftCtrl`, etc.) are defined in `ModifierKeyIds`.

## Keyboard Layout Data (`jarai-keyboard-layout.json`)

Maps WPF key ID → `{ "normal": "ក", "shifted": "គ" }` for all keys in the Jarai Keyman layout. Used by `JaraiLayoutService` and `JaraiKeyboardControl`. Covers all letter keys, number row, and Oem symbol keys.

## Dark Theme (`App.xaml`)

Named brushes used throughout:

| Resource | Color | Use |
|---|---|---|
| `BgBrush` | `#1a1b2e` | Window background |
| `SurfaceBrush` | `#252636` | Cards, special keys |
| `Surface2Brush` | `#2f3047` | Regular keys, inputs |
| `BorderBrush` | `#363752` | Key borders |
| `AccentBrush` | `#7c6af7` | Highlighted keys, active elements |
| `AccentHoverBrush` | `#9580ff` | Highlighted key borders |
| `TextBrush` | `#e2e8f0` | Primary text |
| `MutedBrush` | `#6b7280` | Secondary/hint text |
| `ErrorBrush` | `#f87171` | Wrong key feedback |
| `SuccessBrush` | `#4ade80` | Correct key feedback |

Global styles defined for `Button` (accent, rounded), `GhostButton` (keyed style, outline), and `TextBox` (dark, rounded, accent focus ring).

## IME / Keyman Notes

- `PreviewKeyDown` captures raw physical key before Keyman converts it
- `PreviewTextInput` captures the Keyman-converted Jarai character for display only
- Modifier steps skip `PreviewTextInput` (they produce no character output)
- Setting `e.Handled = true` in `PreviewKeyDown` blocks `PreviewTextInput` — only set it for wrong key presses

## Build & Run

Open `lamat.slnx` in Visual Studio and run. Targets `net10.0-windows` with WPF.
Keyman with a Jarai keyboard layout must be active for correct character display.
