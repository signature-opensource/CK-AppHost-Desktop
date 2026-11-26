The Angular side of a CKomposable desktop application whose window shows an Angular client: the package that a deployment adds to an App to make it a desktop application.

It brings its back-end (CK.AppHost.DesktopNgApp: the "DesktopNgApp" aspect and the runner). The `ck-desktop-log-links`
component shows the "Last log" and "Open folder" links when the application runs with its window and writes a text
log; its axios instance (`createDesktopAxios`) sends the requests as messages to the window when the page is served by
the in-process endpoint (`app://localhost`).

See the [README](https://github.com/signature-opensource/CK-AppHost-Desktop/blob/stable/CK.Ng.AppHost.DesktopNgApp/README.md).
