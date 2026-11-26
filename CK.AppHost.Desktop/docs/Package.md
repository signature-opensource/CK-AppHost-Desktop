The runner of a CKomposable desktop application: a native window (InfiniFrame) shows what the App wants to show, and closing the window stops the host.

The base of a runner (`DesktopHostRunner`, an `IAppHostRunner` of CK.AppHost) that a package specializes with what the
window shows; it shows the failures of the host in the window, sets the folder of the logs of a published application,
and makes the host a `WinExe`.

See the [README](https://github.com/signature-opensource/CK-AppHost-Desktop/blob/stable/CK.AppHost.Desktop/README.md).
