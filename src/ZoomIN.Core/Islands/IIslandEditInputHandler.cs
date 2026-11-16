using ZoomIN.Core.Viewport;

namespace ZoomIN.Core.Islands;

/// <summary>Receives the input of the island edit mode, in physical desktop pixels (see <see cref="IIslandEditInput"/>).</summary>
public interface IIslandEditInputHandler
{
    /// <summary>The left button went down.</summary>
    void Pressed(PhysicalPoint point);

    /// <summary>The pointer moved; several moves may arrive as the latest one.</summary>
    void Moved(PhysicalPoint point);

    /// <summary>The left button went up.</summary>
    void Released(PhysicalPoint point);

    /// <summary>The wheel turned by <paramref name="notches"/> (positive = away from the user).</summary>
    void Wheel(double notches, PhysicalPoint point);

    void Key(IslandEditKey key);
}
