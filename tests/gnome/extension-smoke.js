// Run with GJS and Mutter's typelib/library paths. Real Gio Unix sockets, mocked Shell windows and pointer.
// No interaction with the running desktop, settings, extensions or user files.
const {Gio, GLib, Clutter, Meta} = imports.gi;
const assert = (condition, message) => { if (!condition) throw new Error(message); };
const Main = {sessionMode: {isLocked: false, hasWindows: true}, overview: {visible: false}, modalCount: 0,
    layoutManager: {monitors: [{x: 0, y: 0, width: 1920, height: 1080}]}};
let pointer = [500, 400, 0];
let map;
let moves = [];
const window = {
    get_title: () => 'Filee Donut', get_wm_class: () => 'Filee', get_wm_class_instance: () => 'Filee',
    get_gtk_application_id: () => null, get_frame_rect: () => ({x: 0, y: 0, width: 400, height: 400}),
    get_pid: () => new Gio.Credentials().get_unix_pid(), get_window_type: () => Meta.WindowType.NORMAL,
    showing_on_its_workspace: () => true, make_above() {}, raise() {}, move_frame: (_user, x, y) => moves.push([x, y]),
};
const actor = {meta_window: window, is_visible: () => true};
const shell = {
    get_pointer: () => pointer, get_window_actors: () => [actor],
    window_manager: {connect: (_event, callback) => { map = callback; return 1; }, disconnect: () => { map = null; }},
    display: {sort_windows_by_stacking: windows => windows},
};
const path = ARGV[0];
const [ok, bytes] = GLib.file_get_contents(path);
assert(ok, 'read extension');
const source = new TextDecoder().decode(bytes).replace(/^import .*;\n/gm, '').replace('export default class', 'class');
const ExtensionClass = new Function('Clutter', 'Gio', 'GLib', 'Meta', 'Extension', 'Main', 'global',
    `${source}\nreturn FileeDonutExtension;`)(Clutter, Gio, GLib, Meta, class {}, Main, shell);
const loop = new GLib.MainLoop(null, false);
const extension = new ExtensionClass();
const socketPath = `${GLib.get_user_runtime_dir()}/filee-gnome/pointer-v1.sock`;
let connection;
function read(stream) {
    return new Promise((resolve, reject) => stream.read_line_async(GLib.PRIORITY_DEFAULT, null, (input, result) => {
        try { const [line] = input.read_line_finish_utf8(result); resolve(JSON.parse(line)); } catch (error) { reject(error); }
    }));
}
async function run() {
    extension.enable();
    const info = Gio.File.new_for_path(socketPath).query_info('unix::mode', Gio.FileQueryInfoFlags.NONE, null);
    assert((info.get_attribute_uint32('unix::mode') & 0o777) === 0o600, 'socket is private');
    const client = new Gio.SocketClient();
    connection = await new Promise((resolve, reject) => client.connect_async(new Gio.UnixSocketAddress({path: socketPath}), null,
        (object, result) => { try { resolve(object.connect_finish(result)); } catch (error) { reject(error); } }));
    const input = new Gio.DataInputStream({base_stream: connection.get_input_stream()});
    const first = await read(input);
    assert(first.Version === 1 && first.X === 500 && first.Buttons === 0, 'first pointer frame');
    pointer = [550, 450, Clutter.ModifierType.CONTROL_MASK | Clutter.ModifierType.BUTTON1_MASK];
    const pressed = await read(input);
    assert(pressed.Modifiers === 1 && pressed.Buttons === 1, 'Ctrl + left button mapping');
    pointer = [550, 450, Clutter.ModifierType.BUTTON3_MASK];
    assert((await read(input)).Buttons === 2, 'right button mapping');
    Main.sessionMode.isLocked = true;
    const locked = await read(input);
    assert(locked.Blocked && locked.Modifiers === 0 && locked.Buttons === 0 && locked.Process === null, 'lock screen suppresses state');
    Main.sessionMode.isLocked = false;
    map(null, actor);
    await read(input);
    assert(moves.length === 1, 'position exactly once when mapped');
    pointer = [800, 600, 0];
    await read(input);
    assert(moves.length === 1, 'moving to a format does not recenter the ring');
    pointer = [1910, 1070, 0];
    map(null, actor);
    await read(input);
    assert(moves.length === 2 && moves[1][0] === 1520 && moves[1][1] === 680, 'clamp at monitor edge on next opening');
    print('PASS: real Gio socket, private permissions, modifiers/buttons, lock screen, fixed placement, screen edges');
}
const timeout = GLib.timeout_add(GLib.PRIORITY_DEFAULT, 8000, () => { printerr('FAIL: timed out'); imports.system.exit(1); return GLib.SOURCE_REMOVE; });
run().then(() => {
    GLib.Source.remove(timeout);
    extension.disable();
    assert(!map && !Gio.File.new_for_path(socketPath).query_exists(null), 'disable disconnects signals and removes socket');
    connection?.close(null);
    loop.quit();
}).catch(error => { printerr(error.stack); extension.disable(); imports.system.exit(1); });
loop.run();
