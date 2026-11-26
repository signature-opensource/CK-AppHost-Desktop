using CK.AppHost.DesktopNgApp;
using CK.Core;
using CK.TS.Angular;
using System.Linq;

[assembly: CK.Setup.AppBuilderContributor( typeof( CK.Setup.DesktopNgAppAppBuilderContributor ) )]

namespace CK.Setup;

/// <summary>
/// The build of a desktop application: the App whose client enables the <see cref="DesktopNgAppOption"/> (the "DesktopNgApp"
/// option of its "NgApp" feature) has an in-process endpoint: <c>"HttpEndpoint": { "InProcess": true }</c> is projected
/// in its party (the packaging implies it: it is never written in a configuration). The window shows one client: two
/// clients of an App with the option are an error.
/// </summary>
public sealed class DesktopNgAppAppBuilderContributor : IAppBuilderContributor
{
    /// <summary>
    /// The path of the key, in the host's configuration, that makes the endpoint of the party in process.
    /// </summary>
    public const string InProcessPath = $"{AppIdentityAppBuilderContributor.SectionName}:Local:HttpEndpoint:InProcess";

    readonly AppIdentityAppBuilderContributor _appIdentity;

    /// <summary>
    /// Initializes a new contributor.
    /// </summary>
    /// <param name="appIdentity">The parties and their declared clients.</param>
    public DesktopNgAppAppBuilderContributor( AppIdentityAppBuilderContributor appIdentity ) => _appIdentity = appIdentity;

    /// <inheritdoc />
    public bool Contribute( IActivityMonitor monitor, AppBuilderContext context )
    {
        // The feature that owns the option: "NgApp".
        var featureName = typeof( NgAppClientFeature ).GetCustomAttributes( inherit: false ).OfType<IConfigurableTypeAttribute>().Single().Name;
        bool success = true;
        foreach( var app in _appIdentity.Clients.Where( c => c.IsOptionEnabled( featureName, typeof( DesktopNgAppOption ) ) ).GroupBy( c => c.App ) )
        {
            var clients = app.ToList();
            if( clients.Count > 1 )
            {
                monitor.Error( $"The clients '{clients.Select( c => c.Name ).Concatenate( "', '" )}' of {app.Key} enable the \"DesktopNgApp\" option: the desktop window shows one client." );
                success = false;
                continue;
            }
            app.Key.ProjectValue( InProcessPath, "true" );
            monitor.Info( $"The endpoint of {app.Key} is in process (the desktop window shows its client '{clients[0].Name}'): no port is opened." );
        }
        return success;
    }
}
