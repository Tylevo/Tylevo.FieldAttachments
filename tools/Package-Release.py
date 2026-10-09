#!/usr/bin/env python3
"""Create the curated player ZIP after an explicitly approved final build.

Uses only Python's standard library and Windows metadata readers. Does not load
plugin code, modify build inputs, copy configurations, install, or upload.
"""
import argparse
from datetime import datetime, timezone
import hashlib
import json
import os
from pathlib import Path
import re
import stat
import subprocess
import tempfile
import zipfile


VERSION = "1.0.0"
PLUGIN = "Tylevo.FieldAttachments"
PREFIX = f"BepInEx/plugins/{PLUGIN}/"
BUILT_FILES = {f"{PLUGIN}.dll", "README.md", "LICENSE", "build-manifest.json"}
ONLINE_FILES = (
    "docs/USER_GUIDE.md",
    "docs/CONFIGURATION.md",
    "docs/TROUBLESHOOTING.md",
    "docs/images/hk416-attachment-menu.png",
    "docs/images/hk416-inspection.png",
)
GITHUB_FILES = f"https://github.com/Tylevo/Tylevo.FieldAttachments/blob/v{VERSION}/"
RAW_FILES = f"https://raw.githubusercontent.com/Tylevo/Tylevo.FieldAttachments/v{VERSION}/"
PRIVATE_PATH = re.compile(
    r"(?i)(?:[A-Za-z]:[\\/](?:Users|Documents and Settings)[\\/]|/(?:Users|home)/[^/\s\x00]+/)"
)


def require(condition, message):
    if not condition:
        raise ValueError(message)


def sha256(data):
    return hashlib.sha256(data).hexdigest().upper()


def package_readme(data):
    """Keep source Markdown intact; point omitted player docs to this release."""
    text = data.decode("utf-8-sig")
    def replace(match):
        path, separator, fragment = match.group(2).partition("#")
        require(path in ONLINE_FILES, f"Unexpected relative README documentation link: {path}")
        base = RAW_FILES if match.group(1).startswith("!") else GITHUB_FILES
        return match.group(1) + base + path + separator + fragment + ")"
    text = re.sub(r"(!?\[[^\]]*\]\()(docs/[^\s)]+)\)", replace, text)
    require(not re.search(r"\]\(\.?/?docs/", text), "Unconverted README documentation link")
    return text.encode("utf-8")


def regular_file(root, relative):
    require(not relative.startswith("/") and "\\" not in relative and
            all(part not in ("", ".", "..") for part in relative.split("/")),
            f"Unsafe relative file: {relative}")
    current = root
    for component in (None, *relative.split("/")):
        if component is not None:
            current /= component
        info = current.lstat()
        require(not stat.S_ISLNK(info.st_mode) and
                not (getattr(info, "st_file_attributes", 0) & 0x400),
                f"Reparse point or symlink refused: {relative}")
    require(current.is_file(), f"Not a regular file: {relative}")
    return current


def no_private_paths(name, data):
    # Also inspect both UTF-16 alignments for strings in managed DLL metadata.
    # Match home-directory signatures, not arbitrary letter-colon bytes inside
    # compressed bundles/PNGs or the trailing 's:/' in an HTTPS link.
    views = (data.decode("utf-8", errors="replace"),
             data.decode("utf-16-le", errors="ignore"),
             data[1:].decode("utf-16-le", errors="ignore"))
    require(not any(PRIVATE_PATH.search(view) for view in views),
            f"Absolute/private filesystem path in payload: {name}")


def dll_metadata(path):
    environment = os.environ.copy()
    environment["TFA_RELEASE_DLL"] = str(path)
    command = (
        "$ErrorActionPreference='Stop'; "
        "$a=[Reflection.AssemblyName]::GetAssemblyName($env:TFA_RELEASE_DLL); "
        "$v=[Diagnostics.FileVersionInfo]::GetVersionInfo($env:TFA_RELEASE_DLL); "
        "[ordered]@{name=$a.Name;assemblyVersion=$a.Version.ToString();"
        "fileVersion=$v.FileVersion;productVersion=$v.ProductVersion} | ConvertTo-Json -Compress"
    )
    result = subprocess.run(
        ["powershell.exe", "-NoProfile", "-NonInteractive", "-Command", command],
        env=environment, capture_output=True, text=True, check=True, timeout=30,
    )
    metadata = json.loads(result.stdout.strip())
    require(metadata["name"] == PLUGIN, "Unexpected DLL assembly name")
    require(metadata["assemblyVersion"] == VERSION + ".0", "DLL assembly version mismatch")
    require(metadata["fileVersion"] == VERSION + ".0", "DLL file version mismatch")
    require(metadata["productVersion"].split("+")[0] == VERSION, "DLL product version mismatch")
    return metadata


def prepare(root, expected_dll):
    build = root / "dist" / "BepInEx" / "plugins" / PLUGIN
    manifest_bytes = regular_file(build, "build-manifest.json").read_bytes()
    manifest = json.loads(manifest_bytes.decode("utf-8-sig"))
    require(manifest.get("pluginVersion") == VERSION, "Manifest plugin version mismatch")
    require(manifest.get("coreTestsPassed") is True, "Manifest core tests not passed")
    require(manifest.get("readOnlyByDefault") is False, "Attachment changes must be enabled by default")
    require(manifest.get("actualSptVersion") == "4.1.6", "Manifest SPT target mismatch")
    require(manifest.get("authoredTemplateCount") == 194 and
            manifest.get("additionalAliasCount") == 26 and
            manifest.get("original123AnimationHashesPreserved") is True,
            "Manifest coverage/baseline proof mismatch")
    animations = manifest.get("authoredAnimationHashes", {})
    require(isinstance(animations, dict) and len(animations) == 129,
            "Expected exactly 129 animation hashes")
    for name, digest in animations.items():
        require(re.fullmatch(r"Animation/[a-z0-9_]+_presentation\.bundle", name) is not None and
                isinstance(digest, str) and re.fullmatch(r"[A-Fa-f0-9]{64}", digest) is not None,
                "Invalid animation manifest entry")
    allowed_built = BUILT_FILES | set(animations)
    actual_built = set()
    for directory, subdirs, files in os.walk(build, followlinks=False):
        for name in subdirs:
            path = Path(directory) / name
            info = path.lstat()
            require(not path.is_symlink() and not (getattr(info, "st_file_attributes", 0) & 0x400),
                    "Build contains a linked directory")
            require(path.relative_to(build).as_posix() == "Animation", "Unexpected build directory")
        for name in files:
            actual_built.add((Path(directory) / name).relative_to(build).as_posix())
    require(actual_built == allowed_built and len(actual_built) == 133,
            f"Build file allowlist mismatch: extra={sorted(actual_built - allowed_built)}, "
            f"missing={sorted(allowed_built - actual_built)}")
    payload = {}
    sources = {}
    for relative in sorted(allowed_built):
        source = regular_file(build, relative)
        sources[PREFIX + relative] = source
        payload[PREFIX + relative] = source.read_bytes()
    require(len(payload) == 133, "Expected 133 payload files")
    for relative, digest in animations.items():
        require(sha256(payload[PREFIX + relative]) == digest.upper(), f"Bundle hash mismatch: {relative}")
        require(payload[PREFIX + relative] == regular_file(root, "assets/" + relative).read_bytes(),
                f"Build/source bundle mismatch: {relative}")
    for relative in ("README.md", "LICENSE"):
        require(payload[PREFIX + relative] == regular_file(root, relative).read_bytes(),
                f"Build contains stale {relative}")
    plugin_bytes = payload[PREFIX + PLUGIN + ".dll"]
    require(sha256(plugin_bytes) == expected_dll == manifest.get("pluginSha256", "").upper(),
            "Final build DLL pin/manifest mismatch")
    metadata = dll_metadata(sources[PREFIX + PLUGIN + ".dll"])
    source_hashes = {name: sha256(data) for name, data in payload.items()}
    payload[PREFIX + "README.md"] = package_readme(payload[PREFIX + "README.md"])
    for name, data in payload.items():
        no_private_paths(name, data)
    readme = payload[PREFIX + "README.md"].decode("utf-8-sig")
    image_refs = re.findall(r"!\[[^\]]*\]\(([^\s)]+)\)", readme)
    require(set(image_refs) == {RAW_FILES + path for path in ONLINE_FILES[-2:]},
            "README image reference allowlist changed")
    hashes = {name: sha256(data) for name, data in sorted(payload.items())}
    checksums = "".join(f"{digest}  {name}\n" for name, digest in hashes.items()).encode("utf-8")
    return payload, sources, source_hashes, hashes, checksums, metadata


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", type=Path, default=Path(__file__).absolute().parent.parent)
    parser.add_argument("--output", type=Path)
    parser.add_argument("--expected-dll-sha256", required=True)
    parser.add_argument("--build-ready", action="store_true")
    parser.add_argument("--validate-only", action="store_true")
    args = parser.parse_args()
    require(args.build_ready, "Wait for final build approval, then pass --build-ready")
    root = args.root.absolute()
    output = (args.output or root.parent / f"{PLUGIN}-{VERSION}.zip").absolute()
    receipt = root / "dist" / "release-package-receipt.json"
    checksum_file = root / "dist" / "release-package-SHA256SUMS.txt"
    require(not output.exists(), "Output ZIP already exists; refusing to overwrite")
    require(args.validate_only or not receipt.exists(), "Packaging receipt already exists; refusing to overwrite")
    require(args.validate_only or not checksum_file.exists(), "Local checksums already exist; refusing to overwrite")
    expected = args.expected_dll_sha256.upper()
    require(re.fullmatch(r"[A-F0-9]{64}", expected) is not None, "Invalid final DLL SHA-256")
    payload, sources, source_hashes, hashes, checksums, metadata = prepare(root, expected)
    if args.validate_only:
        print(json.dumps({"passed": True, "mode": "validate-only", "payloadFiles": len(payload),
                          "animationBundles": 129, "dll": metadata, "dllSha256": expected}, indent=2))
        return
    handle, temporary_name = tempfile.mkstemp(prefix=output.name + ".", suffix=".partial", dir=output.parent)
    os.close(handle)
    temporary = Path(temporary_name)
    try:
        entries = payload
        with zipfile.ZipFile(temporary, "w", compression=zipfile.ZIP_DEFLATED, compresslevel=9) as archive:
            for name, data in sorted(entries.items()):
                info = zipfile.ZipInfo(name, date_time=(2026, 1, 1, 0, 0, 0))
                info.compress_type = zipfile.ZIP_DEFLATED
                info.create_system = 3
                info.external_attr = 0o100644 << 16
                archive.writestr(info, data)
        with zipfile.ZipFile(temporary, "r") as archive:
            require(len(archive.infolist()) == 133 and set(archive.namelist()) == set(entries),
                    "ZIP entry allowlist mismatch")
            require(archive.testzip() is None, "ZIP CRC validation failed")
            for name, data in entries.items():
                require(archive.read(name) == data, f"ZIP/source mismatch: {name}")
        for name, source in sources.items():
            require(sha256(source.read_bytes()) == source_hashes[name], f"Source changed during packaging: {name}")
        # Windows rename is atomic and refuses an existing destination. Unlike
        # hard links, it also works inside the local filesystem sandbox.
        if os.name == "nt":
            os.rename(temporary, output)
        else:
            os.link(temporary, output)
    finally:
        temporary.unlink(missing_ok=True)
    result = {
        "passed": True, "createdUtc": datetime.now(timezone.utc).isoformat(),
        "version": VERSION, "archive": output.name, "archiveSha256": sha256(output.read_bytes()),
        "archiveBytes": output.stat().st_size, "archiveFiles": 133, "payloadFiles": 133,
        "animationBundles": 129, "dllSha256": expected, "dll": metadata,
        "exactAllowlistPassed": True, "crcPassed": True, "sourceHashesPassed": True,
        "privatePathScanPassed": True, "installed": False, "uploaded": False,
        "readmeDocumentationLinks": "absolute GitHub v1.0.0 URLs; source README unchanged",
        "sourceReadmeSha256": source_hashes[PREFIX + "README.md"],
        "checksumsFile": checksum_file.name,
        "payloadSha256": hashes,
    }
    with checksum_file.open("xb") as stream:
        stream.write(checksums)
    with receipt.open("x", encoding="utf-8", newline="\n") as stream:
        json.dump(result, stream, indent=2)
        stream.write("\n")
    print(json.dumps({key: value for key, value in result.items() if key != "payloadSha256"}, indent=2))


if __name__ == "__main__":
    main()
