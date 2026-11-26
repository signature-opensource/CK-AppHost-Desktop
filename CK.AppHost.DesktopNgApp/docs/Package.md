A CKomposable application as a desktop application whose window shows one of its Angular client applications, with no port opened.

The "DesktopNgApp" option of the "NgApp" feature of a client (the client that the window shows, its title and size) and
its runner (`DesktopNgAppRunner`): the endpoint of the party is in process, the window relays the requests of its page
(`app://localhost`), and the commands of the desktop (the logs, the folder dialog) are handled. A deployment adds
CK.Ng.AppHost.DesktopNgApp to the App, which brings this package.

See the [README](https://github.com/signature-opensource/CK-AppHost-Desktop/blob/stable/CK.AppHost.DesktopNgApp/README.md).
