// KWin script for the experimental Wayland gesture: Wayland gives Filee no cursor position and no say over where its
// window opens, so this moves the donut ("Filee Donut") to the cursor whenever it is mapped.
// Load for this session:   qdbus6 org.kde.KWin /Scripting loadScript <this file> filee-donut
//                          qdbus6 org.kde.KWin /Scripting start
// Remove:                  qdbus6 org.kde.KWin /Scripting unloadScript filee-donut

function centreOnCursor(w) {
    var p = workspace.cursorPos;
    var g = w.frameGeometry;
    var x = Math.round(p.x - g.width / 2);
    var y = Math.round(p.y - g.height / 2);
    if (g.x !== x || g.y !== y) {
        w.frameGeometry = { x: x, y: y, width: g.width, height: g.height };
    }
}

workspace.windowAdded.connect(function (w) {
    if (w.caption !== "Filee Donut") {
        return;
    }
    // Filee also positions the window itself (twice, to cope with DPI changes), so win the last few moves.
    var left = 4;
    centreOnCursor(w);
    w.frameGeometryChanged.connect(function () {
        if (left > 0) {
            left--;
            centreOnCursor(w);
        }
    });
});
