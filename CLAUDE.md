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
- If a non-Jarai (ASCII) character is received via `PreviewTextInput`, the step is reverted and a "Switch to Jarai keyboard" warning is shown
- A `JaraiKeyboardControl` renders the full keyboard with Jarai characters on each key and highlights the key(s) to press

### Sentence Practice (active)
- Loads `sentence-practice.json` — one sentence per line (plain text, not JSON)
- User types each space-separated word; Space/Enter submits the current word
- **Target sentence** (`TargetText`) renders each word as a colored `Run` inline: accent = current word, green = correct, red = incorrect
- **Input display** (`SentenceInputDisplay`) shows all submitted words plus the word currently being typed, so text accumulates across the sentence
- **Keyboard focus**: an invisible 1×1 `TextBox` (`SentenceInputBox`) holds keyboard focus so Keyman can output Jarai characters; its `TextChanged` event drives `SentenceInputDisplay`
- **Backspace** within a word: handled natively by `SentenceInputBox`
- **Backspace** across a word boundary (TextBox empty): intercepted in `PreviewKeyDown`, pops the last submitted word via `RevertLastWord()`, restores it to the TextBox, and clears its result color
- **End of sentence**: if all words correct → advance to next sentence; if any wrong → show final colors, status message prompts retry; next Space resets the sentence
- Space/Enter is intercepted in `PreviewKeyDown` before reaching the TextBox; also blocked in `PreviewTextInput` so no space lands in the TextBox

## Key Services

- `KeySequenceSessionService` — tracks current item/step index for word practice; supports `RevertStep()`
- `InputSequenceEvaluator` — compares physical key ID against expected step (case-insensitive)
- `KeyboardHintService` — formats hint text (e.g. "Shift" for modifier steps); `GetKeysToHighlight()` returns key IDs for the visual keyboard to highlight
- `JaraiLayoutService` — loads `jarai-keyboard-layout.json`; provides `GetNormalLabel(keyId)` and `GetShiftedLabel(keyId)`
- `SentenceSessionService` — tracks sentence/word index; supports `AdvanceWord()`, `RevertWord()`, `ResetWordIndex()`, `AdvanceSentence()`
- `SentenceEvaluator` — Unicode-normalised word comparison
- `PracticeDataLoader` — loads word practice from JSON, sentence practice line-by-line

## Sentence Practice State (`MainWindow`)

| Field | Type | Purpose |
|---|---|---|
| `_submittedWords` | `List<string>` | Each word submitted this sentence (enables revert) |
| `_typedWordsDisplay` | `string` | Concatenated submitted words with trailing spaces |
| `_wordResults` | `List<bool?>` | Per-word correctness; null = not yet submitted |
| `_sentenceFailed` | `bool` | True after sentence completed with errors; next Space resets |

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

- `PreviewKeyDown` captures raw physical key before Keyman converts it; Space/Enter for sentence practice must be intercepted here
- `PreviewTextInput` captures the Keyman-converted Jarai character; for word practice this drives the "Your Input" display
- Modifier steps skip `PreviewTextInput` (they produce no character output)
- `e.Handled = true` in `PreviewKeyDown` can suppress the following `PreviewTextInput` — only set it when actually consuming the key (wrong key presses, Space/Enter submission); do **not** set it unconditionally for Backspace or it will block Keyman's composition output
- Sentence practice requires a focused `TextBox` for Keyman to output characters; a `TextBlock` alone is insufficient — `SentenceInputBox` (invisible) exists solely to satisfy this requirement
- `ConvertKeyEventToKeyId` unwraps `Key.ImeProcessed` → `e.ImeProcessedKey` and filters out residual `Key.ImeProcessed` / `Key.None` results to avoid ghost error messages on focus re-entry

## Build & Run

Open `lamat.slnx` in Visual Studio and run. Targets `net10.0-windows` with WPF.
Keyman with a Jarai keyboard layout must be active for correct character display.
