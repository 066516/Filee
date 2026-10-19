# Live and Motion Photos

Filee can convert short SDR videos into Apple Live Photos or standard Android Motion Photos, and export the motion from supported camera photos as MP4/MOV. Install **FFmpeg** in Settings → Engines first.

## Create a photo from a video

In Settings → Presets, choose **Live Photo (iPhone · LIVP)** or **Motion Photo (Android)**. Set the clip start, length (0.1–10 seconds, default 3), and cover time relative to the clip start. A cover beyond the clip is limited to its last frame. The clip is shortened at the end of the source. Audio is kept unless **Remove audio** is selected. Video is encoded as H.264 at 30 fps, with AAC audio when present; surround sound is mixed to stereo. The usual quality and maximum-height options apply. HDR creation is refused rather than silently producing an incorrectly rendered cover; export an SDR clip first.

The new presets are added to the preset library when upgrading. Add them to a toolbar in Settings → Donut toolbar; existing donut assignments are preserved.

### iPhone

The output is a `.livp` ZIP containing a same-named JPEG/MOV pair with one shared Apple content identifier. The JPEG contains Apple maker-note tag 17; the movie has movie-level pairing metadata and a boxed timed metadata track marking the selected cover frame.

**A LIVP is a transport package, not an Apple Photos import API.** Unpack it and use an importer that submits the JPEG and paired MOV as one Live Photo (for example, an application using `PHAssetCreationRequest` with photo and paired-video resources). Sending the ZIP to Files, or importing the two assets independently, does not ensure that Photos creates a Live Photo. This implementation has generated-fixture and ffprobe validation, but has **not been verified by importing into Apple Photos on an iPhone or Mac**. On-device import remains a required compatibility check.

### Android

The output is a `.motion.jpg` JPEG with Google Motion Photo 1.0 XMP container metadata and an appended MP4. Copy it without recompressing to a gallery that supports standard Motion Photos. Renaming it to `.jpg` preserves the data; an editor or messaging service that re-encodes only the still will discard the motion.

This targets the **published Motion Photo format**, not every manufacturer's private variant. Google Photos / Pixel and compatible Samsung galleries are intended targets, but **no phone-gallery import has been verified yet**. Xiaomi, OPPO, vivo, Huawei, and older Samsung SEF-only photos require representative files and device testing before claiming compatibility. No private vendor-specific writer is included.

## Export the motion as video

- Drop a `.livp` or `.motion.jpg` onto MP4/MOV, or use the **Extract live video** preset.
- For a camera `.jpg`, `.heic`, or `.avif`, use **Extract live video**, or enable **Extract Live/Motion Photo video** in an MP4/MOV preset. This is explicit so ordinary image-to-video conversions keep their existing still-picture behavior.
- Android input supports JPEG Motion Photo 1.0 container-length metadata, legacy Google Microvideo offset metadata, and HEIC/AVIF top-level `mpvd` video boxes. XMP namespace prefixes may differ. A disabled or stale motion flag does not authorize guessing a video by scanning image bytes.
- Apple input supports LIVP packages and adjacent same-basename JPEG/HEIC + MOV exports. Both content identifiers must match; same names alone are insufficient. Keep both original assets together. The ZIP may contain one pair only; assets are copied into private scratch paths, never extracted using archive-supplied paths.

The image and source video are never modified. A normal still, missing companion, invalid offset, mismatched identifier, or unsupported vendor layout reports an error instead of silently returning a static clip. Extracted embedded videos are limited to 512 MiB; LIVP still entries are limited to 64 MiB. Exported MP4/MOV files follow the existing FFmpeg preset's encoding/remux rules, including optional audio removal.

## Verification and references

Tests generate their own videos and covers. They exercise both output types with/without audio, round trips, trimming, cover timing, ordinary-photo behavior, namespace variants, invalid offsets, pairing mismatches, and malformed boxes. ffprobe independently checks that the Apple timed marker is a `mebx` stream at the selected presentation time.

- [Android Motion Photo 1.0 specification](https://developer.android.com/media/platform/motion-photo-format)
- [Apple timed metadata sample descriptions](https://developer.apple.com/documentation/quicktime-file-format/timed_metadata_sample_descriptions)
- [Apple paired video resource](https://developer.apple.com/documentation/photos/phassetresourcetype/pairedvideo)
- [ExifTool's Apple maker-note tag documentation](https://exiftool.org/TagNames/Apple.html)

Related request: [#174](https://github.com/KnifeLemon/Filee/issues/174). Vendor compatibility and device validation remain open work.
