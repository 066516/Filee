// Apple Live Photo pairing and a timed still-image marker. Only private FFmpeg scratch movies are rewritten.

using System.Buffers.Binary;
using System.Text;
using ImageMagick;
using static Filee.Engines.Media.MovieBoxes;

namespace Filee.Engines.Media;

internal static class AppleLivePhoto
{
    internal const string ContentIdentifier = "com.apple.quicktime.content.identifier";
    internal const string StillImageTime = "com.apple.quicktime.still-image-time";
    private const int MaxMovieHeader = 16 * 1024 * 1024;

    public static void WriteCover(string png, string jpeg, string identifier)
    {
        using var image = new MagickImage(png);
        var profile = new ExifProfile();
        profile.SetValue(ExifTag.MakerNote, MakerNote(identifier));
        image.SetProfile(profile);
        image.Quality = 90;
        image.Write(jpeg, MagickFormat.Jpeg);
    }

    internal static byte[] MakerNote(string identifier)
    {
        var id = Utf8(Guid.Parse(identifier).ToString("D").ToUpperInvariant() + "\0");
        // Apple maker-note offsets are relative to the note, whose IFD starts at byte 14.
        return Join(Utf8("Apple iOS\0"), U16(1), Utf8("MM"), U16(1),
            U16(17), U16(2), U32((uint)id.Length), U32(32), U32(0), id);
    }

    public static string? ImageIdentifier(string imagePath)
    {
        using var image = new MagickImage();
        image.Ping(imagePath);
        return ReadMakerNote(image.GetExifProfile()?.GetValue(ExifTag.MakerNote)?.Value);
    }

    internal static string? ReadMakerNote(byte[]? data)
    {
        if (data is null || data.Length < 16 || !data.AsSpan().StartsWith("Apple iOS\0"u8))
            return null;
        var little = data.AsSpan(12, 2).SequenceEqual("II"u8);
        if (!little && !data.AsSpan(12, 2).SequenceEqual("MM"u8))
            return null;
        ushort Read16(int at) => little ? BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(at)) : BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(at));
        uint Read32(int at) => little ? BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(at)) : BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(at));
        var count = Read16(14);
        if (16L + count * 12L > data.Length)
            return null;
        for (var i = 0; i < count; i++)
        {
            var at = 16 + i * 12;
            if (Read16(at) != 17 || Read16(at + 2) != 2)
                continue;
            var length = Read32(at + 4);
            var offset = Read32(at + 8);
            if (length < 36 || length > 128 || offset > data.Length || length > data.Length - offset)
                return null;
            var id = Encoding.ASCII.GetString(data, (int)offset, (int)length).TrimEnd('\0');
            return Guid.TryParse(id, out var guid) ? guid.ToString("D") : null;
        }
        return null;
    }

    private static (MovieBox Box, byte[] Payload) ReadMovie(Stream file)
    {
        file.Position = 0;
        while (file.Position < file.Length)
        {
            var box = ReadHeader(file);
            if (box.Type == "moov")
            {
                if (box.Length > MaxMovieHeader)
                    throw new InvalidDataException("Live Photo movie metadata is too large.");
                var data = new byte[checked((int)(box.Length - box.HeaderLength))];
                file.ReadExactly(data);
                return (box, data);
            }
            file.Position = box.End;
        }
        throw new InvalidDataException("Live Photo has no movie metadata.");
    }

    public static string? MovieIdentifier(string path)
    {
        using var file = File.OpenRead(path);
        var (_, movie) = ReadMovie(file);
        var children = Children(movie);
        // Apple's movie-level meta is a plain container; FFmpeg also encounters a full-box meta under udta.
        var metadata = children.Where(c => c.Type == "meta").Select(c => c.Payload).ToList();
        foreach (var udta in children.Where(c => c.Type == "udta"))
            metadata.AddRange(Children(udta.Payload).Where(c => c.Type == "meta").Select(c => c.Payload));
        foreach (var data in metadata)
        {
            var atoms = Children(data, data.Length >= 4 && data.AsSpan(0, 4).SequenceEqual(new byte[4]) ? 4 : 0);
            var keys = atoms.FirstOrDefault(c => c.Type == "keys").Payload;
            var values = atoms.FirstOrDefault(c => c.Type == "ilst").Payload;
            if (keys is null || values is null || keys.Length < 8)
                continue;
            var keyList = Children(keys, 8);
            var index = keyList.FindIndex(k => k.Type == "mdta" && Encoding.UTF8.GetString(k.Payload) == ContentIdentifier) + 1;
            if (index == 0)
                continue;
            foreach (var value in Children(values).Where(v => v.Type == Encoding.Latin1.GetString(U32((uint)index))))
            {
                var item = Children(value.Payload).FirstOrDefault(c => c.Type == "data").Payload;
                if (item is { Length: > 8 } && Guid.TryParse(Encoding.UTF8.GetString(item, 8, item.Length - 8), out var id))
                    return id.ToString("D");
            }
        }
        return null;
    }

    /// <summary>Adds movie-level pairing keys and one signed-byte timed metadata sample at the still frame.</summary>
    public static void TagMovie(string path, string identifier, double coverSeconds)
    {
        using var file = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        var (box, movie) = ReadMovie(file);
        if (box.End != file.Length)
            throw new InvalidDataException("Expected FFmpeg's trailing movie header.");
        var children = Children(movie);
        var mvhd = children.Single(c => c.Type == "mvhd").Payload;
        var scaleAt = mvhd[0] == 1 ? 20 : 12;
        var scale = BinaryPrimitives.ReadUInt32BigEndian(mvhd.AsSpan(scaleAt));
        var nextId = BinaryPrimitives.ReadUInt32BigEndian(mvhd.AsSpan(mvhd.Length - 4));
        if (scale == 0 || nextId == uint.MaxValue)
            throw new InvalidDataException("Invalid movie timescale or track id.");
        BinaryPrimitives.WriteUInt32BigEndian(mvhd.AsSpan(mvhd.Length - 4), nextId + 1);
        var videoTrack = children.First(c => c.Type == "trak");
        var tkhd = Children(videoTrack.Payload).Single(c => c.Type == "tkhd").Payload;
        var videoId = BinaryPrimitives.ReadUInt32BigEndian(tkhd.AsSpan(tkhd[0] == 1 ? 20 : 12));
        var sample = Box("\0\0\0\u0001", [0xff]); // signed int8 marker, one local key
        var sampleOffset = checked((ulong)(box.Start + 8));
        var track = StillTrack(nextId, videoId, scale, coverSeconds, sampleOffset, (uint)sample.Length);
        var metadata = PairingMetadata(identifier);
        // No media data moves: existing stco/co64 entries still point at the same bytes.
        file.SetLength(box.Start);
        file.Position = box.Start;
        file.Write(Box("mdat", sample));
        file.Write(Box("moov", [.. children.Where(c => c.Type != "meta").Select(c => Box(c.Type, c.Payload)), track, metadata]));
    }

    private static byte[] PairingMetadata(string identifier)
    {
        var keys = new[] { ContentIdentifier, "com.apple.quicktime.live-photo.auto", "com.apple.quicktime.full-frame-rate-playback-intent" };
        var values = new[] { Box("data", U32(1), U32(0), Utf8(identifier)), Box("data", U32(21), U32(0), [1]), Box("data", U32(21), U32(0), U64(1)) };
        return Box("meta", Full("hdlr", Join(U32(0), Utf8("mdta"), new byte[13])),
            Full("keys", Join(U32((uint)keys.Length), Join([.. keys.Select(k => Box("mdta", Utf8(k)))]))),
            Box("ilst", [.. values.Select((v, i) => Box(Encoding.Latin1.GetString(U32((uint)i + 1)), v))]));
    }

    private static byte[] StillTrack(uint id, uint videoId, uint movieScale, double cover, ulong offset, uint sampleSize)
    {
        const uint timeScale = 1000;
        var start = checked((uint)Math.Round(cover * movieScale));
        var tick = Math.Max(1, movieScale / timeScale);
        var matrix = Join(U32(0x10000), U32(0), U32(0), U32(0), U32(0x10000), U32(0), U32(0), U32(0), U32(0x40000000));
        var tkhd = Full("tkhd", Join(U32(0), U32(0), U32(id), U32(0), U32(start + tick), new byte[16], matrix, new byte[8]), 3);
        var edits = new List<byte[]>();
        if (start > 0)
            edits.Add(Join(U32(start), U32(uint.MaxValue), U16(1), U16(0)));
        edits.Add(Join(U32(tick), U32(0), U16(1), U16(0)));
        var mebx = Box("mebx", new byte[6], U16(1), Box("keys", Box("\0\0\0\u0001",
            Box("keyd", Utf8("mdta" + StillImageTime)), Box("dtyp", U32(1), Utf8("com.apple.metadata.datatype.int8")))));
        var table = Box("stbl", Full("stsd", Join(U32(1), mebx)), Full("stts", Join(U32(1), U32(1), U32(1))),
            Full("stsc", Join(U32(1), U32(1), U32(1), U32(1))), Full("stsz", Join(U32(sampleSize), U32(1))),
            Full("co64", Join(U32(1), U64(offset))));
        var minf = Box("minf", Full("nmhd", []), Box("dinf", Full("dref", Join(U32(1), Full("url ", [], 1)))), table);
        var mdia = Box("mdia", Full("mdhd", Join(U32(0), U32(0), U32(timeScale), U32(1), U16(0x55c4), U16(0))),
            Full("hdlr", Join(U32(0), Utf8("mdta"), new byte[12], Utf8("Core Media Metadata\0"))), minf);
        return Box("trak", tkhd, Box("edts", Full("elst", Join(U32((uint)edits.Count), Join([.. edits])))),
            Box("tref", Box("cdsc", U32(videoId))), mdia);
    }
}
