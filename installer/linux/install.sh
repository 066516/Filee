#!/bin/sh
set -eu

source_dir=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd -P)
data_dir=${XDG_DATA_HOME:-"$HOME/.local/share"}
install_dir="$data_dir/filee/application"
bin_dir="$HOME/.local/bin"
desktop_file="$data_dir/applications/com.filee.app.desktop"
gnome_extension_id="filee-donut@filee.sh"
gnome_extension_source="$source_dir/gnome/$gnome_extension_id"
gnome_extension_dir="$data_dir/gnome-shell/extensions/$gnome_extension_id"
if [ -e "$install_dir" ] || [ -L "$install_dir" ]; then
    printf '%s\n' "An installation already exists at $install_dir. Run its uninstall.sh first."
    exit 1
fi
if [ -e "$bin_dir/filee" ] || [ -L "$bin_dir/filee" ]; then
    printf '%s\n' "$bin_dir/filee already exists. Remove or rename it before installing Filee."
    exit 1
fi
if [ -e "$desktop_file" ] || [ -L "$desktop_file" ]; then
    printf '%s\n' "$desktop_file already exists. Remove or rename it before installing Filee."
    exit 1
fi
is_gnome=false
if printf '%s' "${XDG_CURRENT_DESKTOP:-}" | tr ':' '\n' | grep -qi '^GNOME$'; then
    is_gnome=true
fi
if [ "$is_gnome" = true ] && [ -d "$gnome_extension_source" ] && { [ -e "$gnome_extension_dir" ] || [ -L "$gnome_extension_dir" ]; }; then
    printf '%s\n' "A GNOME extension already exists at $gnome_extension_dir. Remove or rename it before installing Filee."
    exit 1
fi
command -v python3 >/dev/null
test -x "$source_dir/Filee"
test -x "$source_dir/filee-cli"
mkdir -p "$data_dir/filee" "$bin_dir" "$data_dir/applications"
cp -R "$source_dir" "$install_dir"
printf '%s\n' 'com.filee.app' > "$install_dir/.filee-package"
ln -s "$install_dir/filee-cli" "$bin_dir/filee"
if [ "$is_gnome" = true ] && [ -d "$gnome_extension_source" ]; then
    mkdir -p "$(dirname "$gnome_extension_dir")"
    cp -R "$install_dir/gnome/$gnome_extension_id" "$gnome_extension_dir"
    printf '%s\n' 'com.filee.app' > "$gnome_extension_dir/.filee-package"
    if command -v gsettings >/dev/null 2>&1; then
        enabled=$(gsettings get org.gnome.shell enabled-extensions 2>/dev/null || true)
        if [ -n "$enabled" ]; then
            updated=$(python3 - "$enabled" "$gnome_extension_id" <<'PY'
import ast
import sys

try:
    extensions = ast.literal_eval(sys.argv[1].removeprefix("@as "))
except (SyntaxError, ValueError):
    raise SystemExit(0)
if not isinstance(extensions, list) or not all(isinstance(item, str) for item in extensions):
    raise SystemExit(0)
if sys.argv[2] not in extensions:
    extensions.append(sys.argv[2])
print("[" + ", ".join("'" + item.replace("'", "\\'") + "'" for item in extensions) + "]")
PY
            )
            if [ -n "$updated" ]; then
                gsettings set org.gnome.shell enabled-extensions "$updated" >/dev/null 2>&1 || true
            fi
        fi
    fi
fi
python3 - "$install_dir" "$desktop_file" <<'PY'
import pathlib
import sys

app, desktop = map(pathlib.Path, sys.argv[1:])
def quoted_exec(value):
    value = value.replace('%', '%%')
    for character in ('\\', '"', '`', '$'):
        value = value.replace(character, '\\' + character)
    return '"' + value.replace('\\', '\\\\').replace('\n', '\\n').replace('\r', '\\r') + '"'

def desktop_value(value):
    return str(value).replace('\\', '\\\\').replace('\n', '\\n').replace('\r', '\\r')

desktop.write_text(
    '[Desktop Entry]\nType=Application\nName=Filee\n'
    'Comment=Convert files with Filee\n'
    f'Exec={quoted_exec(str(app / "Filee"))} --convert %F\n'
    f'Icon={desktop_value(app / "filee.png")}\n'
    'Terminal=false\nCategories=Utility;\nX-Filee-Managed=true\n', encoding='utf-8')
PY
if command -v update-desktop-database >/dev/null 2>&1; then
    update-desktop-database "$data_dir/applications"
fi
printf '%s\n' "Installed Filee at $install_dir" "CLI: $bin_dir/filee (add $bin_dir to PATH if needed)."

if [ "$is_gnome" = true ] && [ -d "$gnome_extension_source" ]; then
    printf '%s\n' 'GNOME Wayland: enable "Filee donut at the cursor" in Extensions.' 'Sign out and sign in if the extension is not listed yet. Then hold Ctrl and drag a file in Files.' 'No administrator access or input group is required. See GNOME-WAYLAND.md.'
fi
