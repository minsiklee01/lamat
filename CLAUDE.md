# lamat

A WPF typing/keyboard practice application for learning the Jarai keyboard layout, built with .NET 10 and C#.

## Project Structure

- `lamat/Models/` — Data models (practice items, key steps, keyboard info)
- `lamat/Services/` — Business logic (session management, evaluators, data loading)
- `lamat/Views/` — UI views (CompletionView stub)
- `lamat/Data/` — Practice data files

## Practice Modes

### Word Practice (active)
- Loads `word-practice.json` — each item has a Jarai `displayText` and a list of `KeyStep`s (physical key IDs)
- User presses keys one at a time; evaluated against the expected sequence
- Shift is a separate step: user must hold Shift while pressing the next key; releasing Shift early reverts to the Shift step
- Uses `PreviewKeyDown` (not `KeyDown`) to capture physical keys before Keyman/IME processes them
- "Your Input" field shows Keyman-converted Jarai characters via `PreviewTextInput`

### Sentence Practice (active)
- Loads `sentence-practice.json` — one sentence per line (plain text, not JSON)
- User types each space-separated word into a TextBox and presses Space/Enter to advance

## Key Services

- `KeySequenceSessionService` — tracks current item/step index for word practice; supports `RevertStep()`
- `InputSequenceEvaluator` — compares physical key ID against expected step (case-insensitive)
- `KeyboardHintService` — formats hint text (e.g. "Shift" for modifier steps); `GetKeysToHighlight()` returns keys for a future visual keyboard
- `SentenceSessionService` — tracks sentence/word index; supports `AdvanceWord()`
- `SentenceEvaluator` — Unicode-normalised word comparison
- `PracticeDataLoader` — loads word practice from JSON, sentence practice line-by-line

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

Modifier keys (`LeftShift`, `RightShift`, `LeftCtrl`, etc.) are defined in `ModifierKeyIds`.

## IME / Keyman Notes

- `PreviewKeyDown` captures raw physical key before Keyman converts it
- `PreviewTextInput` captures the Keyman-converted Jarai character for display only
- Modifier steps skip `PreviewTextInput` (they produce no character output)
- Setting `e.Handled = true` in `PreviewKeyDown` blocks `PreviewTextInput` — only set it for wrong key presses

## Planned

- Visual keyboard diagram (use `GetKeysToHighlight()` from `KeyboardHintService`)
- More practice data

## Build & Run

Open `lamat.slnx` in Visual Studio and run. Targets `net10.0-windows` with WPF.
Keyman with a Jarai keyboard layout must be active for correct character display.
