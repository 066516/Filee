# GNOME Wayland

Filee supports drag gestures on GNOME Wayland through the **Filee donut at the cursor** GNOME Shell extension.

The extension provides the pointer state required by Filee without access to `/dev/input`, membership in the `input` group, or running Filee as root.

> This integration is experimental and has been tested on Fedora 44 with GNOME Shell 50.

## Setup

Install Filee normally:

```sh
./install.sh
```

Then:

1. Open the **Extensions** application.
2. Enable **Filee donut at the cursor**.
3. Log out and back in if the extension does not appear or was recently updated.
4. Start Filee and make sure gesture detection is enabled under **Triggers**.

Do not run Filee with `sudo`.

## Usage

With the default gesture:

1. Open **Files / Nautilus**.
2. Hold **Ctrl** and drag a file.
3. Move the file over the desired format in the Filee wheel.
4. Release it to start the conversion.

The wheel stays in place while selecting a format.

Pausing Filee, disabling the gesture or extension, excluding the application, or dropping outside the wheel leaves the normal file-manager behavior unchanged.

Custom modifiers and excluded applications continue to work through Filee's existing gesture settings.

## Limitations

- GNOME integration is currently experimental.
- GNOME Shell 45–50 are declared as supported, but not every version has been tested.
- The integration provides pointer and modifier state, not a native Wayland backend for Avalonia.
- Global Tab and mouse-wheel state are not available for switching wheel faces during a drag.
- Multi-monitor setups, fractional scaling and different Wayland/XWayland combinations may require additional testing.

The KDE Wayland integration remains separate.

## Troubleshooting

If the wheel does not appear:

- Make sure Filee is not paused.
- Check that the drag gesture is enabled under **Triggers**.
- Make sure **Filee donut at the cursor** is enabled.
- Make sure Files / Nautilus is not excluded.
- Do not run Filee with `sudo`.
- If the extension was installed or updated recently, log out and back in.

To check the extension:

```sh
gnome-extensions info filee-donut@filee.sh
```

To check the communication socket:

```sh
ls -l "$XDG_RUNTIME_DIR/filee-gnome/pointer-v1.sock"
```

When connected successfully, the Filee log contains:

```text
GNOME pointer extension connected
```

If the extension is active but an older version is still loaded, log out and back in to reload GNOME Shell.