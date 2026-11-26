using CK.AppHost.Desktop;
using CK.AppIdentity;
using CK.Core;
using CK.TS.Angular;
using CK.TypeScript;

namespace CK.AppHost.DesktopNgApp;

/// <summary>
/// The "DesktopNgApp" option of the "NgApp" feature of a client: the App is a desktop application whose window shows this
/// client (served by the endpoint of the party: it requires the "Hosting" feature of the client), run by
/// <see cref="DesktopNgAppRunner"/>. It is configured where the package is added to the App, typically by a deployment:
/// <code>
/// "Local:PartyClient:App:NgApp:DesktopNgApp": { "Title": "CK Configurator" }
/// </code>
/// The builder makes the endpoint of the party in process (<c>"HttpEndpoint": { "InProcess": true }</c>) and refuses two
/// clients with the option: the window shows one client.
/// </summary>
[PartyClientFeatureOption( "DesktopNgApp", TypeActivation.Optional )]
[Requires<HostingClientFeature>]
[Configuration<DesktopNgAppConfiguration>]
public sealed class DesktopNgAppOption : IPartyComponentOption<NgAppClientFeature>
{
    /// <summary>
    /// Initializes a new <see cref="DesktopNgAppOption"/>.
    /// </summary>
    /// <param name="configuration">Its configuration.</param>
    public DesktopNgAppOption( DesktopNgAppConfiguration configuration )
    {
        Configuration = configuration;
    }

    /// <summary>
    /// Gets the settings of the window.
    /// </summary>
    public DesktopNgAppConfiguration Configuration { get; }
}

/// <summary>
/// The configuration of the "DesktopNgApp" option of a client (<see cref="DesktopNgAppOption"/>): the window that shows it.
/// <code>
/// "DesktopNgApp": { "Title": "CK Configurator", "Width": 1280, "Height": 800 }
/// </code>
/// </summary>
[ConfigurationModel]
public sealed class DesktopNgAppConfiguration : IConfigurationModel
{
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
        if( Width <= 0 ) context.Error( $"'{context.Path}:Width' is {Width}: a positive integer is expected." );
        if( Height <= 0 ) context.Error( $"'{context.Path}:Height' is {Height}: a positive integer is expected." );
    }
}
