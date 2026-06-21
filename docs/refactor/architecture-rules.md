# Architecture Rules

These rules describe the intended target architecture. R0 checks may allow existing debt, but new work should move toward these boundaries.

## Domain

- Does not reference WinForms.
- Does not reference UI, Storage, Terraria integration, process watchers, file repositories, or shell services.
- Owns pure split, timer, boss, route, comparison, and formatting rules.

## Application

- Does not reference WinForms.
- Accepts user input and runtime notifications as commands or snapshots.
- Emits state snapshots and effects instead of performing shell side effects.
- Does not directly call `AppSettingsStore` or `AppLogger` in the final architecture.

## Configuration

- Owns settings DTOs, defaults, migration, normalization, profile selection, and repository behavior.
- Does not decide run state.
- Does not execute UI or automation side effects.
- Keeps compatibility logic in migrators and current-shape cleanup in normalizers.

## Storage

- Owns run statistics, split time sets, and world pool persistence.
- Does not reference UI, Application shell, or Terraria watcher implementations.

## Terraria

- Owns process, memory, window, save, world generation, and automation adapters.
- Converts unstable external behavior into stable project models.
- Does not reference `MainForm`, settings pages, overlay shell, or rendering implementation.

## UI

- Owns WinForms windows, input adapters, overlay hosts, modal lifecycle, and shell side-effect execution.
- Event handlers should collect input and route it through Application commands or shell services.
- Settings pages edit settings data; they do not start automation, watchers, or global hotkey registration directly.

## Infrastructure

- Owns common JSON, logging, diagnostics, scheduling, and platform wrappers.
- Windows native APIs live under the Windows-specific infrastructure boundary.

## Test Policy During Refactor

- If a test encodes old architecture rather than desired behavior, update the test to the new architecture.
- Do not add production compatibility code solely to satisfy a stale test.
- Keep tests deterministic and prefer snapshots, fake watchers, fake ports, and explicit timestamps.
