# Changelog

All notable changes to this project are documented here.

**Build 9781 [2021012]**

### Fixed
- Crash (duplicate dictionary key) when a folder held files that pad to the same sort name, e.g. `1.mp3` and `001.mp3`.
- Crash when ffmpeg could not be started (the error was swallowed, then reading the exit code threw).
- Invalid or missing arguments produced a confusing "path invalid" message; the parser's usage text is now shown and the app exits with code 1. `--help`/`--version` exit with 0.
- The mp3 filter matched any name ending in `mp3` (e.g. `foo.xmp3`); it now checks the exact `.mp3` extension, ignoring case.
- `-f` was marked required despite having a default; it is now optional.
- `Console.Clear()` no longer crashes when output is redirected.
- Unreadable subfolders are skipped instead of aborting the run.

### Changed
- The merge is written to `<folder>.merging.tmp` and renamed to `<folder>.mp3` only when ffmpeg succeeded and produced a non-empty file, so a failed run can no longer leave a corrupt `.mp3`.
- Original files are deleted only after the merged file is in place; delete failures are reported rather than fatal.
- ffmpeg arguments are passed as an argument list (correct quoting for spaces/trailing backslashes) and `-nostdin` is added so ffmpeg can't hang waiting for input.
- Exit code is 1 if any folder failed to merge.
- Filenames containing `|` (unsupported by ffmpeg's concat syntax) cause that folder to be skipped with a message.
- Code restructured into static methods with doc comments; nullable-warning suppression removed.

### Build
- A Release build now also publishes a single-file, framework-dependent win-x64 exe to `bin/Release/net8.0-windows/win-x64/publish/` (no separate publish step).

### Documentation
- README: documented optional `-f`, `-k`, subfolder/root handling, temp-file safety and the `|` limitation.
- Added this changelog.

**Build 8884 [20240428]**

Initial release (commits `42f8f05`, `8d2706b`).

### Added
- Console app (.NET 8, Windows) that recursively walks a root folder and merges the mp3s in each subfolder into a single `<foldername>.mp3` using ffmpeg's concat protocol with stream copy (no re-encode).
- Options: `-f/--ffmpegPath`, `-p/--parseFolderPath`, `-k/--keepOriginalFiles`.
- Natural-ish sorting by zero-padding numbers in filenames to three digits (`chapter 2` sorts before `chapter 11`).
- Originals deleted after a successful merge unless `-k` is given; an existing `<foldername>.mp3` is never used as an input.
- Falls back to the root folder itself when it has no subfolders.
- README, Apache-2.0 license, app icon, solution/project files, funding link.
