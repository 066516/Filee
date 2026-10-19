// Android JPEG Motion Photo metadata and bounded extraction of the video at the end of a camera image.

using System.Buffers.Binary;
using System.Globalization;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace Filee.Engines.Media;

/// <summary>Google Motion Photo 1.0 JPEGs and legacy Microvideo JPEGs.</summary>
internal static class MotionPhoto
{
    private static readonly byte[] XmpHeader = "http://ns.adobe.com/xap/1.0/\0"u8.ToArray();
    private const string Camera = "http://ns.google.com/photos/1.0/camera/";
    private const string Container = "http://ns.google.com/photos/1.0/container/";
    private const string Item = "http://ns.google.com/photos/1.0/container/item/";

    /// <summary>Writes one standard JPEG Motion Photo, preserving the encoded picture and video bytes.</summary>
    public static async Task WriteAsync(string jpeg, string video, string output, long coverMicroseconds, CancellationToken ct)
    {
        var length = new FileInfo(video).Length;
        var xml = $"""
            <x:xmpmeta xmlns:x="adobe:ns:meta/">
              <rdf:RDF xmlns:rdf="http://www.w3.org/1999/02/22-rdf-syntax-ns#">
                <rdf:Description xmlns:Camera="{Camera}" xmlns:Container="{Container}" xmlns:Item="{Item}"
                  Camera:MotionPhoto="1" Camera:MotionPhotoVersion="1" Camera:MotionPhotoPresentationTimestampUs="{coverMicroseconds.ToString(CultureInfo.InvariantCulture)}">
                  <Container:Directory><rdf:Seq>
                    <rdf:li rdf:parseType="Resource"><Container:Item Item:Mime="image/jpeg" Item:Semantic="Primary" Item:Length="0" Item:Padding="0" /></rdf:li>
                    <rdf:li rdf:parseType="Resource"><Container:Item Item:Mime="video/mp4" Item:Semantic="MotionPhoto" Item:Length="{length.ToString(CultureInfo.InvariantCulture)}" /></rdf:li>
                  </rdf:Seq></Container:Directory>
                </rdf:Description>
              </rdf:RDF>
            </x:xmpmeta>
            """;
        var payload = XmpHeader.Concat(Encoding.UTF8.GetBytes(xml)).ToArray();
        await using var image = File.OpenRead(jpeg);
        if (image.ReadByte() != 0xff || image.ReadByte() != 0xd8)
            throw new InvalidDataException("The Motion Photo cover is not a JPEG.");
        await using var dest = File.Create(output);
        dest.Write([0xff, 0xd8, 0xff, 0xe1]);
        var size = new byte[2];
        BinaryPrimitives.WriteUInt16BigEndian(size, checked((ushort)(payload.Length + 2)));
        dest.Write(size);
        await dest.WriteAsync(payload, ct);
        await image.CopyToAsync(dest, ct);
        await using var movie = File.OpenRead(video);
        await movie.CopyToAsync(dest, ct);
    }

    /// <summary>Finds the declared video, never by searching arbitrary image bytes for an MP4 signature.</summary>
    public static (long Offset, long Length)? FindVideo(Stream file)
    {
        file.Position = 0;
        if (file.ReadByte() == 0xff && file.ReadByte() == 0xd8)
            return FindJpegVideo(file);
        file.Position = 0;
        while (file.Position < file.Length)
        {
            var box = MovieBoxes.ReadHeader(file);
            if (box.Type == "mpvd")
            {
                if (box.End != file.Length || box.Length <= box.HeaderLength)
                    throw new InvalidDataException("Invalid Motion Photo video box.");
                return (box.Start + box.HeaderLength, box.Length - box.HeaderLength);
            }
            file.Position = box.End;
        }
        return null;
    }

    private static (long Offset, long Length)? FindJpegVideo(Stream file)
    {
        // Metadata must precede JPEG scan data. Bound the walk even for a file made of thousands of segments.
        while (file.Position < Math.Min(file.Length, 8 * 1024 * 1024))
        {
            if (file.ReadByte() != 0xff)
                throw new InvalidDataException("Invalid JPEG marker.");
            int marker;
            do { marker = file.ReadByte(); } while (marker == 0xff);
            if (marker is 0xda or 0xd9)
                return null;
            if (marker < 0)
                throw new EndOfStreamException();
            var size = new byte[2];
            file.ReadExactly(size);
            var length = BinaryPrimitives.ReadUInt16BigEndian(size) - 2;
            if (length < 0 || length > file.Length - file.Position)
                throw new InvalidDataException("Invalid JPEG segment length.");
            if (marker == 0xe1)
            {
                var data = new byte[length];
                file.ReadExactly(data);
                if (data.AsSpan().StartsWith(XmpHeader))
                {
                    using var reader = XmlReader.Create(new MemoryStream(data, XmpHeader.Length, data.Length - XmpHeader.Length),
                        new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 65535 });
                    var xml = XDocument.Load(reader);
                    var attrs = xml.Descendants().Attributes().ToList();
                    string? Value(string name) => attrs.FirstOrDefault(a => a.Name == XName.Get(name, Camera))?.Value;
                    long videoLength;
                    if (Value("MotionPhoto") is { } motion)
                    {
                        if (motion != "1")
                            return null;
                        var items = xml.Descendants(XName.Get("Item", Container)).ToList();
                        var last = items.LastOrDefault();
                        if (last?.Attribute(XName.Get("Semantic", Item))?.Value != "MotionPhoto"
                            || last.Attribute(XName.Get("Mime", Item))?.Value is not ("video/mp4" or "video/quicktime")
                            || !long.TryParse(last.Attribute(XName.Get("Length", Item))?.Value, NumberStyles.None, CultureInfo.InvariantCulture, out videoLength))
                            throw new InvalidDataException("Motion Photo has no valid video directory entry.");
                    }
                    else if (Value("MicroVideo") == "1"
                             && long.TryParse(Value("MicroVideoOffset"), NumberStyles.None, CultureInfo.InvariantCulture, out videoLength))
                    {
                        // Microvideo v1 measured backwards from EOF; Motion Photo 1.0 replaced this property.
                    }
                    else
                        continue;
                    var start = file.Length - videoLength;
                    if (videoLength < 12 || start < file.Position)
                        throw new InvalidDataException("Motion Photo video length is outside the file.");
                    ValidateVideo(file, start, videoLength);
                    return (start, videoLength);
                }
            }
            else
                file.Position += length;
        }
        return null;
    }

    /// <summary>Requires a real ISO media header at the declared boundary, not stale XMP left by an image editor.</summary>
    public static void ValidateVideo(Stream file, long start, long length)
    {
        file.Position = start;
        var box = MovieBoxes.ReadHeader(file);
        if (box.Type != "ftyp" || box.Length < 16 || box.Length > length)
            throw new InvalidDataException("The image no longer contains its declared Motion Photo video.");
    }
}
