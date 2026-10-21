# TheRun Component Settings UI Spec

Model: `LiveSplit.UI.Components.CollectorSettings` (`SettingsBase`, `INotifyPropertyChanged`).
Component: `LiveSplit.UI.Components.CollectorComponent` (`LogicComponent`).

## Overview

The TheRun component automatically syncs splits to therun.gg, supports live race tracking, and tracks personal bests and run history.

## Dialog Layout

Fixed dialog size: 476 x 280, 7px padding. Arranged in a vertical grid/table layout.
All labels are translation keys (`UiLocalizer.Translate`).

## Controls in Order

| Row / Label | Kind | Binding / Property | Behavior / Description |
|---|---|---|---|
| Header Description | Label (multi-line) | Static | `"therun.gg syncs your splits automatically and shows your live runs alongside your personal bests, stats, and history."` (spans all columns) |
| "Get your upload key at therun.gg/livesplit" | Link / Hyperlink | Static / Action | Clicking opens `https://therun.gg/livesplit` in the default browser via `PlatformServices.Current.OpenUrl`. |
| "Upload Key" | Label + Password Box + Button | `UploadKey`, `CanValidate`, `TestLabel` | Label: `"Upload Key"`. Text box with masked input (`UseSystemPasswordChar = true`) bound two-way to `UploadKey`. "Test" button bound to `TestLabel` (`"Test"` when idle, `"`...`"` while validating), enabled through `CanValidate` (`Status != ConnectionStatus.Validating`). Clicking initiates `ValidateKeyAsync()`. Text/accent color changes according to `Status`: green when `Connected`, red when `Error`. |
| User Profile Row | Image + Link + Link | `PictureUrl`, `ValidatedUsername`, `ProfileUrl`, `LiveUrl`, `StatusMessage` | • **Avatar**: Image box loaded from `PictureUrl`, visible when `Status == ConnectionStatus.Connected && PictureUrl != null`.<br>• **Username / Error Link**: When `Status == ConnectionStatus.Connected`, displays `"therun.gg/{ValidatedUsername}"` and clicking opens `ProfileUrl`. When `Status == ConnectionStatus.Error`, displays `StatusMessage` (`"Invalid upload key."` or `"Could not reach therun.gg."`).<br>• **Live Link**: Text `"Live"`, visible when `Status == ConnectionStatus.Connected && ValidatedUsername != null`. Clicking opens `LiveUrl` (`https://therun.gg/live/{ValidatedUsername}`). |
| "Enable Stats Sync" | Checkbox | `IsStatsUploadingEnabled` | Default: `true`. Tooltip: `"Syncs your splits file to therun.gg after each completed run, on reset, and when closing LiveSplit.\nThis powers your profile stats, personal bests, and history."` |
| "Sync on Reset" | Checkbox | `IsUploadOnResetEnabled` | Default: `true`. Tooltip: `"When enabled, stats are synced every time you reset the timer.\nWhen disabled, stats are only synced after a completed run or when closing LiveSplit."` |
| "Enable Live Tracking" | Checkbox | `IsLiveTrackingEnabled` | Default: `true`. Tooltip: `"Sends real-time split data to therun.gg so your current run appears on the live page.\nViewers and other runners can follow your progress as it happens."` |
| "Upload Layout Path" | Checkbox | `IsLayoutPathUploadEnabled` | Default: `false`. Tooltip: `"Includes the layout file path in the uploaded splits file.\nDisabled by default because the path may contain personal information such as your Windows username.\nIf disabled, you will need to reload your layout when re-downloading your splits from therun.gg."` |

## Storage and Persistence

- **Upload Key Security**: To avoid leaking secrets in layout XML files, the `UploadKey` is persisted to disk at `$XDG_CONFIG_HOME/LiveSplit/TheRun/uploadkey.txt` (or `%LOCALAPPDATA%/Livesplit.TheRun/uploadkey.txt` on Windows) with user-only permissions (`0600` on Linux).
- **Legacy XML Migration**: If the layout XML contains a `<Path>` element with an upload key, it is loaded, saved to the local key file, and cleared from the model.
- **Settings XML**: Version `1.0.0`. Stores `IsStatsUploadingEnabled`, `IsUploadOnResetEnabled`, `IsLiveTrackingEnabled`, `IsLayoutPathUploadEnabled`.
