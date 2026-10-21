# Racetime Settings UI Spec

Model: `LiveSplit.Racetime.RacetimeSettingsControl` (`SettingsBase`, `INotifyPropertyChanged`).
Associated race provider settings: `LiveSplit.Racetime.RacetimeSettings` (`RaceProviderSettings`).

## Overview

Racetime is a racing provider plugin for LiveSplit that integrates with racetime.gg.
The settings page is hosted inside the Race Provider settings container in LiveSplit.

## Controls in Order

| Label | Kind | Binding / Property | Behavior / Description |
|---|---|---|---|
| "There are no options available for this racing service." | Label | `Message` | Informational label centered in the settings area. No interactive settings controls are currently displayed. |

## Hidden / Legacy Settings

The following settings are persisted in `settings.cfg` within the `<Plugin>` node for `LiveSplit.Racetime.dll` via `RacetimeSettings.FromXml` / `ToXml`, but are not exposed as controls in the UI:

| Setting XML Element | Type | Default | Description |
|---|---|---|---|
| `LoadChatHistory` | `bool` | `true` | Whether to request and load chat history upon joining a room. |
| `HideResults` | `bool` | `false` | Whether to obscure finishing times and placements to avoid race spoilers. |

## Provider Metadata

- `DisplayName`: `"racetime.gg"`
- `WebsiteLink`: `"https://racetime.gg"`
- `RulesLink`: `"https://racetime.gg/about/rules"`
