# CK.AppHost.DesktopNgApp

A CKomposable application as a desktop application whose window shows one of its Angular client applications, with no
port opened: the endpoint of the party is in process and the window relays the requests of its page.

The App of the solution is a web application (its endpoint on a port, its client in a browser, the live reload of its
dev server): being a desktop application is a packaging, applied by a deployment. The deployment adds
[CK.Ng.AppHost.DesktopNgApp](../CK.Ng.AppHost.DesktopNgApp/README.md) to the App (it brings this package) and
enables the "DesktopNgApp" option of the "NgApp" feature of the client that the window shows:

```jsonc
// Deployment/#Production/ckonf.json
"Deployment": { "Apps": { "$Configurator": { "Packages": [ "CK.Ng.AppHost.DesktopNgApp" ] } } },
"AppContexts": { "Configurator": { "App": { "Local": { "PartyClient": { "App": { "NgApp": { "DesktopNgApp": { "Title": "CK Configurator" } } } } } } } }
```

(See "The packages that a deployment adds to an App" in the README of
[CK.AppDeployment](https://github.com/signature-opensource/CK-AppBuilder/blob/stable/CK.AppDeployment/README.md).)

## The "DesktopNgApp" option

An optional option of the "NgApp" client feature (`DesktopNgAppOption`, an `IPartyComponentOption<NgAppClientFeature>`):
the client whose "NgApp" enables it is the one that the window shows. It requires the "Hosting" feature of the client
(the endpoint of the party serves it). Its model, `DesktopNgAppConfiguration`:

| Key | |
|---|---|
| `Title` | The title of the window: the client name by default. |
| `Width`, `Height` | The size of the window: 1280x800 by default. |

The window shows one client: two clients of an App with the option are a build error. The build makes the endpoint of
the party in process ([CK.AppHost.DesktopNgApp.Engine](../CK.AppHost.DesktopNgApp.Engine/README.md)).

## The runner

`DesktopNgAppRunner` specializes the runner of [CK.AppHost.Desktop](../CK.AppHost.Desktop/README.md):

- **The window opens before the host starts**, with the title and the size of the option: they are read from the
  configuration before the party is set up (`PartyClientFeatureDriver.TryFindOptionModels`, the option's model).
- **The endpoint is in process** (the "InProcess" option of
  [CK.AppIdentity.HttpEndpoint.InProcess](https://github.com/signature-opensource/CK-AspNet/blob/stable/CK.AppIdentity.HttpEndpoint.InProcess/README.md),
  projected by the build): the web application of the party runs on an in-memory server, no port is opened, nothing
  outside the window can call it.
- **The window shows the client** at `app://localhost/` followed by the `RequestPath` of the client: the
  `RunningHttpEndpoint.DefaultUrl` of the endpoint and the "Hosting" feature of the client. No client with the option,
  an endpoint that is not in process are shown in the window.

## The in-process bridge

The window relays the requests of its page to the in-memory server ([`DesktopInProcessBridge`](DesktopInProcessBridge.cs)):

- **The files of the page** (the GET of the `app` scheme) are asked to the in-memory server. `app` is the only custom
  scheme that InfiniFrame registers as a secure origin with an authority (`app://localhost`): any other name gives the
  page a `null` origin (no `fetch`, no `localStorage`). InfiniFrame's handler receives the url only and returns a body
  and a content type: the request has the `Accept` of a navigation for a document (no extension or `.html`: a dev server
  serves its index to it), an error is a text (a response without content type would be a download for the window).
- **The other requests** (the API: Cris) are messages: the axios adapter of
  [CK.Ng.AppHost.DesktopNgApp](../CK.Ng.AppHost.DesktopNgApp/README.md) posts `{ id, method, url, headers, body }`
  (envelope id `"ck:http"`), the bridge runs it and answers `{ id, status, headers, body, base64 }` (a text or JSON body
  as is, any other as base64). The requests have `Origin` (the url of the server) and `Sec-Fetch-Site: same-origin`:
  only the page of the window sends them. The cookies that the server sets are kept by the bridge.
- **The sizes:** InfiniFrame caps a message from the page at 1 MiB (a request); the responses are read without its
  dispatcher (no cap).
- A navigation that didn't reach the bridge (the page asked no file) is requested again.

## The commands of the desktop

`DesktopCommandHandlers` handles the commands of [CK.IO.AppHost.Desktop](../CK.IO.AppHost.Desktop/README.md) (their
client side is [CK.Ng.AppHost.DesktopNgApp](../CK.Ng.AppHost.DesktopNgApp/README.md)):

- `IGetDesktopInfoQCommand`: whether the host runs with its window, and whether the GrandOutput has a "TextFile"
  handler.
- `IOpenLastLogCommand`: a `GrandOutputHandlersAction` closes the current file of the "TextFile" handler (on the
  dispatcher of the GrandOutput, after the entries already sent: the file always contains the command's own log line).
  `T-….log.tmp` becomes its final `….log` and the next log line starts a new file: nothing is copied. The closed file
  is opened with the application of the system for ".log" files.
- `IOpenLogFolderCommand`: opens the folder of the "TextFile" handler (under `LogFile.RootLogPath`).
- `IPickFolderCommand`: shows the folder dialog of the system in the window (InfiniFrame's `FilePickerDialogs`).

Files are opened with the shell on Windows, `open` on macOS, `xdg-open` on Linux. The commands fail without the window
(`DesktopHostRunner.Window`): a server never opens files on its machine.
