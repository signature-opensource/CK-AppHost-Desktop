# CK-AppHost-Desktop

A CKomposable application as a desktop application: a native window shows one of its client applications.

| Project | Description |
|---|---|
| [CK.IO.AppHost.Desktop](CK.IO.AppHost.Desktop/README.md) | The commands of a desktop application: its information, its last log, its log folder. |
| [CK.AppHost.Desktop](CK.AppHost.Desktop/README.md) | The runner of a desktop application (`DesktopHostRunner`): the native window, its failures, the logs of a published application, a `WinExe` host. A package specializes it with what the window shows. |
| [CK.AppHost.DesktopNgApp](CK.AppHost.DesktopNgApp/README.md) | A desktop application whose window shows an Angular client of the party: the "DesktopNgApp" option of a client's "NgApp", the runner (an in-process endpoint, no port) and the handlers of the desktop commands. |
| [CK.Ng.AppHost.DesktopNgApp](CK.Ng.AppHost.DesktopNgApp/README.md) | Its Angular side, the package that a deployment adds to an App: the "Last log" and "Open folder" links, the axios instance of the in-process endpoint. |
