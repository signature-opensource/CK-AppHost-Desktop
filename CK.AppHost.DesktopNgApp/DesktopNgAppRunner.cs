using CK.AppHost.Desktop;
using CK.AppIdentity;
using CK.Core;
using CK.TS.Angular;
using CK.TypeScript;
using InfiniFrame;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CK.AppHost.DesktopNgApp;

/// <summary>
/// The runner of a desktop application whose window shows one of its Angular client applications (the client whose
/// "NgApp" enables the <see cref="DesktopNgAppOption"/>): the endpoint of the party is in process ("InProcess", no port is
/// opened: the builder sets it), the window shows the client at <c>app://localhost/{RequestPath}</c> and relays the
/// requests of its page (<see cref="DesktopInProcessBridge"/>, the axios adapter of CK.Ng.AppHost.DesktopNgApp).
/// </summary>
public sealed class DesktopNgAppRunner : DesktopHostRunner
{
    // The bridge to the in-process endpoint: its handlers are registered with the window (they answer "starting..." until
    // the endpoint is started).
    readonly DesktopInProcessBridge _bridge = new();


    /// <summary>
    /// Reads the settings of the window before the host starts: the model of the <see cref="DesktopNgAppOption"/> of the
    /// client that enables it (the defaults when there is no such client: the failure is shown once the host is started).
    /// </summary>
    /// <param name="services">The services of the built host (not started).</param>
    /// <returns>The settings of the window.</returns>
    protected override DesktopWindowSettings GetWindowSettings( IServiceProvider services )
    {
        var monitor = new ActivityMonitor( "Reading the settings of the desktop window." );
        try
        {
            return ReadSettings( monitor, services );
        }
        finally
        {
            monitor.MonitorEnd();
        }
    }

    internal static DesktopWindowSettings ReadSettings( IActivityMonitor monitor, IServiceProvider services )
    {
        var driver = services.GetService<PartyClientFeatureDriver>();
        if( driver != null
            && driver.TryFindOptionModels( monitor, typeof( DesktopNgAppOption ), out var found )
            && found.Count == 1
            && found[0].Model is DesktopNgAppConfiguration c )
        {
            return new DesktopWindowSettings( string.IsNullOrWhiteSpace( c.Title ) ? found[0].ClientName : c.Title, c.Width, c.Height );
        }
        return new DesktopWindowSettings( "Application", DesktopWindowSettings.DefaultWidth, DesktopWindowSettings.DefaultHeight );
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
        var clients = services.GetRequiredService<ApplicationIdentityService>().GetFeature<PartyClientFeature>()?.Clients
                              .Where( c => c.GetFeature<NgAppClientFeature>()?.GetOption<DesktopNgAppOption>() != null )
                              .ToList() ?? [];
        if( clients.Count == 0 )
        {
            return "No client of the party enables the \"DesktopNgApp\" option of its \"NgApp\" feature: the desktop window has nothing to show.";
        }
        if( clients.Count > 1 )
        {
            return $"The clients '{clients.Select( c => c.Name ).Concatenate( "', '" )}' enable the \"DesktopNgApp\" option: the desktop window shows one client.";
        }
        var client = clients[0];
        var hosting = client.GetFeature<HostingClientFeature>();
        if( hosting == null ) return $"The {client} is not served by the endpoint of the party: it has no \"Hosting\".";
        var root = services.GetRequiredService<HttpEndpointFeatureDriver>().Endpoint?.DefaultUrl;
        if( root == null ) return $"The {client} has no url: the endpoint of the party is not started.";
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
