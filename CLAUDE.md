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
- Loads `word-practice.json` (or a text file via `LoadWordPracticeFromTextFile`) — each item has a Jarai `displayText` and a list of `KeyStep`s (physical key IDs), converted at load time into a flat `(Chars, KeyId, Modifier)` sequence by `ComputeWordCharSeq`
- Evaluated key-ID-by-key-ID via `PreviewKeyDown` against that precomputed sequence — the physical key pressed (and whether the required modifier, `"LeftShift"`/`"RightAlt"`/`null`, is held) must match the expected step exactly; no Keyman/IME output is read at all, so this mode works even without Keyman active
- A modifier is a separate step: user must hold Shift or Right Alt while pressing the next key; releasing it early is tolerated (state is just re-checked against the next keystroke, no explicit "revert" step)
- **Word flow strip**: one faded previous word (the one just completed) on the left, the current word bright and centered, two upcoming words fading out to the right — populated via `KeySequenceSessionService.PeekItem(offset)`, which reads items around `CurrentItemIndex` without disturbing session state. Replaces the shared `TargetText` for this mode (collapsed via `SwitchMode`'s `isWord` check).
- "PRESS" badge (`ExpectedKeyText`) shows the next key (+ modifier name) to press; "YOUR INPUT" (`ActualKeyText`) accumulates the confirmed-correct characters typed so far in the current word
- An invisible 1×1 `TextBox` (`WordPracticeInputBox`) holds keyboard focus so Keyman's TSF context activates for the window; without it Keyman ignores keystrokes until a focus cycle occurs
- A `JaraiKeyboardControl` renders the full keyboard with Jarai characters on each key and highlights the expected key + modifier

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

### Paragraph Practice (active)
- Loads a raw prose "story" `.txt` file (bundled sample: `Data/paragraph-practice-sample.txt`, plus custom file import) via `PracticeDataLoader.LoadParagraphPracticeFromTextFile`
- Import pipeline: blank lines separate paragraphs; single newlines within a paragraph are soft wraps and get joined with a space; hyphens (`-`) and ASCII digit runs (verse/chapter markers) are stripped; the joined paragraph is then split into individual practice lines on sentence-ending punctuation (`។ . ! ?`); characters with no key mapping are stripped per-line (dropping any dependent combining marks/coeng-subscript cluster too, not just the single bad codepoint — see "Khmer/Jarai Grapheme Clustering" below) rather than discarding the whole line
- **Word-based evaluation, not key-by-key**: earlier versions precomputed an expected physical-key sequence per line (like Word Practice) and validated every keystroke against it, but that reverse derivation (character → which key produces it) was fragile and occasionally guided the wrong key. Paragraph Practice now works exactly like Sentence Practice instead — free-form typing (characters built from whichever key + Shift/AltGr is actually pressed, via forward `GetNormalLabel`/`GetShiftedLabel`/`GetAltGrLabel` lookups), with each word checked against the target word on Space/Enter via `SentenceEvaluator.IsWordMatch`. `_paragraphCurrentWord`/`_paragraphSubmittedWords`/`_paragraphWordResults`/`_paragraphWordIdx` mirror Sentence Practice's per-word state; `AdvanceParagraphWord()`/`RevertLastParagraphWord()` mirror `AdvanceSentenceWord()`/`RevertLastWord()`. Unlike Sentence Practice, an incorrect word does **not** block progress — the line always completes (colors show right/wrong per word) and waits for "press any key to continue" before advancing, regardless of correctness.
- No visual keyboard and no key highlighting in this mode (`JaraiKeyboard.Visibility` collapsed alongside Sentence Practice) — there's no single "next key" to guide toward anymore
- **Vertical line-flow UI**: previous line (faded, above) → current line (each word colored — accent for the word in progress, green/red once submitted, like Sentence Practice's `TargetText`) → a live "typed so far" box (`ParagraphInputDisplay`) → 5 upcoming lines (fading, below). `ParagraphSessionService.PeekLine(offset)` reads prev/next lines for display; `KeySequenceSessionService`/`ParagraphSessionService` don't track word-within-line index, so that's local `MainWindow` state (`_paragraphWordIdx`), reset by `LoadParagraphLine()` whenever a new line loads

## Key Services

- `KeySequenceSessionService` — tracks current item/step index for word practice; supports `RevertStep()`; `PeekItem(offset)` reads items around the current one (for the word flow strip) without disturbing session state
- `InputSequenceEvaluator` — compares physical key ID against expected step (case-insensitive)
- `KeyboardHintService` — formats hint text (e.g. "Shift" for modifier steps); `GetKeysToHighlight()` returns key IDs for the visual keyboard to highlight (currently unused by `MainWindow`, which inlines equivalent logic)
- `JaraiLayoutService` — loads `jarai-keyboard-layout.json`; provides `GetNormalLabel(keyId)`, `GetShiftedLabel(keyId)`, `GetAltGrLabel(keyId)` (forward key→char lookups, used by Sentence/Paragraph Practice's free-form typing) and `DeriveKeySteps` (reverse char→key derivation, used only by `LoadWordPracticeFromTextFile` to turn raw text into `word-practice.json`-shaped `KeyStep` sequences), resolving the correct modifier (`"LeftShift"`, `"RightAlt"`, or `null`) per character via the reverse char→key map
- `SentenceSessionService` — tracks sentence/word index; supports `AdvanceWord()`, `RevertWord()`, `ResetWordIndex()`, `AdvanceSentence()`
- `SentenceEvaluator` — Unicode-normalised word comparison
- `ParagraphSessionService` — tracks `CurrentLineIndex` through a flat list of practice lines; `PeekLine(offset)` for prev/next line lookahead used by the vertical line-flow UI
- `PracticeDataLoader` — loads word practice from JSON, sentence practice line-by-line, paragraph practice from raw prose text
- `SoundService` — background music (looped `MediaPlayer`) + a synthesized key-click sound effect (in-memory WAV, no bundled asset needed); `ToggleMute()` mutes both

## Right-Alt (AltGr) Support

A third character layer, alongside normal and Shift. `jarai-keyboard-layout.json` entries carry an optional `"altGr"` field; `JaraiKeyEntry.AltGr` holds it.

- Represented as `string? Modifier` throughout (the KeyId to hold: `"LeftShift"`, `"RightAlt"`, or `null`) rather than a shift-specific bool — this is the same convention `word-practice.json`'s `KeyStep` list already used for authored Shift steps, just generalized
- `JaraiLayoutService`'s reverse map (`TryGetKeyForChar`) resolves a character to `(keyId, modifier)`; `BuildReverseMap` registers normal/shifted/altGr entries with `modifier` = `null`/`"LeftShift"`/`"RightAlt"` respectively
- `MainWindow` tracks `_altGrHeld` alongside `_shiftHeld`; Right Alt press/release is captured the same way Shift is (via `Key.System` → `e.SystemKey` unwrapping) and is **not** marked `e.Handled` on the modifier key itself, so Keyman still sees it
- Applies uniformly across Word/Position/Paragraph/Sentence practice matching and highlighting — `ModifierDisplayName(modifier)` formats "Shift" or "Alt" for status text
- Position Practice's candidate generation (`EnqueueNextPositionTarget`) includes the AltGr layer alongside normal/shifted, filtered by `IsJaraiChar` the same way shifted chars are (so ASCII punctuation on the AltGr layer, e.g. `~$&*{}=[]:,.`, isn't drilled as a position-practice target)

## Sentence Practice State (`MainWindow`)

| Field | Type | Purpose |
|---|---|---|
| `_submittedWords` | `List<string>` | Each word submitted this sentence (enables revert) |
| `_typedWordsDisplay` | `string` | Concatenated submitted words with trailing spaces |
| `_wordResults` | `List<bool?>` | Per-word correctness; null = not yet submitted |
| `_sentenceFailed` | `bool` | True after sentence completed with errors; next Space resets |

## Key Controls

### `JaraiKeyboardControl` (`lamat/Controls/`)
- Programmatically builds a QWERTY keyboard on `Initialize(JaraiLayoutService)`: 4 main rows (720px wide, special key widths calculated precisely so rows align) plus a 5th row with `Space` and `RightAlt` (centered, not aligned to the 720px grid)
- Each key shows the Jarai character (large, centered) and the English key identifier (small, top-left)
- Special keys (Tab, Caps, Bksp, Enter, Shift, Space, Alt) use `SurfaceBrush` background and muted label
- `SetHighlights(string[] keyIds, string? errorKeyId, string? modifierKey, bool modifierHeld)` highlights the target key(s) in `AccentBrush`, and the given modifier key (e.g. `"LeftShift"` or `"RightAlt"`) in amber (needed) or green (held) — generic across Shift and AltGr, not shift-specific despite the parameter having originally been named `shiftKey`/`shiftHeld`
- Called directly from `MainWindow`'s `Refresh*UI()` methods (not via `KeyboardHintService`, which is currently unused)
- Hidden entirely (`JaraiKeyboard.Visibility` collapsed) for Sentence Practice and Paragraph Practice — both are free-form-typing modes with no single "next key" to highlight

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

Maps WPF key ID → `{ "normal": "ក", "shifted": "គ", "altGr": "ឝ" }` for all keys in the Jarai Keyman layout. `altGr` is optional and only present on keys with a genuine third-layer character. Used by `JaraiLayoutService` and `JaraiKeyboardControl`. Covers all letter keys, number row, and Oem symbol keys.

Some entries are intentionally blank (`""`) — `R`'s shifted value, `OemPlus`'s normal value, and `D8`'s shifted value — because those combos don't correspond to real Jarai characters and were removed from training/typing on request. A blank `normal`/`shifted`/`altGr` value excludes that layer from the reverse char→key map, Position Practice candidates, and the visual keyboard's label for that slot, without deleting the key entry itself (deleting the whole entry would make `GetNormalLabel` fall back to showing the raw key ID as a label).

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

All four modes now evaluate purely off the **physical key** — none of them read Keyman's converted character output. Word/Position Practice compare the pressed key ID against a precomputed expected step; Sentence/Paragraph Practice build up typed text themselves via forward `GetNormalLabel`/`GetShiftedLabel`/`GetAltGrLabel` lookups keyed on which physical key + modifier was pressed. Keyman is never asked what it thinks was typed.

- Every mode's `Window_KeyDown` (`PreviewKeyDown`) unwraps the real key the same way: `Key.System → e.SystemKey` (Alt-combos and some Shift combos route through here), `Key.ImeProcessed → e.ImeProcessedKey`, else `e.Key` as-is; if that still resolves to `Key.None`, `_lastRawVirtualKey` (captured from the raw `WM_KEYDOWN`/`WM_SYSKEYDOWN` message in `WndProc`) is used as a fallback via `KeyInterop.KeyFromVirtualKey`
- Modifier keys (Shift, Right Alt) are intentionally **not** marked `e.Handled` on their own press/release — only the following non-modifier key press is. This isn't for Keyman's benefit anymore (nothing reads its output), but changing it hasn't been revisited since the key-ID rewrite; treat it as the working convention
- **Every mode still requires a focused `TextBox` for Keyman to work**, even though its output is unused — Keyman is a TSF (Text Services Framework) input method, and WPF only activates a TSF edit context when a TextBox (or similar IME-aware element) is focused. Without one, Keyman goes dormant and — more importantly for `PreviewKeyDown`-only modes — some physical keystrokes can be swallowed before this app's own handler sees them. `WordPracticeInputBox`, `SentenceInputBox`, `PositionInputBox`, and `ParagraphInputBox` exist solely for this purpose — do not remove them, even though none of them have their `Text`/`TextChanged` read anymore

## Sound

- `SoundService` (`lamat/Services/`) owns a looped `MediaPlayer` for background music and a `SoundPlayer` playing an in-memory-synthesized key-click WAV (exponentially-decaying tone + noise burst) — no bundled click asset is needed
- Background music is optional: `Initialize` checks for `Data/Audio/background-music.mp3` (or `.wav`) and no-ops if absent; the `.csproj` globs `Data\Audio\**\*.mp3`/`*.wav` to copy-to-output automatically, so dropping a file in that folder is enough to enable it
- A 🔊/🔇 button (`MuteButton`, top-right, visible on every screen) calls `SoundService.ToggleMute()`, which pauses/resumes music and suppresses the click effect
- `PlayClick()` is called at the point each real (non-modifier) key press is accepted, in Word/Position/Paragraph practice's shared key-down handler and in Sentence Practice's key-down handler

## Khmer/Jarai Grapheme Clustering

`JaraiLayoutService.StripUnmappableChars` needs special handling beyond .NET's generic `StringInfo` grapheme rules: when the *base* character of a cluster has no key mapping and gets dropped, any combining marks or coeng+subscript-consonant stack immediately following it are dropped too — otherwise the orphaned dependent renders as a dotted-circle placeholder (no base to attach to). `IsCombiningMark` (Unicode category check) and the `Coeng` constant (`'្'`, U+17D2, private to that file) drive this. This still matters for Paragraph Practice's text importer.

(Paragraph Practice's UI used to need equivalent cluster-boundary logic for per-character coloring — splitting a Khmer coeng+subscript stack across two differently-colored `Run`s caused the same dotted-circle bug — but that code (`MainWindow.BuildParagraphClusters`/`GetKhmerClusterStarts`) was removed when Paragraph Practice switched to word-level coloring; see "Paragraph Practice" above.)

## Build & Run

Open `lamat.slnx` in Visual Studio and run. Targets `net10.0-windows` with WPF.
Keyman with a Jarai keyboard layout must be active for correct character display.
