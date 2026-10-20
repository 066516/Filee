// What Setup carries: the published Filee (every file with its size and SHA-256), the version, and the conversion
// engines Setup can offer (from EngineDownloads, written at build time by build/tools/make-setup.cs).

using System.Text;
using System.Text.Json;

namespace Filee.Setup.Payload;

/// <summary>One file of the app, relative to the install folder (backslashes).</summary>
public sealed record PayloadFile(string Path, long Size, string Sha256);

/// <summary>An engine package of Settings → Engines, as Setup offers it on a fresh install.</summary>
/// <param name="MarkerFolders">Folders under %LocalAppData%\Filee\engines that are all marked when it is installed.</param>
public sealed record EngineOffer(string Id, IReadOnlyList<string> MarkerFolders, long DownloadSize, long InstalledSize, bool Suggested);

public sealed record PayloadManifest(
    string Version,
    IReadOnlyList<PayloadFile> Files,
    string CompressedSha256,
    IReadOnlyList<EngineOffer> Engines,
    string EngineMarker)
{
    /// <summary>Format of the payload; a Setup refuses a payload with another one.</summary>
    public const int CurrentFormat = 1;

    public long TotalSize => Files.Sum(f => f.Size);

    public byte[] ToJson()
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartObject();
            writer.WriteNumber("format", CurrentFormat);
            writer.WriteString("version", Version);
            writer.WriteString("compression", "brotli");
            writer.WriteString("compressedSha256", CompressedSha256);
            writer.WriteString("engineMarker", EngineMarker);
            writer.WriteStartArray("engines");
            foreach (var engine in Engines)
            {
                writer.WriteStartObject();
                writer.WriteString("id", engine.Id);
                writer.WriteStartArray("markerFolders");
                foreach (var folder in engine.MarkerFolders)
                    writer.WriteStringValue(folder);
                writer.WriteEndArray();
                writer.WriteNumber("downloadSize", engine.DownloadSize);
                writer.WriteNumber("installedSize", engine.InstalledSize);
                writer.WriteBoolean("suggested", engine.Suggested);
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
            writer.WriteStartArray("files");
            foreach (var file in Files)
            {
                writer.WriteStartObject();
                writer.WriteString("path", file.Path);
                writer.WriteNumber("size", file.Size);
                writer.WriteString("sha256", file.Sha256);
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
            writer.WriteEndObject();
        }
        return stream.ToArray();
    }

    /// <summary>Reads and checks a manifest; throws <see cref="InvalidDataException"/> when it isn't one Setup can use.</summary>
    public static PayloadManifest FromJson(ReadOnlySpan<byte> json)
    {
        try
        {
            using var document = JsonDocument.Parse(json.ToArray());
            var root = document.RootElement;
            if (root.GetProperty("format").GetInt32() != CurrentFormat || root.GetProperty("compression").GetString() != "brotli")
                throw new InvalidDataException("Unknown payload format.");
            var files = root.GetProperty("files").EnumerateArray()
                .Select(f => new PayloadFile(f.GetProperty("path").GetString()!, f.GetProperty("size").GetInt64(), f.GetProperty("sha256").GetString()!))
                .ToList();
            foreach (var file in files)
                CheckPath(file.Path);
            if (files.Select(f => f.Path).Distinct(StringComparer.OrdinalIgnoreCase).Count() != files.Count)
                throw new InvalidDataException("The payload lists a file twice.");
            var engines = root.TryGetProperty("engines", out var list)
                ? list.EnumerateArray().Select(e => new EngineOffer(
                    e.GetProperty("id").GetString()!,
                    e.GetProperty("markerFolders").EnumerateArray().Select(m => m.GetString()!).ToList(),
                    e.GetProperty("downloadSize").GetInt64(),
                    e.GetProperty("installedSize").GetInt64(),
                    e.GetProperty("suggested").GetBoolean())).ToList()
                : [];
            return new PayloadManifest(
                root.GetProperty("version").GetString()!,
                files,
                root.GetProperty("compressedSha256").GetString()!,
                engines,
                root.TryGetProperty("engineMarker", out var marker) ? marker.GetString() ?? "" : "");
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException or NullReferenceException)
        {
            throw new InvalidDataException("The payload manifest can't be read.", ex);
        }
    }

    /// <summary>
    /// A relative path that stays inside the install folder: no root, no "..", no drive, no alternate data stream.
    /// The payload is checked by its hash anyway; this keeps a broken one from writing elsewhere.
    /// </summary>
    public static void CheckPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || System.IO.Path.IsPathRooted(path) || path.Contains(':')
            || path.Split('\\', '/').Any(part => part is "" or "." or ".."))
            throw new InvalidDataException($"Bad path in the payload: {path}");
    }

    public static string Hex(byte[] hash) => Convert.ToHexString(hash).ToLowerInvariant();

    internal static Encoding Utf8 { get; } = new UTF8Encoding(false);
}
