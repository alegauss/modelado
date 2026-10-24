namespace Modelado.Imaging;

/// <summary>
/// Every colour Modelado decides for itself, each declared once, as bytes (MOD4, after
/// PhotoBoost's Brand). A colour is a value, and each surface converts it at its own edge: the
/// renderer packs it into pixels, the window into a brush. Everything else is Fluent's: the chrome
/// paints with the theme's brushes, so light, dark and the user's accent come from Windows. What is
/// here is only what Fluent has no opinion on, and an architecture test refuses a colour written
/// anywhere else.
///
/// <para>It lives here rather than in the app, as PhotoBoost's does, because the viewer's renderer
/// is drawn by the app, the command line and the tests alike, and all three must paint the same
/// picture.</para>
/// </summary>
public static class Brand
{
    /// <summary>The top of the viewer's background gradient.</summary>
    public static readonly (byte R, byte G, byte B) ViewportTop = (0x3A, 0x3F, 0x47);

    /// <summary>The bottom of the gradient, also the ground behind a photo shown letterboxed.</summary>
    public static readonly (byte R, byte G, byte B) ViewportBottom = (0x1C, 0x1F, 0x24);

    /// <summary>The empty viewer's title: light on the dark ground, in either theme.</summary>
    public static readonly (byte R, byte G, byte B) HintTitle = (0xE6, 0xE6, 0xE6);

    /// <summary>The empty viewer's smaller text and the orbit hint, quieter than the title.</summary>
    public static readonly (byte R, byte G, byte B) HintText = (0xA8, 0xAD, 0xB5);

    /// <summary>The clay a model is drawn in when it has no colours, or when clay is asked for.</summary>
    public static readonly (byte R, byte G, byte B) Clay = (0xB8, 0xB8, 0xB3);

    /// <summary>The triangle edges the wireframe view draws over the clay.</summary>
    public static readonly (byte R, byte G, byte B) Edge = (0x2B, 0x2B, 0x2B);

    /// <summary>
    /// The app icon's tile, top-left of its gradient (MOD7): a cool blue, the sibling of
    /// PhotoBoost's warm clay tile.
    /// </summary>
    public static readonly (byte R, byte G, byte B) IconTileLight = (0x5B, 0x9B, 0xE6);

    /// <summary>The tile's bottom-right; also the mesh lines drawn on the cube at large sizes.</summary>
    public static readonly (byte R, byte G, byte B) IconTileDeep = (0x23, 0x4F, 0x9C);

    /// <summary>The cube's top face, the one the light falls on.</summary>
    public static readonly (byte R, byte G, byte B) IconCubeTop = (0xFF, 0xFF, 0xFF);

    /// <summary>The cube's left face, a step darker.</summary>
    public static readonly (byte R, byte G, byte B) IconCubeLeft = (0xD6, 0xE5, 0xF8);

    /// <summary>The cube's right face, the one in shade.</summary>
    public static readonly (byte R, byte G, byte B) IconCubeRight = (0xA4, 0xC1, 0xEA);
}
