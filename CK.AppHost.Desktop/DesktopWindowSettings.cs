namespace CK.AppHost.Desktop;

/// <summary>
/// The title and the size of a desktop window.
/// </summary>
/// <param name="Title">The title of the window.</param>
/// <param name="Width">The width of the window.</param>
/// <param name="Height">The height of the window.</param>
public sealed record DesktopWindowSettings( string Title, int Width, int Height )
{
    /// <summary>
    /// The default width.
    /// </summary>
    public const int DefaultWidth = 1280;

    /// <summary>
    /// The default height.
    /// </summary>
    public const int DefaultHeight = 800;
}
