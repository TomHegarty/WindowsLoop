using System.Drawing;

namespace CtrlSnap;

public enum SnapPosition
{
    Maximize,
    Left,
    Right,
    Top,
    Bottom,
    TopLeft,
    TopRight,
    BottomLeft,
    BottomRight,
}

/// <summary>Pure geometry: which position a mouse offset points at, and where that position is on screen.</summary>
public static class Layout
{
    /// <summary>
    /// Maps the mouse offset from where Ctrl was pressed to a position.
    /// Inside the dead zone means "maximize"; outside it, the direction is split
    /// into eight 45° slices: straight directions are halves, diagonals are quarters.
    /// </summary>
    public static SnapPosition Pick(int dx, int dy, int deadZone)
    {
        if ((long)dx * dx + (long)dy * dy < (long)deadZone * deadZone)
            return SnapPosition.Maximize;

        // Screen Y grows downwards, so flip it to get a normal maths angle.
        double angle = Math.Atan2(-dy, dx) * 180.0 / Math.PI;
        if (angle < 0) angle += 360;
        int slice = (int)Math.Floor((angle + 22.5) / 45.0) % 8;

        return slice switch
        {
            0 => SnapPosition.Right,
            1 => SnapPosition.TopRight,
            2 => SnapPosition.Top,
            3 => SnapPosition.TopLeft,
            4 => SnapPosition.Left,
            5 => SnapPosition.BottomLeft,
            6 => SnapPosition.Bottom,
            _ => SnapPosition.BottomRight,
        };
    }

    /// <summary>The rectangle a position covers inside a monitor's work area.</summary>
    public static Rectangle Bounds(SnapPosition position, Rectangle area)
    {
        int halfW = area.Width / 2;
        int halfH = area.Height / 2;
        int restW = area.Width - halfW;
        int restH = area.Height - halfH;

        return position switch
        {
            SnapPosition.Left => new Rectangle(area.X, area.Y, halfW, area.Height),
            SnapPosition.Right => new Rectangle(area.X + halfW, area.Y, restW, area.Height),
            SnapPosition.Top => new Rectangle(area.X, area.Y, area.Width, halfH),
            SnapPosition.Bottom => new Rectangle(area.X, area.Y + halfH, area.Width, restH),
            SnapPosition.TopLeft => new Rectangle(area.X, area.Y, halfW, halfH),
            SnapPosition.TopRight => new Rectangle(area.X + halfW, area.Y, restW, halfH),
            SnapPosition.BottomLeft => new Rectangle(area.X, area.Y + halfH, halfW, restH),
            SnapPosition.BottomRight => new Rectangle(area.X + halfW, area.Y + halfH, restW, restH),
            _ => area,
        };
    }
}
