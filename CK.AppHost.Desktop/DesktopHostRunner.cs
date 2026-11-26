using CK.AppHost.Desktop;
using CK.Setup;
using InfiniFrame;
using InfiniFrame.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace CK.Core;

/// <summary>
/// The runner of a desktop application (<see cref="IAppHostRunner"/>): a native window (InfiniFrame) shows what a
/// specialization loads in it. The window opens at once on a waiting page, the host starts in the background, then
/// <see cref="LoadAsync"/> loads the content. A failure of the host (an invalid configuration, a setup error...) is shown
/// in the window. Closing the window stops the host; stopping the host closes the window.
/// <para>
/// This is a definer: a package specializes it with what the window shows and how (CK.AppHost.DesktopNgApp: an Angular
/// application of the party). An App has at most one runner.
/// </para>
/// <para>
/// The window runs on the thread of <see cref="CKomposableAppHost.Run(string[])"/> (the main thread of the process, that
/// macOS requires), except on Windows where it runs on an STA thread of its own (WebView2 requires an STA thread, and the
/// main thread of a top-level <c>Program.cs</c> is not one).
/// </para>
/// </summary>
[CKTypeDefiner]
public abstract class DesktopHostRunner : IAppHostRunner
{
    volatile IInfiniFrameWindow? _window;
    // The client is loaded once the start page is loaded (it posts a message): a navigation requested before (before the
    // window is created, or while its start page loads) is lost or replaced by the start page.
    readonly TaskCompletionSource _startPageLoaded = new( TaskCreationOptions.RunContinuationsAsynchronously );

    /// <summary>
    /// Gets the window of the application, null until it is opened (and for a host that is built but not run by this
    /// runner: a test).
    /// </summary>
    public IInfiniFrameWindow? Window => _window;

    /// <summary>
    /// Gets whether this host is published (its <see cref="CKomposableAppHost.PublishedFileName"/> is next to it), not run
    /// from its project in Development. The browser's context menu (Back, Reload, Save as...) is off when it is published:
    /// the client shows its own menus.
    /// </summary>
    public static bool IsPublished => File.Exists( Path.Combine( AppContext.BaseDirectory, CKomposableAppHost.PublishedFileName ) );

    /// <summary>
    /// Sets the folder of the logs of a published desktop application (see <see cref="GetPublishedLogPath"/>) before the
    /// monitoring is configured. A specialization that overrides this calls it.
    /// </summary>
    /// <param name="monitor">The monitor of the builder.</param>
    /// <param name="builder">The builder of the host.</param>
    public virtual void ConfigureHost( IActivityMonitor monitor, HostApplicationBuilder builder )
    {
        if( LogFile.RootLogPath == null && IsPublished ) LogFile.RootLogPath = GetPublishedLogPath( monitor );
    }

    /// <summary>
    /// The logs of a published desktop application are in the user's application data: its folder may be read only (an
    /// installed application) or temporary (the extraction folder of a single file application).
    /// <c>{LocalApplicationData}/{Domain}-{Party}/Logs</c>, from the full name of its deployment ("CK/$Configurator/#Production"
    /// gives "CK-Configurator"), or the name of the host without ".Host".
    /// </summary>
    /// <param name="monitor">The monitor to use.</param>
    /// <returns>The folder of the logs.</returns>
    static string GetPublishedLogPath( IActivityMonitor monitor )
    {
        string? name = null;
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse( File.ReadAllText( Path.Combine( AppContext.BaseDirectory, CKomposableAppHost.PublishedFileName ) ) );
            if( doc.RootElement.TryGetProperty( "Deployment", out var d )
                && d.ValueKind == System.Text.Json.JsonValueKind.Object
                && d.TryGetProperty( "FullName", out var f )
                && f.GetString() is { Length: > 0 } fullName )
            {
                name = GetLogFolderName( fullName );
            }
        }
        catch( Exception ex )
        {
            monitor.Warn( $"Unable to read '{CKomposableAppHost.PublishedFileName}'.", ex );
        }
        if( string.IsNullOrEmpty( name ) )
        {
            name = System.Reflection.Assembly.GetEntryAssembly()?.GetName().Name ?? "CKomposable";
            if( name.EndsWith( ".Host", StringComparison.OrdinalIgnoreCase ) ) name = name[..^5];
        }
        return Path.Combine( Environment.GetFolderPath( Environment.SpecialFolder.LocalApplicationData ), name, "Logs" );
    }

    /// <summary>
    /// Gets the name of the folder of a published desktop application from the full name of its party: the segments
    /// without the environment, without '$' ("CK/$Configurator/#Production" gives "CK-Configurator").
    /// </summary>
    /// <param name="fullName">The full name of the party.</param>
    /// <returns>The name of the folder.</returns>
    public static string GetLogFolderName( string fullName )
    {
        return string.Join( '-', fullName.Split( '/' ).Where( s => !s.StartsWith( '#' ) ).Select( s => s.TrimStart( '$' ) ) );
    }

    /// <summary>
    /// Gets the title and the size of the window, before the host starts (the window opens at once).
    /// </summary>
    /// <param name="services">The services of the built host (it is not started).</param>
    /// <returns>The settings of the window.</returns>
    protected abstract DesktopWindowSettings GetWindowSettings( IServiceProvider services );

    /// <summary>
    /// Registers what the window needs (a custom scheme, the handlers of messages of the page) before it is built. Does
    /// nothing by default.
    /// </summary>
    /// <param name="builder">The builder of the window.</param>
    protected virtual void ConfigureWindow( IInfiniFrameWindowBuilder builder )
    {
    }

    /// <summary>
    /// Loads the content of the window once the host is started and the start page is loaded (see
    /// <see cref="Load(IInfiniFrameWindow, Uri, string?)"/>). A failure is reported by an exception or a message.
    /// </summary>
    /// <param name="host">The started host.</param>
    /// <param name="window">The window.</param>
    /// <returns>Null on success, the message of a failure otherwise (it is shown in the window).</returns>
    protected abstract Task<string?> LoadAsync( IHost host, IInfiniFrameWindow window );

    /// <inheritdoc />
    public int Run( IHost host )
    {
        var settings = GetWindowSettings( host.Services );
        int exitCode = 0;
        Task? hostTask = null;
        void RunWindow()
        {
            var builder = InfiniFrameWindowBuilder.Create()
                            .SetTitle( settings.Title )
                            .SetSize( settings.Width, settings.Height )
                            // The browser's menu (Back, Reload, Save as...) is not the one of an application: the client
                            // shows its own menus. It is kept in Development (Inspect).
                            .EnableContextMenu( !IsPublished )
                            .SetStartPageContent( Page( settings.Title, "starting...", null, readyMessageId: _startPageLoadedId ) )
                            .RegisterWebMessagePostHandler( _startPageLoadedId, ( IInfiniFrameWindow w, string? payload ) => _startPageLoaded.TrySetResult() );
            ConfigureWindow( builder );
            var window = builder.Build();
            _window = window;
            hostTask = Task.Run( async () =>
            {
                if( !await RunHostAsync( host, window, settings ).ConfigureAwait( false ) ) Interlocked.Exchange( ref exitCode, 1 );
            } );
            window.WaitForClose();
        }
        try
        {
            if( OperatingSystem.IsWindows() )
            {
                var t = new Thread( RunWindow ) { Name = "DesktopWindow" };
                t.SetApartmentState( ApartmentState.STA );
                t.Start();
                t.Join();
            }
            else
            {
                RunWindow();
            }
        }
        catch( Exception ex )
        {
            ReportFailure( ex );
            exitCode = 1;
        }
        // The window is closed: the host stops (nothing happens when it is already stopped).
        host.Services.GetService<IHostApplicationLifetime>()?.StopApplication();
        if( hostTask != null && !hostTask.Wait( TimeSpan.FromSeconds( 30 ) ) )
        {
            ActivityMonitor.StaticLogger.Warn( "The host didn't stop in 30 seconds after the desktop window was closed." );
        }
        return exitCode;
    }

    /// <summary>
    /// A desktop application has no console: the failure to build the host is shown in a window (if a window can be
    /// opened).
    /// </summary>
    /// <param name="ex">The failure.</param>
    public void OnBuildFailure( Exception ex ) => TryShowFailure( "The application failed to start", ex );

    // Starts the host, loads the content in the window, and waits for the host to stop. False on failure (it is shown in
    // the window that stays open until it is closed).
    async Task<bool> RunHostAsync( IHost host, IInfiniFrameWindow window, DesktopWindowSettings settings )
    {
        // The errors logged while the host starts: shown in the window on failure (the exception only says that a setup
        // failed). The monitors created while it starts collect them.
        var errors = new ConcurrentQueue<string>();
        Action<IActivityMonitor> collect = m => m.Output.RegisterClient( new ErrorCollector( errors ) );
        ActivityMonitor.AutoConfiguration += collect;
        try
        {
            try
            {
                await host.StartAsync().ConfigureAwait( false );
            }
            finally
            {
                ActivityMonitor.AutoConfiguration -= collect;
            }
            var lifetime = host.Services.GetRequiredService<IHostApplicationLifetime>();
            // Stopping the host closes the window.
            lifetime.ApplicationStopping.Register( () =>
            {
                if( !window.IsClosedOrClosing() ) window.Close();
            } );
            await WaitForStartPageAsync().ConfigureAwait( false );
            var failure = await LoadAsync( host, window ).ConfigureAwait( false );
            if( failure != null )
            {
                ActivityMonitor.StaticLogger.Error( failure );
                await ShowFailureAsync( window, settings.Title, failure, [], null ).ConfigureAwait( false );
                return false;
            }
            await host.WaitForShutdownAsync().ConfigureAwait( false );
            return true;
        }
        catch( Exception ex )
        {
            ReportFailure( ex );
            await ShowFailureAsync( window, settings.Title, "The application failed to start.", errors, ex ).ConfigureAwait( false );
            return false;
        }
    }

    /// <summary>
    /// Navigates the window to a url. InfiniFrame only navigates to a trusted origin (a refused navigation is silent): the
    /// window opened on a page without origin, the one of the url (its port is known once the host is started) is trusted
    /// now.
    /// </summary>
    /// <param name="window">The window.</param>
    /// <param name="url">The url to load.</param>
    /// <param name="customScheme">A custom scheme to allow (the url's scheme, when it is not http or https).</param>
    protected static void Load( IInfiniFrameWindow window, Uri url, string? customScheme = null )
    {
        try
        {
            var policy = InfiniFrameUriSecurityPolicyRegistry.GetForWindow( window );
            var builder = new InfiniFrameUriSecurityPolicyBuilder( policy ).AddTrustedOrigin( url );
            if( customScheme != null ) builder.AllowNavigationScheme( customScheme );
            InfiniFrameUriSecurityPolicyRegistry.BindToWindow( window, builder.Build() );
            window.Load( url );
        }
        catch( Exception ex )
        {
            ActivityMonitor.StaticLogger.Error( $"While loading '{url}' in the desktop window.", ex );
        }
    }

    /// <summary>
    /// Loads a url once it answers (anything but a server error: a forwarder answers 502 while the dev server of a client
    /// in Development is not listening). After 3 minutes, the url is loaded anyway: the window shows what it answers.
    /// </summary>
    /// <param name="window">The window.</param>
    /// <param name="url">The url to load.</param>
    /// <param name="getStatus">Gets the status code of the url.</param>
    /// <param name="customScheme">A custom scheme to allow (see <see cref="Load(IInfiniFrameWindow, Uri, string?)"/>).</param>
    /// <returns>The awaitable.</returns>
    protected static async Task LoadWhenReadyAsync( IInfiniFrameWindow window, Uri url, Func<Uri, Task<int>> getStatus, string? customScheme = null )
    {
        try
        {
            var limit = DateTime.UtcNow + _readyDelay;
            while( !window.IsClosedOrClosing() && DateTime.UtcNow < limit )
            {
                try
                {
                    if( await getStatus( url ).ConfigureAwait( false ) < 500 ) break;
                }
                catch( System.Net.Http.HttpRequestException ) { }
                catch( TaskCanceledException ) { }
                await Task.Delay( 500 ).ConfigureAwait( false );
            }
            if( window.IsClosedOrClosing() ) return;
            Load( window, url, customScheme );
        }
        catch( Exception ex )
        {
            ActivityMonitor.StaticLogger.Error( $"While loading '{url}' in the desktop window.", ex );
        }
    }

    static readonly TimeSpan _readyDelay = TimeSpan.FromMinutes( 3 );

    const string _startPageLoadedId = "ck:start-page-loaded";

    async Task WaitForStartPageAsync()
    {
        try
        {
            await _startPageLoaded.Task.WaitAsync( TimeSpan.FromSeconds( 30 ) ).ConfigureAwait( false );
        }
        catch( TimeoutException )
        {
            ActivityMonitor.StaticLogger.Warn( "The start page of the desktop window is not loaded after 30 seconds: the content is loaded anyway." );
        }
    }

    static async Task ShowFailureAsync( IInfiniFrameWindow window, string title, string message, IEnumerable<string> errors, Exception? ex )
    {
        try
        {
            // The host may fail before the window is ready: a page loaded before is lost.
            await window.WaitForReadyAsync().ConfigureAwait( false );
            if( window.IsClosedOrClosing() ) return;
            var result = await window.LoadRawStringAsync( Page( title, message, ex, errors ) ).ConfigureAwait( false );
            ActivityMonitor.StaticLogger.Trace( $"The failure is shown in the desktop window: {result}." );
        }
        catch( Exception e )
        {
            ActivityMonitor.StaticLogger.Error( "While showing the failure in the desktop window.", e );
        }
    }

    // A window only for the failure (the host was not built): nothing more when no window can be opened.
    static void TryShowFailure( string message, Exception ex )
    {
        void Show()
        {
            InfiniFrameWindowBuilder.Create()
                .SetTitle( "Error" )
                .SetSize( 900, 600 )
                .SetStartPageContent( Page( "Error", message, ex ) )
                .Build()
                .WaitForClose();
        }
        try
        {
            if( OperatingSystem.IsWindows() )
            {
                var t = new Thread( Show ) { Name = "DesktopWindow" };
                t.SetApartmentState( ApartmentState.STA );
                t.Start();
                t.Join();
            }
            else Show();
        }
        catch
        {
            // No window: the standard error and the logs say it.
        }
    }

    // The waiting page and the failure page.
    static string Page( string title, string message, Exception? ex, IEnumerable<string>? errors = null, string? readyMessageId = null )
    {
        // The page tells the host that it is loaded: an InfiniFrame envelope posted to the WebView (WebView2 or WebKit).
        var ready = readyMessageId == null
                    ? ""
                    : $$$"""
                        <script>(function(){var e=JSON.stringify({id:'{{{readyMessageId}}}',command:'Post',version:2,data:''});
                        try{if(window.chrome&&window.chrome.webview){window.chrome.webview.postMessage(e);}
                        else if(window.webkit&&window.webkit.messageHandlers&&window.webkit.messageHandlers.infiniFrameInterop){window.webkit.messageHandlers.infiniFrameInterop.postMessage(e);}}catch(x){}})();</script>
                        """;
        var list = errors == null ? "" : string.Concat( errors.Distinct().Select( e => $"<li>{WebUtility.HtmlEncode( e )}</li>" ) );
        if( list.Length > 0 ) list = $"<ul class=\"errors\">{list}</ul>";
        var t = WebUtility.HtmlEncode( title );
        var m = WebUtility.HtmlEncode( message );
        var details = ex == null ? "" : $"<pre>{WebUtility.HtmlEncode( ex.ToString() )}</pre>";
        var logs = ex == null || LogFile.RootLogPath == null
                    ? ""
                    : $"<p>The logs are in <code>{WebUtility.HtmlEncode( LogFile.RootLogPath )}</code>.</p>";
        var style = ex == null
                    ? "display:flex;height:100vh;margin:0;align-items:center;justify-content:center;color:#555"
                    : "margin:24px;color:#333";
        return $$"""
            <!DOCTYPE html>
            <html><head><meta charset="utf-8"><title>{{t}}</title>
            <style>body{font-family:Segoe UI,sans-serif;{{style}}} pre{white-space:pre-wrap;font-size:12px;background:#f5f5f5;padding:12px} .errors{color:#b00020}</style>
            </head><body><div><div>{{t}}: {{m}}</div>{{list}}{{logs}}{{details}}</div>{{ready}}</body></html>
            """;
    }

    // The standard error (a console may be attached in Development) and the monitoring when it is still active.
    static void ReportFailure( Exception ex )
    {
        try
        {
            Console.Error.WriteLine( $"The host failed: {ex.Message}" );
            Console.Error.WriteLine( ex );
            if( CK.Monitoring.GrandOutput.Default != null ) ActivityMonitor.StaticLogger.Fatal( "The host failed.", ex );
        }
        catch
        {
            // Nothing more can be done: the exit code says it.
        }
    }

    // Collects the errors (and fatals) of a monitor, groups included.
    sealed class ErrorCollector : ActivityMonitorClient
    {
        readonly ConcurrentQueue<string> _errors;

        public ErrorCollector( ConcurrentQueue<string> errors ) => _errors = errors;

        protected override void OnUnfilteredLog( ref ActivityMonitorLogData data )
        {
            if( data.MaskedLevel >= LogLevel.Error ) _errors.Enqueue( data.Text );
        }

        protected override void OnOpenGroup( IActivityLogGroup group )
        {
            if( group.Data.MaskedLevel >= LogLevel.Error ) _errors.Enqueue( group.Data.Text );
        }
    }
}
