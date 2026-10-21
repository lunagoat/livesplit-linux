# Racetime Channel Window UI Spec

Model / ViewModel: `LiveSplit.Racetime.View.ChannelForm` (`SettingsBase`, `IDisposable`, `INotifyPropertyChanged`).
Associated channel controller: `LiveSplit.Racetime.Controller.RacetimeChannel`.

## Overview

The Racetime channel window displays race progress, user status, and embedded web chat for a specific racetime.gg race room. In the Linux port, `ChannelForm` is completely decoupled from WinForms/WebView2 and serves as a view-model/state class consumed by the Avalonia shell or external web browser.

## Window Attributes

- Default size: 1145 x 619
- Minimum size: 600 x 400
- Always on top: Bound to `AlwaysOnTop` (defaults to layout's `AlwaysOnTop` setting)
- Window Title: Bound to `Title` (string, raises `PropertyChanged`):
  - Initial connection: `"Connecting to {channelName}"` (translation key `"Connecting to "`)
  - Race loaded: `"{Goal} [{GameName}] - {ChannelName}"`
  - Disconnected: `"Disconnected"` (translation key `"Disconnected"`)

## Controls and Layout

| Element | Kind | Binding / Property | Behavior / Description |
|---|---|---|---|
| Status / Loading Message | Label | `LoadingMessage` | Center-aligned text displayed while connecting or loading (`"Loading..."` initially; `""` when loaded; `"Error loading page."` on failure). |
| Race / Chat View | Web View / Embed | `PageUrl`, `IsPageVisible` | Displays racetime.gg live web page (`PageUrl` = `"{FullWebRoot}{Race.Id}/livesplit"`). Visible when `IsPageVisible` is true. Requests to the domain carry the OAuth Bearer token in the `Authorization` header. On platforms without embedded web view, can be opened in default browser via `PlatformServices.Current.OpenUrl`. |

## Lifecycle and Closing Protocol

- **Join Flow**: `RacetimeAPI.Instance.Join(timerModel, raceId)` creates the `RacetimeChannel` and `ChannelForm`, fires `RacetimeAPI.ChannelOpened`, and calls `view.Connect(raceId)`.
- **Form Closing (`RequestClose()`)**:
  - If `Channel.Race?.State == RaceState.Started` and `Channel.PersonalStatus == UserStatus.Racing`:
    - Shows confirmation warning dialog via `PlatformServices.Current.ShowMessage`:
      - Caption: `""`
      - Message: `"Do you want to FORFEIT before closing the window?"`
      - Buttons: `MessageBoxButtons.YesNoCancel`, Icon: `MessageBoxIcon.Warning`
      - If `Yes`: calls `Channel.Forfeit()`, proceeds to close.
      - If `No`: proceeds to close without forfeiting.
      - If `Cancel`: cancels close and keeps window open (returns `false`).
  - On close: calls `Dispose()` which disconnects the WebSocket, unhooks events, and cleans up race comparisons.
