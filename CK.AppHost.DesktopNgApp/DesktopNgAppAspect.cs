using CK.AppHost.Desktop;
using CK.Core;
using CK.Setup;

namespace CK.AppHost.DesktopNgApp;

/// <summary>
/// The "DesktopNgApp" App aspect: the App is a desktop application whose window shows one of its client applications (an
/// Angular application served by the endpoint of the party), run by <see cref="DesktopNgAppRunner"/>. It is configured
/// where the package is added to the App, typically by a deployment:
/// <code>
/// "Aspects": { "DesktopNgApp": { "Client": "App", "Title": "CK Configurator" } }
/// </code>
/// The builder checks that the client is declared by the App, and projects the window settings in the host's
/// configuration (the root section <see cref="SectionName"/>).
/// </summary>
[AppAspect( "DesktopNgApp", TypeActivation.Optional )]
[Configuration<DesktopNgAppConfiguration>]
public sealed class DesktopNgAppAspect : IAppAspectApply
{
    /// <summary>
    /// The root section of the host's configuration in which the window settings are projected.
    /// </summary>
    public const string SectionName = "DesktopNgApp";

    readonly DesktopNgAppConfiguration _configuration;

    /// <summary>
    /// Initializes a new <see cref="DesktopNgAppAspect"/> from its configuration.
    /// </summary>
    /// <param name="configuration">Its configuration, read and validated by the App builder.</param>
    public DesktopNgAppAspect( DesktopNgAppConfiguration configuration )
    {
        Throw.CheckNotNullArgument( configuration );
        _configuration = configuration;
    }

    /// <inheritdoc />
    public bool Apply( IActivityMonitor monitor, AppBuilderContext context, AppConfiguration app )
    {
        var client = _configuration.Client;
        if( app.Configuration.TryGetSection( $"Local:PartyClient:{client}" ) == null )
        {
            monitor.Error( $"The \"DesktopNgApp\" aspect of {app} shows the client '{client}', but the App has no \"Local:PartyClient:{client}\"." );
            return false;
        }
        app.ProjectValue( $"{SectionName}:Client", client );
        app.ProjectValue( $"{SectionName}:Title", string.IsNullOrWhiteSpace( _configuration.Title ) ? client : _configuration.Title );
        app.ProjectValue( $"{SectionName}:Width", _configuration.Width.ToString( System.Globalization.CultureInfo.InvariantCulture ) );
        app.ProjectValue( $"{SectionName}:Height", _configuration.Height.ToString( System.Globalization.CultureInfo.InvariantCulture ) );
        return true;
    }
}

/// <summary>
/// The configuration of the "DesktopNgApp" App aspect (<see cref="DesktopNgAppAspect"/>).
/// <code>
/// "DesktopNgApp": { "Client": "App", "Title": "CK Configurator", "Width": 1280, "Height": 800 }
/// </code>
/// </summary>
[ConfigurationModel]
public sealed class DesktopNgAppConfiguration : IConfigurationModel
{
    /// <summary>
    /// Gets the name of the client application that the window shows: a client of the App ("Local:PartyClient:{Client}")
    /// served by the endpoint of the party ("Hosting").
    /// </summary>
    public required string Client { get; init; }

    /// <summary>
    /// Gets the title of the window (the client name by default).
    /// </summary>
    public string? Title { get; init; }

    /// <summary>
    /// Gets the width of the window.
    /// </summary>
    public int Width { get; init; } = DesktopWindowSettings.DefaultWidth;

    /// <summary>
    /// Gets the height of the window.
    /// </summary>
    public int Height { get; init; } = DesktopWindowSettings.DefaultHeight;

    /// <inheritdoc />
    public void Validate( IConfigurationValidationContext context )
    {
        if( string.IsNullOrWhiteSpace( Client ) ) context.Error( $"'{context.Path}:Client' must be the name of a client of the App." );
        if( Width <= 0 ) context.Error( $"'{context.Path}:Width' is {Width}: a positive integer is expected." );
        if( Height <= 0 ) context.Error( $"'{context.Path}:Height' is {Height}: a positive integer is expected." );
    }
}
