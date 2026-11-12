using System.Threading.Channels;
using ZoomIN.Core.Islands;
using ZoomIN.Core.Viewport;

namespace ZoomIN.Platform.Input;

/// <summary>
/// One run of the island edit mode's input (M19 B1): the hook thread queues, a worker hands the
/// input on to the handler one at a time and in order. Moves come fast: a burst of moves between two
/// other inputs is folded into its latest one, which keeps its place in the order (a move after a
/// press reaches the handler after the press). After <see cref="Complete"/> inputs still queued are
/// dropped: they belong to an edit mode that is over.
/// </summary>
internal sealed class IslandEditQueue
{
    private readonly IIslandEditInputHandler _handler;
    private readonly Channel<Input> _queue = Channel.CreateUnbounded<Input>(new UnboundedChannelOptions { SingleReader = true });

    // The move burst queued last and not yet handed on; a new move only updates it. One small object
    // per burst (not per move) on the hook thread.
    private MoveBox? _pending;
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

    public void Press(PhysicalPoint point) => Post(new Input(InputKind.Press, point, 0, null));

    public void Release(PhysicalPoint point) => Post(new Input(InputKind.Release, point, 0, null));

    public void Wheel(double notches, PhysicalPoint point) => Post(new Input(InputKind.Wheel, point, notches, null));

    public void Key(IslandEditKey key) =>
        Post(new Input(key == IslandEditKey.Escape ? InputKind.Escape : InputKind.Delete, default, 0, null));

    public void Move(int x, int y)
    {
        var packed = ((long)x << 32) | (uint)y;
        if (Volatile.Read(ref _pending) is { } burst && burst.TryUpdate(packed))
        {
            return;
        }

        var box = new MoveBox(packed);
        Volatile.Write(ref _pending, box);
        _queue.Writer.TryWrite(new Input(InputKind.Move, default, 0, box));
    }

    public void Complete()
    {
        _stopped = true;
        _queue.Writer.TryComplete();
    }

    // Any other input closes the move burst before it: later moves start a new one after it.
    private void Post(Input input)
    {
        Volatile.Write(ref _pending, null);
        _queue.Writer.TryWrite(input);
    }

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
                    var packed = input.Box!.Take();
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

    private readonly record struct Input(InputKind Kind, PhysicalPoint Point, double Notches, MoveBox? Box);

    /// <summary>The latest position of one move burst; closed once the worker took it.</summary>
    private sealed class MoveBox(long packed)
    {
        private long _packed = packed;
        private int _taken;

        // Hook thread. False once the worker took it: the move must go into a new burst.
        public bool TryUpdate(long packed)
        {
            Interlocked.Exchange(ref _packed, packed); // full fence: written before the flag is read (pairs with Take)
            return Volatile.Read(ref _taken) == 0;
        }

        // Worker. A move written after this read starts a new burst (TryUpdate sees the flag).
        public long Take()
        {
            Interlocked.Exchange(ref _taken, 1);
            return Volatile.Read(ref _packed);
        }
    }
}
