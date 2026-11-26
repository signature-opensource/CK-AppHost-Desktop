# CK.AppHost.DesktopNgApp.Engine

The engine of [CK.AppHost.DesktopNgApp](../CK.AppHost.DesktopNgApp/README.md) (its `[assembly: RequiredEngine]`): the
build of a desktop application.

A desktop application opens no port: its endpoint is in process (the "InProcess" option of the "HttpEndpoint" feature,
[CK.AppIdentity.HttpEndpoint.InProcess](https://github.com/signature-opensource/CK-AspNet/blob/stable/CK.AppIdentity.HttpEndpoint.InProcess/README.md)).
The packaging implies it: it is never written in a configuration.

`DesktopNgAppAppBuilderContributor` (an App builder contributor) finds the clients whose "NgApp" feature enables the
"DesktopNgApp" option (`AppIdentityAppBuilderContributor.DeclaredClient.IsOptionEnabled`) and:

- projects `"CK-AppIdentity:Local:HttpEndpoint:InProcess": true` in the host's configuration of their App
  (`DesktopNgAppAppBuilderContributor.InProcessPath`);
- refuses two clients of an App with the option: the window shows one client.
