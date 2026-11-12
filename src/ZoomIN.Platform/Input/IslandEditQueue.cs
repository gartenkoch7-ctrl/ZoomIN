using System.Threading.Channels;
using ZoomIN.Core.Islands;
using ZoomIN.Core.Viewport;

namespace ZoomIN.Platform.Input;

/// <summary>
/// One run of the island edit mode's input (M19 B1): the hook thread queues, a worker hands the
/// input on to the handler one at a time and in order. Moves come fast and are folded into the
/// latest one. After <see cref="Complete"/> inputs still queued are dropped: they belong to an edit
/// mode that is over.
/// </summary>
internal sealed class IslandEditQueue
{
    private readonly IIslandEditInputHandler _handler;
    private readonly Channel<Input> _queue = Channel.CreateUnbounded<Input>(new UnboundedChannelOptions { SingleReader = true });

    // The latest move, packed; queued as one marker until the worker took it.
    private long _move;
    private int _moveQueued;
    private volatile bool _stopped;

    public IslandEditQueue(IIslandEditInputHandler handler)
    {
        _handler = handler;
        _ = Task.Run(RunAsync);
    }

    private enum InputKind
    {
        Press,
        Move,
        Release,
        Wheel,
        Escape,
        Delete,
    }

    public void Press(PhysicalPoint point) => Post(new Input(InputKind.Press, point, 0));

    public void Release(PhysicalPoint point) => Post(new Input(InputKind.Release, point, 0));

    public void Wheel(double notches, PhysicalPoint point) => Post(new Input(InputKind.Wheel, point, notches));

    public void Key(IslandEditKey key) => Post(new Input(key == IslandEditKey.Escape ? InputKind.Escape : InputKind.Delete, default, 0));

    public void Move(int x, int y)
    {
        Volatile.Write(ref _move, ((long)x << 32) | (uint)y);
        if (Interlocked.Exchange(ref _moveQueued, 1) == 0)
        {
            Post(new Input(InputKind.Move, default, 0));
        }
    }

    public void Complete()
    {
        _stopped = true;
        _queue.Writer.TryComplete();
    }

    private void Post(Input input) => _queue.Writer.TryWrite(input);

    private async Task RunAsync()
    {
        await foreach (var input in _queue.Reader.ReadAllAsync().ConfigureAwait(false))
        {
            if (!_stopped)
            {
                Hand(input);
            }
        }
    }

    // The handler logs its own failures; the worker must outlive one of them.
    private void Hand(Input input)
    {
        try
        {
            switch (input.Kind)
            {
                case InputKind.Move:
                    Interlocked.Exchange(ref _moveQueued, 0);
                    var packed = Volatile.Read(ref _move);
                    _handler.Moved(new PhysicalPoint((int)(packed >> 32), (int)packed));
                    break;
                case InputKind.Press:
                    _handler.Pressed(input.Point);
                    break;
                case InputKind.Release:
                    _handler.Released(input.Point);
                    break;
                case InputKind.Wheel:
                    _handler.Wheel(input.Notches, input.Point);
                    break;
                default:
                    _handler.Key(input.Kind == InputKind.Escape ? IslandEditKey.Escape : IslandEditKey.Delete);
                    break;
            }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // Swallowed on purpose; see the comment above.
        }
    }

    private readonly record struct Input(InputKind Kind, PhysicalPoint Point, double Notches);
}
