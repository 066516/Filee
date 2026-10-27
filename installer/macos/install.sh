#!/bin/sh
set -eu

source_dir=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd -P)
install_dir="$HOME/Applications/Takaconv.app"
bin_dir="$HOME/.local/bin"
if [ -e "$install_dir" ] || [ -L "$install_dir" ]; then
    printf '%s\n' "An installation already exists at $install_dir. Run uninstall.sh first."
    exit 1
fi
# A takaconv link left by an earlier Takaconv whose app is gone (moved to the Trash instead of uninstall.sh) is replaced.
# Anything else at that path isn't ours and stays.
stale_link=false
if [ -L "$bin_dir/takaconv" ] && [ ! -e "$bin_dir/takaconv" ]; then
    case $(readlink "$bin_dir/takaconv") in
        */Takaconv.app/Contents/MacOS/takaconv-cli) stale_link=true ;;
    esac
fi
if [ "$stale_link" = false ] && { [ -e "$bin_dir/takaconv" ] || [ -L "$bin_dir/takaconv" ]; }; then
    printf '%s\n' "$bin_dir/takaconv already exists. Remove or rename it before installing Takaconv."
    exit 1
fi
test -x "$source_dir/Takaconv.app/Contents/MacOS/Takaconv"
mkdir -p "$HOME/Applications" "$bin_dir"
/usr/bin/ditto "$source_dir/Takaconv.app" "$install_dir"
# ditto keeps the download's quarantine flag: macOS would then start Takaconv from a translocated copy and could stop
# the takaconv command without a word (#316).
/usr/bin/xattr -dr com.apple.quarantine "$install_dir" 2>/dev/null || true
if [ "$stale_link" = true ]; then
    rm -f -- "$bin_dir/takaconv"
    printf '%s\n' "Replaced the takaconv link left by an earlier installation."
fi
ln -s "$install_dir/Contents/MacOS/takaconv-cli" "$bin_dir/takaconv"
printf '%s\n' "Installed $install_dir" "CLI: $bin_dir/takaconv (add $bin_dir to PATH if needed)."
printf '%s\n' 'Open Takaconv from Applications. Grant Accessibility and Finder Automation access when requested.'
printf '%s\n' 'If Takaconv is already listed and switched on under Accessibility, remove it with the - button and add it again with +.'
