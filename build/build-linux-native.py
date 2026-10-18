#!/usr/bin/env python3
"""Builds the Linux native pieces of the portable package that upstream builds on a newer glibc than Filee supports.

Filee's Linux packages must start on Ubuntu 22.04 (glibc 2.35). Two upstream binaries need a newer glibc, so CI builds
them from their pinned source on Ubuntu 22.04 and build/build-portable.ps1 puts them into the package:

- uiohook: SharpHook's libuiohook-xrecord.so and libuiohook-x11.so (the X11 global input hook back ends). SharpHook
  builds them on Ubuntu 26.04 in C23 mode, where glibc 2.38+ headers turn sscanf into __isoc23_sscanf (GLIBC_2.38).
  The loader libuiohook.so and the Wayland back end stay SharpHook's own: they already run on glibc 2.34.
- rhwp: the rhwp CLI (HWP/HWPX -> PDF). Its release binary is linked on Ubuntu 24.04 (GLIBC_2.39).

Subcommands:
  check                     verify the pins below still match Directory.Packages.props and engines.json
  uiohook --output DIR      build libuiohook-xrecord.so and libuiohook-x11.so into DIR
  rhwp --output DIR         build the rhwp CLI into DIR (rhwp, LICENSE, README files, like its release archive)

Needs git, and for uiohook CMake >= 4.2, a C compiler and the X11/Wayland development packages (see
.github/workflows/portable.yml); for rhwp rustup. Standard library only.
"""

import argparse
import json
import os
import platform
import re
import shutil
import subprocess
import sys
import tempfile
import xml.etree.ElementTree as ElementTree

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))

# SharpHook's NuGet package carries a libuiohook build; ours must come from the same source. LIBUIOHOOK_COMMIT is the
# commit of SharpHook's `libuiohook` submodule at tag v{SHARPHOOK_VERSION} (TolikPylypchuk/libuiohook, LGPL-3.0). When
# Directory.Packages.props moves SharpHook to another version, `check` fails until both lines are updated: look up the
# submodule commit of the new tag (`git ls-tree v<version> libuiohook` in SharpHook) and copy its CMake flags from
# SharpHook's .github/workflows/build.yml.
SHARPHOOK_VERSION = "8.0.0"
LIBUIOHOOK_REPOSITORY = "https://github.com/TolikPylypchuk/libuiohook"
LIBUIOHOOK_COMMIT = "a41658fb2bef7503a3bcb305ab8bf849755fe906"
SHARPHOOK_REPOSITORY = "https://github.com/TolikPylypchuk/SharpHook"
# Only the back ends that need the newer glibc are replaced; the names are what the loader dlopens from its folder.
UIOHOOK_LIBRARIES = ["libuiohook-xrecord.so", "libuiohook-x11.so"]

# The rhwp release pinned in engines.json for Linux (`check` compares the versions). RUST_TOOLCHAIN is the one in the
# tag's rust-toolchain.toml. Cargo.lock in the tag pins every dependency (--locked).
RHWP_VERSION = "0.8.7"
RHWP_REPOSITORY = "https://github.com/edwardkim/rhwp"
RHWP_COMMIT = "1a76570e833917d15817415a53c09ad61ab3203f"
RUST_TOOLCHAIN = "1.93.1"

# SharpHook's CI flags for each architecture (build.yml, jobs libuiohook-linux-x64 / libuiohook-linux-arm64).
UIOHOOK_FLAGS = {
    "x86_64": {
        "CC": "x86_64-linux-gnu-gcc",
        "CFLAGS": "-O2 -march=x86-64 -mtune=generic -g -fomit-frame-pointer -flto -fno-stack-protector -pipe",
        "PKG_CONFIG_PATH": "/usr/lib/x86_64-linux-gnu/pkgconfig",
    },
    "aarch64": {
        "CC": "aarch64-linux-gnu-gcc",
        "CFLAGS": "-Os -march=armv8-a+crc -mtune=cortex-a53 -g -flto -fomit-frame-pointer -fno-stack-protector -pipe",
        "PKG_CONFIG_PATH": "/usr/lib/aarch64-linux-gnu/pkgconfig",
    },
}
UIOHOOK_LDFLAGS = "-Wl,-O1 -Wl,--as-needed"
MINIMUM_CMAKE = (4, 2)


class BuildError(Exception):
    pass


def run(command, cwd=None, env=None, capture=False):
    print("+ " + " ".join(command), flush=True)
    result = subprocess.run(command, cwd=cwd, env=env, check=False, text=True,
                            stdout=subprocess.PIPE if capture else None)
    if result.returncode != 0:
        raise BuildError(f"{command[0]} failed with exit code {result.returncode}")
    return result.stdout if capture else None


def sharphook_version():
    path = os.path.join(ROOT, "Directory.Packages.props")
    for element in ElementTree.parse(path).getroot().iter("PackageVersion"):
        if element.get("Include") == "SharpHook":
            return element.get("Version")
    raise BuildError("Directory.Packages.props has no SharpHook PackageVersion.")


def linux_rhwp_versions():
    path = os.path.join(ROOT, "src", "Filee.Engines", "Infrastructure", "engines.json")
    with open(path, encoding="utf-8") as stream:
        manifest = json.load(stream)
    return {rid: manifest["platforms"][rid]["components"]["rhwp"]["version"] for rid in ("linux-x64", "linux-arm64")}


def check():
    problems = []
    version = sharphook_version()
    if version != SHARPHOOK_VERSION:
        problems.append(
            f"Directory.Packages.props uses SharpHook {version}, but build/build-linux-native.py builds the libuiohook "
            f"of SharpHook {SHARPHOOK_VERSION} (commit {LIBUIOHOOK_COMMIT}). Update SHARPHOOK_VERSION, "
            "LIBUIOHOOK_COMMIT and UIOHOOK_FLAGS to the new SharpHook tag.")
    for rid, rhwp in linux_rhwp_versions().items():
        if rhwp != RHWP_VERSION:
            problems.append(
                f"engines.json pins rhwp {rhwp} for {rid}, but build/build-linux-native.py builds rhwp {RHWP_VERSION}. "
                "Update RHWP_VERSION, RHWP_COMMIT and RUST_TOOLCHAIN (from the tag's rust-toolchain.toml).")
    if problems:
        raise BuildError("\n".join(problems))
    print(f"Pins match: SharpHook {SHARPHOOK_VERSION} -> libuiohook {LIBUIOHOOK_COMMIT}, "
          f"rhwp {RHWP_VERSION} -> {RHWP_COMMIT} (Rust {RUST_TOOLCHAIN}).")


def fetch(repository, commit, destination):
    """A shallow checkout of exactly one commit (GitHub serves any reachable commit by its hash)."""
    os.makedirs(destination)
    run(["git", "init", "-q", destination])
    run(["git", "-C", destination, "fetch", "-q", "--depth", "1", repository, commit])
    run(["git", "-C", destination, "checkout", "-q", "--detach", "FETCH_HEAD"])
    head = run(["git", "-C", destination, "rev-parse", "HEAD"], capture=True).strip()
    if head != commit:
        raise BuildError(f"{repository}: expected commit {commit}, got {head}")


def verify_sharphook_submodule(work):
    """The submodule pointer of SharpHook's release tag must be the commit we build."""
    sharphook = os.path.join(work, "sharphook")
    os.makedirs(sharphook)
    run(["git", "init", "-q", sharphook])
    run(["git", "-C", sharphook, "fetch", "-q", "--depth", "1", SHARPHOOK_REPOSITORY,
         f"refs/tags/v{SHARPHOOK_VERSION}"])
    entry = run(["git", "-C", sharphook, "ls-tree", "FETCH_HEAD", "libuiohook"], capture=True).split()
    if len(entry) < 3 or entry[1] != "commit":
        raise BuildError(f"SharpHook v{SHARPHOOK_VERSION} has no libuiohook submodule: {entry}")
    if entry[2] != LIBUIOHOOK_COMMIT:
        raise BuildError(f"SharpHook v{SHARPHOOK_VERSION} uses libuiohook {entry[2]}, not {LIBUIOHOOK_COMMIT}.")
    print(f"SharpHook v{SHARPHOOK_VERSION} uses libuiohook {LIBUIOHOOK_COMMIT}.")


def cmake_version():
    output = run(["cmake", "--version"], capture=True)
    match = re.search(r"(\d+)\.(\d+)", output)
    return (int(match.group(1)), int(match.group(2))) if match else (0, 0)


def build_uiohook(output, work):
    machine = platform.machine()
    if machine not in UIOHOOK_FLAGS:
        raise BuildError(f"No libuiohook build flags for {machine}.")
    if cmake_version() < MINIMUM_CMAKE:
        raise BuildError("libuiohook needs CMake 4.2 or newer (on Ubuntu 22.04: python3 -m pip install cmake).")
    verify_sharphook_submodule(work)
    source = os.path.join(work, "libuiohook")
    fetch(LIBUIOHOOK_REPOSITORY, LIBUIOHOOK_COMMIT, source)
    build = os.path.join(work, "uiohook-build")
    env = dict(os.environ, LDFLAGS=UIOHOOK_LDFLAGS, **UIOHOOK_FLAGS[machine])
    # CMAKE_POLICY_VERSION_MINIMUM lets CMake 4 read the older extra-cmake-modules of Ubuntu 22.04.
    run(["cmake", "-S", source, "-B", build, "-G", "Unix Makefiles", "-D", "CMAKE_VERBOSE_MAKEFILE=true",
         "-D", "BUILD_TEST=ON", "-D", "CMAKE_POLICY_VERSION_MINIMUM=3.5"], env=env)
    targets = [name[len("lib"):-len(".so")] for name in UIOHOOK_LIBRARIES] + ["uiohook_tests"]
    run(["cmake", "--build", build, "--parallel", str(os.cpu_count() or 2), "--target", *targets], env=env)
    # The same unit tests SharpHook's CI runs on its build (no display needed).
    run([os.path.join(build, "uiohook_tests"), "--headless"], cwd=build)
    os.makedirs(output, exist_ok=True)
    for name in UIOHOOK_LIBRARIES:
        built = os.path.join(build, name)
        if not os.path.isfile(built):
            raise BuildError(f"The libuiohook build did not produce {name}.")
        shutil.copy2(built, os.path.join(output, name))
    for name in ("COPYING.md", "COPYING.LESSER.md"):
        shutil.copy2(os.path.join(source, name), os.path.join(output, name))
    write_source_note(output, "libuiohook", LIBUIOHOOK_REPOSITORY, LIBUIOHOOK_COMMIT,
                      f"SharpHook {SHARPHOOK_VERSION}; CMake flags of SharpHook's build.yml; {machine}")


def verify_rhwp_tag():
    """Warns when the tag no longer points at the pinned commit (the pinned commit is built either way)."""
    lines = run(["git", "ls-remote", RHWP_REPOSITORY, f"refs/tags/v{RHWP_VERSION}", f"refs/tags/v{RHWP_VERSION}^{{}}"],
                capture=True).splitlines()
    refs = dict(reversed(line.split("\t", 1)) for line in lines if "\t" in line)
    tagged = refs.get(f"refs/tags/v{RHWP_VERSION}^{{}}") or refs.get(f"refs/tags/v{RHWP_VERSION}")
    if tagged != RHWP_COMMIT:
        print(f"::warning::rhwp tag v{RHWP_VERSION} points at {tagged}, the build uses the pinned {RHWP_COMMIT}.")


def build_rhwp(output, work):
    if shutil.which("rustup") is None:
        raise BuildError("rhwp needs rustup (https://rustup.rs).")
    verify_rhwp_tag()
    source = os.path.join(work, "rhwp")
    fetch(RHWP_REPOSITORY, RHWP_COMMIT, source)
    run(["rustup", "toolchain", "install", RUST_TOOLCHAIN, "--profile", "minimal", "--no-self-update"])
    # "+toolchain" takes precedence over the repository's rust-toolchain.toml (whose wasm target the CLI doesn't need).
    env = dict(os.environ, CARGO_TARGET_DIR=os.path.join(work, "rhwp-target"))
    run(["cargo", f"+{RUST_TOOLCHAIN}", "build", "--release", "--locked", "--bin", "rhwp"], cwd=source, env=env)
    program = os.path.join(env["CARGO_TARGET_DIR"], "release", "rhwp")
    version = run([program, "--version"], capture=True)
    print(version.strip())
    if RHWP_VERSION not in version:
        raise BuildError(f"rhwp --version printed {version.strip()!r}, expected {RHWP_VERSION}.")
    # The same layout as rhwp's release archive, which build/fetch-portable-engines.ps1 unpacks for local builds.
    os.makedirs(output, exist_ok=True)
    shutil.copy2(program, os.path.join(output, "rhwp"))
    for name in ("LICENSE", "README.md", "README_EN.md"):
        if os.path.isfile(os.path.join(source, name)):
            shutil.copy2(os.path.join(source, name), os.path.join(output, name))
    write_source_note(output, "rhwp", RHWP_REPOSITORY, RHWP_COMMIT,
                      f"v{RHWP_VERSION}; cargo +{RUST_TOOLCHAIN} build --release --locked --bin rhwp; {platform.machine()}")


def write_source_note(output, name, repository, commit, details):
    with open(os.path.join(output, "SOURCE.txt"), "w", encoding="utf-8") as stream:
        stream.write(f"{name} built from source by Filee's CI on {distribution()}.\n"
                     f"Source: {repository}/tree/{commit}\n{details}\n")


def distribution():
    try:
        with open("/etc/os-release", encoding="utf-8") as stream:
            for line in stream:
                if line.startswith("PRETTY_NAME="):
                    return line.split("=", 1)[1].strip().strip('"')
    except OSError:
        pass
    return platform.platform()


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__.split("\n\n")[0])
    parser.add_argument("component", choices=["check", "uiohook", "rhwp"])
    parser.add_argument("--output", help="folder for the built files (must not exist yet)")
    parser.add_argument("--work", help="scratch folder (default: a new temporary folder)")
    options = parser.parse_args(argv)
    try:
        check()
        if options.component == "check":
            return 0
        if not sys.platform.startswith("linux"):
            raise BuildError("Build the Linux native pieces on Linux.")
        if not options.output:
            raise BuildError("--output is required.")
        output = os.path.abspath(options.output)
        if os.path.exists(output):
            raise BuildError(f"{output} already exists. Use a fresh output folder.")
        work = os.path.abspath(options.work) if options.work else tempfile.mkdtemp(prefix=f"filee-{options.component}-")
        os.makedirs(work, exist_ok=True)
        if options.component == "uiohook":
            build_uiohook(output, work)
        else:
            build_rhwp(output, work)
        print(f"{options.component} ready in {output}: {', '.join(sorted(os.listdir(output)))}")
        return 0
    except BuildError as error:
        print(f"::error::{error}" if os.environ.get("GITHUB_ACTIONS") else f"error: {error}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    sys.exit(main())
