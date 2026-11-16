using ZoomIN.Core.Islands;
using ZoomIN.Core.Viewport;

namespace ZoomIN.App.Islands;

/// <summary>
/// The input handler of one run of the island edit mode (M19 B1): every input names its run, so an
/// input of an ended run that is still on its way is never applied to the next run.
/// </summary>
internal sealed class IslandEditInputRun(IslandController controller, IslandEditSession run) : IIslandEditInputHandler
{
    public void Pressed(PhysicalPoint point) => controller.Step(run, editor => editor.Press(point), "press");

    public void Moved(PhysicalPoint point) => controller.Step(run, editor => editor.Move(point), "move");

    public void Released(PhysicalPoint point) => controller.Step(run, editor => editor.Release(point), "release");

    public void Wheel(double notches, PhysicalPoint point) => controller.Step(run, editor => editor.Wheel(notches, point), "wheel");

    public void Key(IslandEditKey key) =>
        controller.Step(run, editor => key == IslandEditKey.Escape ? editor.Escape() : editor.Delete(), key.ToString());
}
