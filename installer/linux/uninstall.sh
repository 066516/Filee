#!/bin/sh
set -eu

data_dir=${XDG_DATA_HOME:-"$HOME/.local/share"}
install_dir="$data_dir/takaconv/application"
desktop_file="$data_dir/applications/com.takaful.takaconv.desktop"
gnome_extension_id="filee-donut@filee.sh"
gnome_extension_dir="$data_dir/gnome-shell/extensions/$gnome_extension_id"
python3 - "$data_dir" "$install_dir" <<'PY'
import pathlib
import sys

root, target = map(pathlib.Path, sys.argv[1:])
expected = root.resolve() / 'takaconv' / 'application'
if target.is_symlink() or target.resolve() != expected:
    raise SystemExit('Refusing to remove an installation outside its expected directory.')
if (target / '.takaconv-package').read_text(encoding='utf-8').strip() != 'com.takaful.takaconv':
    raise SystemExit('The directory is not an installation created by Takaconv.')
PY
"$install_dir/Takaconv" --uninstall-cleanup
if [ -f "$gnome_extension_dir/.takaconv-package" ] && [ "$(cat "$gnome_extension_dir/.takaconv-package")" = 'com.takaful.takaconv' ]; then
    if command -v gnome-extensions >/dev/null 2>&1; then
        gnome-extensions disable "$gnome_extension_id" >/dev/null 2>&1 || true
    fi
    rm -rf -- "$gnome_extension_dir"
fi
if [ -L "$HOME/.local/bin/takaconv" ] && [ "$(readlink "$HOME/.local/bin/takaconv")" = "$install_dir/takaconv-cli" ]; then
    rm -- "$HOME/.local/bin/takaconv"
fi
if [ -f "$desktop_file" ] && grep -q '^X-Takaconv-Managed=true$' "$desktop_file"; then
    rm -- "$desktop_file"
fi
rm -rf -- "$install_dir"
printf '%s\n' 'Takaconv was removed. Your settings and conversion results were kept.'
