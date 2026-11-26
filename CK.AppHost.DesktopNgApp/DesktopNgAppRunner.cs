using CK.AppHost.Desktop;
using CK.AppIdentity;
using CK.Core;
using CK.TypeScript;
using InfiniFrame;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;

namespace CK.AppHost.DesktopNgApp;

/// <summary>
/// The runner of a desktop application whose window shows one of its Angular client applications (the
/// <see cref="DesktopNgAppAspect"/>): the endpoint of the party is in process ("InProcess", no port is opened), the window
/// shows the client at <c>app://localhost/{RequestPath}</c> and relays the requests of its page
/// (<see cref="DesktopInProcessBridge"/>, the axios adapter of CK.Ng.AppHost.DesktopNgApp).
/// </summary>
public sealed class DesktopNgAppRunner : DesktopHostRunner
{
    // The bridge to the in-process endpoint: its handlers are registered with the window (they answer "starting..." until
    // the endpoint is started).
    readonly DesktopInProcessBridge _bridge = new();

    /// <summary>
    /// The configuration key that makes the endpoint of the party in process: the window relays the requests of its page.
    /// </summary>
    public const string InProcessKey = "CK-AppIdentity:Local:HttpEndpoint:InProcess";

    /// <summary>
    /// Sets the folder of the logs (see <see cref="DesktopHostRunner.ConfigureHost"/>) and makes the endpoint of the party
    /// in process: a desktop application opens no port.
    /// </summary>
    /// <param name="monitor">The monitor of the builder.</param>
    /// <param name="builder">The builder of the host.</param>
    public override void ConfigureHost( IActivityMonitor monitor, HostApplicationBuilder builder )
    {
        base.ConfigureHost( monitor, builder );
        builder.Configuration.AddInMemoryCollection( [new KeyValuePair<string, string?>( InProcessKey, "true" )] );
        monitor.Info( "The endpoint of the desktop application is in process: no port is opened." );
    }

    /// <summary>
    /// Reads the settings that the <see cref="DesktopNgAppAspect"/> projected (the defaults for what is missing).
    /// </summary>
    /// <param name="configuration">The configuration of the host.</param>
    /// <returns>The settings of the window.</returns>
    protected override DesktopWindowSettings GetWindowSettings( IConfiguration configuration ) => ReadSettings( configuration );

    internal static DesktopWindowSettings ReadSettings( IConfiguration configuration )
    {
        var s = configuration.GetSection( DesktopNgAppAspect.SectionName );
        var title = s["Title"];
        if( string.IsNullOrWhiteSpace( title ) ) title = s["Client"];
        return new DesktopWindowSettings( string.IsNullOrWhiteSpace( title ) ? "Application" : title,
                                          Size( s["Width"], DesktopWindowSettings.DefaultWidth ),
                                          Size( s["Height"], DesktopWindowSettings.DefaultHeight ) );

        static int Size( string? text, int defaultValue )
            => int.TryParse( text, NumberStyles.None, CultureInfo.InvariantCulture, out var v ) && v > 0 ? v : defaultValue;
    }

    /// <inheritdoc />
    protected override void ConfigureWindow( IInfiniFrameWindowBuilder builder )
    {
        builder.RegisterCustomSchemeHandler( DesktopInProcessBridge.Scheme, _bridge.HandleScheme )
               .RegisterWebMessagePostHandler( DesktopInProcessBridge.MessageId, _bridge.HandleMessage );
    }

    /// <summary>
    /// Gets the url of the client that the window shows: the <see cref="RunningHttpEndpoint.DefaultUrl"/> of the endpoint of
    /// the party followed by the request path of the client ("Hosting").
    /// </summary>
    /// <param name="services">The services of the started host.</param>
    /// <param name="url">The url, null on failure.</param>
    /// <returns>Null on success, the failure otherwise.</returns>
    internal static string? GetClientUrl( IServiceProvider services, out string? url )
    {
        url = null;
        var client = services.GetRequiredService<IConfiguration>()[$"{DesktopNgAppAspect.SectionName}:Client"];
        if( string.IsNullOrWhiteSpace( client ) )
        {
            return $"No \"{DesktopNgAppAspect.SectionName}:Client\" in the configuration: the \"DesktopNgApp\" aspect of the App names the client that the window shows.";
        }
        var c = services.GetRequiredService<ApplicationIdentityService>().GetFeature<PartyClientFeature>()?.Find( client );
        if( c == null ) return $"The client '{client}' that the desktop window shows is not a client of the party.";
        var hosting = c.GetFeature<HostingClientFeature>();
        if( hosting == null ) return $"The {c} is not served by the endpoint of the party: it has no \"Hosting\".";
        var root = services.GetRequiredService<HttpEndpointFeatureDriver>().Endpoint?.DefaultUrl;
        if( root == null ) return $"The {c} has no url: the endpoint of the party is not started.";
        var path = hosting.RequestPath.Trim( '/' );
        url = path.Length == 0 ? root.TrimEnd( '/' ) + '/' : $"{root.TrimEnd( '/' )}/{path}/";
        return null;
    }

    /// <inheritdoc />
    protected override async Task<string?> LoadAsync( IHost host, IInfiniFrameWindow window )
    {
        var failure = GetClientUrl( host.Services, out var url );
        if( failure != null ) return failure;
        ActivityMonitor.StaticLogger.Info( $"Desktop window on '{url}'." );
        var server = host.Services.GetRequiredService<HttpEndpointFeatureDriver>().Endpoint?.GetInProcessServer();
        if( server == null || !url!.StartsWith( DesktopInProcessBridge.Scheme + "://", StringComparison.OrdinalIgnoreCase ) )
        {
            return $"The endpoint's URL is '{url}': the desktop window serves only an in-process endpoint on \"{DesktopInProcessBridge.Scheme}://\" (\"InProcess\": true).";
        }
        _bridge.SetServer( server, url );
        var uri = new Uri( url );
        await LoadWhenReadyAsync( window,
                                  uri,
                                  async u => (await _bridge.GetAsync( server, u.PathAndQuery, default ).ConfigureAwait( false )).Response.StatusCode,
                                  DesktopInProcessBridge.Scheme ).ConfigureAwait( false );
        // The page asks its files to the bridge: a navigation that didn't reach it is requested again.
        for( int retry = 0; retry < 10 && !window.IsClosedOrClosing(); ++retry )
        {
            if( await _bridge.WaitForPageAsync( TimeSpan.FromSeconds( 2 ) ).ConfigureAwait( false ) ) break;
            ActivityMonitor.StaticLogger.Warn( $"The desktop window didn't load '{url}': it is loaded again." );
            Load( window, uri, DesktopInProcessBridge.Scheme );
        }
        return null;
    }
}
