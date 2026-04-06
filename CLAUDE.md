# lamat

A WPF typing/keyboard practice application built with .NET 10 and C#.

## Project Structure

- `lamat/Models/` — Data models (practice items, key steps, keyboard info)
- `lamat/Services/` — Business logic (session management, evaluators, data loading, rendering)
- `lamat/Views/` — UI views
- `lamat/Data/` — JSON practice data files (word, sentence, key-sequence practice)

## Key Concepts

- **Practice modes**: Word practice, sentence practice, key-sequence practice (`PracticeModeType`)
- **Session services**: `SentenceSessionService`, `KeySequenceSessionService` manage active practice sessions
- **Evaluators**: `SentenceEvaluator`, `InputSequenceEvaluator` check user input correctness
- **Data loading**: `PracticeDataLoader` reads JSON files from `Data/`
- **Rendering**: `PracticeTextRenderer` handles display of practice text

## Build & Run

Open `lamat.slnx` in Visual Studio and run. Targets `net10.0-windows` with WPF.
