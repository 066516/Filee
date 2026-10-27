#!/bin/sh
set -eu

install_dir="$HOME/Applications/Takaconv.app"
if [ -L "$install_dir" ] || [ ! -d "$install_dir" ]; then
    printf '%s\n' "No ordinary Takaconv app bundle was found at $install_dir."
    exit 1
fi
resolved_parent=$(CDPATH= cd -- "$HOME/Applications" && pwd -P)
resolved_app=$(CDPATH= cd -- "$install_dir" && pwd -P)
if [ "$resolved_app" != "$resolved_parent/Takaconv.app" ]; then
    printf '%s\n' 'Refusing to remove an app outside the expected Applications directory.'
    exit 1
fi
bundle_id=$(/usr/libexec/PlistBuddy -c 'Print :CFBundleIdentifier' "$install_dir/Contents/Info.plist")
if [ "$bundle_id" != 'com.takaful.takaconv' ]; then
    printf '%s\n' 'The bundle is not Takaconv.'
    exit 1
fi
"$install_dir/Contents/MacOS/Takaconv" --uninstall-cleanup
if [ -L "$HOME/.local/bin/takaconv" ] && [ "$(readlink "$HOME/.local/bin/takaconv")" = "$install_dir/Contents/MacOS/takaconv-cli" ]; then
    rm -- "$HOME/.local/bin/takaconv"
fi
rm -rf -- "$install_dir"
printf '%s\n' 'Takaconv was removed. Your settings and conversion results were kept.'
