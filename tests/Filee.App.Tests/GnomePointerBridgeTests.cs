using System.Net.Sockets;
using System.Text;
using Filee.App.Services.Triggers;
using Filee.Core.Settings;

namespace Filee.App.Tests;

public sealed class GnomePointerBridgeTests
{
    private static GnomePointerState State(int x = 100, int buttons = 0, int modifiers = 1, bool blocked = false) =>
        new(1, x, 100, modifiers, buttons, "nautilus", false, blocked);

    [Fact]
    public void Ctrl_drag_requires_movement_and_release_ends_it_once()
    {
        var detector = new GestureDetector();
        detector.Configure([new TriggerGesture { DragThreshold = 8 }]);
        var input = new GnomePointerInput(detector);
        var started = 0;
        var ended = 0;
        detector.DragStarted += (_, _, _) => started++;
        detector.DragEnded += (_, _) => ended++;
        input.Apply(State());
        input.Apply(State(buttons: 1));
        input.Apply(State(x: 105, buttons: 1));
        Assert.Equal(0, started);
        input.Apply(State(x: 120, buttons: 1));
        input.Apply(State(x: 140, buttons: 1));
        Assert.Equal(1, started);
        input.Apply(State(x: 140));
        input.Apply(State(x: 140));
        Assert.Equal(1, ended);
    }

    [Fact]
    public void Plain_drag_and_rejected_app_never_open_the_donut()
    {
        var detector = new GestureDetector();
        detector.Configure([new TriggerGesture()]);
        var input = new GnomePointerInput(detector);
        var started = 0;
        detector.DragStarted += (_, _, _) => started++;
        input.Apply(State());
        input.Apply(State(buttons: 1, modifiers: 0));
        input.Apply(State(x: 140, buttons: 1, modifiers: 0));
        input.Apply(State(x: 140));
        detector.ScopeFilter = (_, _, _) => false;
        input.Apply(State(buttons: 1));
        input.Apply(State(x: 180, buttons: 1));
        Assert.Equal(0, started);
    }

    [Fact]
    public void Connecting_mid_drag_and_unlocking_do_not_capture_an_existing_drag()
    {
        var detector = new GestureDetector();
        detector.Configure([new TriggerGesture()]);
        var input = new GnomePointerInput(detector);
        var started = 0;
        var ended = 0;
        detector.DragStarted += (_, _, _) => started++;
        detector.DragEnded += (_, _) => ended++;
        input.Apply(State(buttons: 1));
        input.Apply(State(x: 150, buttons: 1));
        Assert.Equal(0, started);
        input.Apply(State());
        input.Apply(State(buttons: 1));
        input.Apply(State(x: 150, buttons: 1));
        Assert.Equal(1, started);
        input.Apply(State(blocked: true));
        Assert.Equal(1, ended);
        input.Apply(State(x: 160, buttons: 1));
        input.Apply(State(x: 200, buttons: 1));
        Assert.Equal(1, started);
    }

    [Fact]
    public void Pause_disabled_gestures_and_custom_modifiers_are_respected()
    {
        var detector = new GestureDetector();
        detector.Configure([new TriggerGesture { Modifiers = ModifierKeys.Ctrl | ModifierKeys.Shift }]);
        var input = new GnomePointerInput(detector);
        var started = 0;
        detector.DragStarted += (_, _, _) => started++;
        input.Apply(State());
        input.Apply(State(buttons: 1));
        input.Apply(State(x: 150, buttons: 1));
        Assert.Equal(0, started);
        input.Apply(State(x: 150, buttons: 1, modifiers: 5)); // modifier added after starting the drag
        Assert.Equal(1, started);
        input.Apply(State());
        detector.Paused = true;
        input.Apply(State(buttons: 1, modifiers: 5));
        input.Apply(State(x: 200, buttons: 1, modifiers: 5));
        Assert.Equal(1, started);
        detector.Configure([]);
        detector.Paused = false;
        input.Apply(State());
        input.Apply(State(buttons: 1, modifiers: 5));
        input.Apply(State(x: 200, buttons: 1, modifiers: 5));
        Assert.Equal(1, started);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"Version\":2}")]
    [InlineData("{\"Version\":1,\"Buttons\":32}")]
    [InlineData("{\"Version\":1,\"Modifiers\":16}")]
    [InlineData("{\"Version\":1,\"X\":2147483647}")]
    [InlineData("bad JSON")]
    public void Invalid_protocol_never_becomes_input(string json) => Assert.False(GnomePointerState.TryParse(json, out _));

    [Fact]
    public async Task Real_unix_socket_delivers_pointer_frames_and_reconnects_after_extension_restart()
    {
        if (!OperatingSystem.IsLinux())
            return;
        var directory = Path.Combine(Path.GetTempPath(), "filee-gnome-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "pointer.sock");
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var listener = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        try
        {
            listener.Bind(new UnixDomainSocketEndPoint(path));
            listener.Listen(1);
            var first = new TaskCompletionSource<GnomePointerState>(TaskCreationOptions.RunContinuationsAsynchronously);
            var second = new TaskCompletionSource<GnomePointerState>(TaskCreationOptions.RunContinuationsAsynchronously);
            var disconnected = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var seen = false;
            using var bridge = new GnomePointerBridge(path, state =>
            {
                if (!first.TrySetResult(state)) second.TrySetResult(state);
            }, connected =>
            {
                if (connected) seen = true;
                else if (seen) disconnected.TrySetResult();
            });
            using (var socket = await listener.AcceptAsync(deadline.Token))
            {
                await socket.SendAsync(Encoding.UTF8.GetBytes("{\"Version\":1,\"X\":120,\"Y\":300,\"Process\":\"nautilus\"}\n"), deadline.Token);
                var state = await first.Task.WaitAsync(deadline.Token);
                Assert.Equal(120, state.X);
                Assert.Equal("nautilus", state.Process);
            }
            await disconnected.Task.WaitAsync(deadline.Token);
            using (var socket = await listener.AcceptAsync(deadline.Token))
            {
                await socket.SendAsync(Encoding.UTF8.GetBytes("{\"Version\":1,\"X\":200}\n"), deadline.Token);
                Assert.Equal(200, (await second.Task.WaitAsync(deadline.Token)).X);
            }
        }
        finally
        {
            listener.Close();
            Directory.Delete(directory, recursive: true);
        }
    }
}
