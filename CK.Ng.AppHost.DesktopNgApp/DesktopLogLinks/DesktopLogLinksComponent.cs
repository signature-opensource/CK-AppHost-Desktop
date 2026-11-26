using CK.Core;
using CK.TS.Angular;

namespace CK.Ng.AppHost.DesktopNgApp;

/// <summary>
/// The "Last log" and "Open folder" links of a desktop application (<c>ck-desktop-log-links</c>): shown only when the
/// application runs with its window and writes a text log. "Last log" closes the current text log and opens it,
/// "Open folder" opens the folder of the text logs.
/// <para>
/// It is a named component (<c>desktop-log-links</c>): a shell that is also run without this package (a web application
/// in a browser, packaged as a desktop application by a deployment) loads it by its name, with
/// <c>resolveNamedComponentTypeAsync( 'desktop-log-links' )</c> of CK.TS.Angular, instead of importing it.
/// </para>
/// </summary>
[NgComponent]
[Package<NgAppHostDesktopNgAppPackage>]
public sealed class DesktopLogLinksComponent : NgComponent, INgNamedComponent
{
}
