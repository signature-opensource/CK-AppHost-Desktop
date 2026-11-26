# CK.Ng.AppHost.DesktopNgApp

The Angular side of a desktop application whose window shows an Angular client of the party: the commands of
CK.IO.AppHost.Desktop, the component that sends them, and the axios instance that talks to the in-process endpoint.

**This is the package that a deployment adds to an App** to make it a desktop application: it references its back-end,
[CK.AppHost.DesktopNgApp](../CK.AppHost.DesktopNgApp/README.md) (the "DesktopNgApp" aspect, the runner, the handlers of
the commands), that brings [CK.AppHost.Desktop](../CK.AppHost.Desktop/README.md).

## `ck-desktop-log-links`

Two links, for the header of an application:

- **Last log** closes the current text log of the application and opens it with the application of the system for
  ".log" files. GrandOutput closes the current file (`T-….log.tmp` becomes its final `….log`) and the next log line
  starts a new one: nothing is copied, nothing is left behind.
- **Open folder** opens the folder of the text logs with the file manager of the system.

The component asks the backend first (`IGetDesktopInfoQCommand`): it shows nothing when the application has no window
or no text log, and when the backend doesn't handle the command. An error of a link is shown as the title of the links
(they turn red).

It is a named component (`INgNamedComponent` of CK.TS.Angular): the shell of an application that is also run without
this package (in a browser, in Development) doesn't import it, it loads it by its name when the package is there:

```ts
import { NgComponentOutlet } from '@angular/common';
import { resolveNamedComponentTypeAsync } from '@local/ck-gen';

readonly desktopLinks = signal<Type<unknown> | null>( null );   // NgComponentOutlet renders nothing for null.
constructor() { resolveNamedComponentTypeAsync( 'desktop-log-links' )?.then( t => this.desktopLinks.set( t ) ); }
// <ng-container *ngComponentOutlet="desktopLinks()" />
```

## The requests of a page without port

The package provides the axios instance of the application (the `AXIOS` token of CK.Ng.Axios: its provider comes after
the one of CK.Ng.Axios, it wins) with [`createDesktopAxios`](Res/desktop-axios.ts): when the page is served by the
`app` scheme of the desktop window (the in-process endpoint, see
[CK.AppHost.DesktopNgApp](../CK.AppHost.DesktopNgApp/README.md#the-in-process-bridge)), its adapter
(`desktopAxiosAdapter`) sends each request as a message to the host instead of HTTP: the `HttpCrisEndpoint` and every
component work as they are. Only text bodies (JSON) are sent; a binary response is an `ArrayBuffer`. Elsewhere (a
browser, a Kestrel endpoint) it is a plain `axios.create()`.
