# CK.AppHost.Desktop

The runner of a desktop application: a native window ([InfiniFrame](https://github.com/InfiniLore/InfiniFrame)) shows
what the App wants to show, and the window and the host live and die together. A desktop application is one process with
one window, installed on its own: it embeds its host.

This package knows nothing about what the window shows: it is the base of a runner (`DesktopHostRunner`) that a package
specializes. [CK.AppHost.DesktopNgApp](../CK.AppHost.DesktopNgApp/README.md) is one: the window shows an Angular client
application of the party.

## The runner

`DesktopHostRunner` is an `IAppHostRunner` of [CK.AppHost](https://github.com/signature-opensource/CK-AspNet/blob/stable/CK.AppHost/README.md):
when it is in the App, `CKomposableAppHost.Run` lets it run the host. The `Program.cs` of the host doesn't change.
It is a definer (`[CKTypeDefiner]`): its specialization is the real object (an App has at most one runner).

A specialization says what the window shows:

| Member | What it does |
|---|---|
| `DesktopHostRunner.GetWindowSettings` | The title and the size of the window (`DesktopWindowSettings`), read from the configuration before the host starts: the window opens at once. |
| `DesktopHostRunner.ConfigureWindow` | Registers what the window needs before it is built (a custom scheme, the handlers of the messages of its page). |
| `DesktopHostRunner.LoadAsync` | Loads the content once the host is started (`DesktopHostRunner.LoadWhenReadyAsync`, `DesktopHostRunner.Load`), or returns the message of a failure. |
| `DesktopHostRunner.ConfigureHost` | Configures the builder of the host (the base sets the folder of the logs: an override calls it). |

What the runner does:

- **The window opens at once** on a waiting page, the host starts in the background, then the content is loaded. A url
  is loaded once it answers without a server error (a dev server compiles before it answers; after 3 minutes the url
  is loaded anyway), and its origin is trusted then (InfiniFrame only navigates to a trusted origin).
- **A failure is shown in the window:** the errors logged while the host started (the actual causes: a setup error only
  says that a feature failed), where the logs are, and the exception. The window stays open until it is closed. A host
  that fails to build shows its failure in a window of its own (`IAppHostRunner.OnBuildFailure`), when a window can be
  opened.
- **Closing the window stops the host; stopping the host closes the window.** The exit code is 0, or 1 on failure.
- **The browser's context menu** (Back, Reload, Save as...) is off in a published host (`DesktopHostRunner.IsPublished`:
  its `CK.AppHost.Published.json` is next to it): the client shows its own menus. In Development, it is kept (Inspect).
- **The window thread:** the window runs on the thread of `CKomposableAppHost.Run` (the main thread, that macOS
  requires), except on Windows where it runs on an STA thread of its own (WebView2 requires an STA thread, and the main
  thread of a top-level `Program.cs` is not one).
- `DesktopHostRunner.Window` is the window once it is opened: the commands of a desktop application use it (a dialog).

**Tested on Windows (WebView2) only:** macOS (WKWebView) and Linux (WebKitGTK) are untested.

## The installed application

- **No console:** a host whose App has this package is a `WinExe`. The package's `buildTransitive` targets change the
  `Exe` of a host project (CK.AppHost sets `CKIsAppHost` in it) into `WinExe`; a Builder that references the same App
  keeps its console. Its logs are files: the console handler of the GrandOutput is off in its deployment.
- **The logs of a published host** are in the user's application data: `DesktopHostRunner.ConfigureHost` sets
  `LogFile.RootLogPath` to `{LocalApplicationData}/{Domain}-{Party}/Logs` (`DesktopHostRunner.GetLogFolderName`, from
  the full name of its deployment: "CK/$Configurator/#Production" gives "CK-Configurator"; the name of the host without
  ".Host" otherwise), unless it is already set. Its folder may be read only (an installed application) or temporary (the
  extraction folder of a single file application). The other files that it writes are in the user's application data
  too, never next to the executable.
- Two launches are two independent applications: what they share on disk must be safe under concurrency.

## Where it is

The package references CK.AppHost for its code only (`ExcludeAssets="build"`: the build targets of CK.AppHost are the
conventions of a host project). It is not added to an App by hand: the package of a kind of desktop application brings
it, and a deployment adds that package to the App (the App of the solution stays a web application, run in a browser
in Development).
