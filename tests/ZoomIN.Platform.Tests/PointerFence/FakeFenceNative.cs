using ZoomIN.Core.Viewport;
using ZoomIN.Platform.PointerFence;

namespace ZoomIN.Platform.Tests.PointerFence;

/// <summary>Records what the fence would do in Windows; never touches the real pointer.</summary>
internal sealed class FakeFenceNative : IFenceNative
{
    public PhysicalRect Screen { get; set; } = new(0, 0, 5760, 1080);

    public PhysicalRect? Clip { get; set; }

    public bool ClipSucceeds { get; set; } = true;

    public bool ClipThrows { get; set; }

    public bool HookRefused { get; set; }

    public bool HookThrows { get; set; }

    public bool WatcherThrows { get; set; }

    public int Unclips { get; private set; }

    public int ClipCalls { get; private set; }

    public FakeHandle? Hook { get; private set; }

    public FakeHandle? Watcher { get; private set; }

    public IFenceHookTarget? Target { get; private set; }

    public IFenceEvents? Events { get; private set; }

    public bool HookActive => Hook is { Disposed: false };

    public bool WatcherActive => Watcher is { Disposed: false };

    /// <summary>True if the real pointer would be confined right now.</summary>
    public bool Confined => Clip is not null && Clip != Screen;

    public bool ClipTo(PhysicalRect rect, out int error)
    {
        ClipCalls++;
        if (ClipThrows)
        {
            throw new InvalidOperationException("clip failed");
        }

        error = ClipSucceeds ? 0 : 5;
        if (ClipSucceeds)
        {
            Clip = rect;
        }

        return ClipSucceeds;
    }

    public void Unclip()
    {
        Unclips++;
        Clip = null;
    }

    public PhysicalRect? CurrentClip() => Clip ?? Screen;

    public PhysicalPoint? Cursor { get; set; } = new PhysicalPoint(10, 10);

    public PhysicalPoint? CursorPosition() => Cursor;

    public PhysicalRect VirtualScreen() => Screen;

    public IDisposable? InstallMoveHook(IFenceHookTarget target)
    {
        if (HookThrows)
        {
            throw new InvalidOperationException("hook failed");
        }

        if (HookRefused)
        {
            return null;
        }

        Target = target;
        Hook = new FakeHandle();
        return Hook;
    }

    public IDisposable StartWatcher(IFenceEvents events)
    {
        if (WatcherThrows)
        {
            throw new InvalidOperationException("watcher failed");
        }

        Events = events;
        Watcher = new FakeHandle();
        return Watcher;
    }

    internal sealed class FakeHandle : IDisposable
    {
        public bool Disposed { get; private set; }

        public bool ThrowOnDispose { get; set; }

        public void Dispose()
        {
            Disposed = true;
            if (ThrowOnDispose)
            {
                throw new InvalidOperationException("dispose failed");
            }
        }
    }
}
