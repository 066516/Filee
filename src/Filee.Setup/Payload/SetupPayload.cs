// The payload appended to Setup.exe:
//
//   [Setup program][Brotli stream: every file of the app, one after the other][manifest JSON][trailer, 72 bytes]
//   (then, once Setup is signed, the Authenticode signature: the trailer sits right before it)
//
// Trailer: "FILEE-PAYLOAD-01", payload offset, compressed length, manifest length (little-endian Int64 each) and the
// SHA-256 of the manifest. The manifest holds the SHA-256 of the compressed stream and of every file. One stream for
// all files compresses far better than a zip (the .NET assemblies share much). Setup checks the stream's hash before
// it changes anything and every file's hash while unpacking.

using System.Buffers;
using System.Buffers.Binary;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

namespace Filee.Setup.Payload;

public sealed class SetupPayload : IDisposable
{
    public const string Magic = "FILEE-PAYLOAD-01";
    public const int TrailerLength = 16 + 8 + 8 + 8 + 32;
    private const int BufferSize = 1 << 20;

    private readonly FileStream _file;

    private SetupPayload(FileStream file, long offset, long length, PayloadManifest manifest)
    {
        _file = file;
        Offset = offset;
        CompressedLength = length;
        Manifest = manifest;
    }

    public PayloadManifest Manifest { get; }

    /// <summary>Where the payload starts: the Setup program before it is the uninstaller.</summary>
    public long Offset { get; }

    public long CompressedLength { get; }

    /// <summary>
    /// Opens the payload of <paramref name="exe"/>; null when it has none (uninstall.exe, a development build).
    /// Throws <see cref="InvalidDataException"/> when there is one but it is damaged.
    /// </summary>
    public static SetupPayload? Open(string exe)
    {
        var file = new FileStream(exe, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete, BufferSize, FileOptions.SequentialScan);
        try
        {
            var end = OverlayEnd(file);
            if (end < TrailerLength)
            {
                file.Dispose();
                return null;
            }
            var trailer = new byte[TrailerLength];
            file.Position = end - TrailerLength;
            file.ReadExactly(trailer);
            if (Encoding.ASCII.GetString(trailer, 0, 16) != Magic)
            {
                file.Dispose();
                return null;
            }
            var offset = BinaryPrimitives.ReadInt64LittleEndian(trailer.AsSpan(16));
            var length = BinaryPrimitives.ReadInt64LittleEndian(trailer.AsSpan(24));
            var manifestLength = BinaryPrimitives.ReadInt64LittleEndian(trailer.AsSpan(32));
            var manifestHash = trailer.AsSpan(40, 32).ToArray();
            if (offset <= 0 || length < 0 || manifestLength <= 0 || manifestLength > 64 << 20
                || offset + length + manifestLength + TrailerLength != end)
                throw new InvalidDataException("The payload trailer is damaged.");
            var manifestBytes = new byte[manifestLength];
            file.Position = offset + length;
            file.ReadExactly(manifestBytes);
            if (!SHA256.HashData(manifestBytes).AsSpan().SequenceEqual(manifestHash))
                throw new InvalidDataException("The payload manifest is damaged.");
            return new SetupPayload(file, offset, length, PayloadManifest.FromJson(manifestBytes));
        }
        catch
        {
            file.Dispose();
            throw;
        }
    }

    /// <summary>
    /// End of the data appended to the program: the file's end, or where its Authenticode signature starts (signing
    /// appends the certificate table after the payload).
    /// </summary>
    internal static long OverlayEnd(Stream file)
    {
        var length = file.Length;
        try
        {
            Span<byte> buffer = stackalloc byte[8];
            file.Position = 0x3C;
            file.ReadExactly(buffer[..4]);
            var pe = BinaryPrimitives.ReadInt32LittleEndian(buffer);
            if (pe <= 0 || pe > length - 24)
                return length;
            file.Position = pe;
            file.ReadExactly(buffer[..4]);
            if (BinaryPrimitives.ReadUInt32LittleEndian(buffer) != 0x00004550) // "PE\0\0"
                return length;
            var optionalHeader = pe + 4 + 20;
            file.Position = optionalHeader;
            file.ReadExactly(buffer[..2]);
            var magic = BinaryPrimitives.ReadUInt16LittleEndian(buffer);
            var directories = optionalHeader + (magic == 0x20B ? 112 : 96);
            file.Position = directories + 4 * 8; // IMAGE_DIRECTORY_ENTRY_SECURITY
            file.ReadExactly(buffer);
            var certificates = BinaryPrimitives.ReadUInt32LittleEndian(buffer);
            var size = BinaryPrimitives.ReadUInt32LittleEndian(buffer[4..]);
            // The certificate table's "address" is a file offset.
            return certificates > 0 && certificates + (long)size == length ? certificates : length;
        }
        catch (EndOfStreamException)
        {
            return length;
        }
    }

    /// <summary>Checks the compressed stream against the manifest. Throws <see cref="InvalidDataException"/>.</summary>
    public void Verify(IProgress<double>? progress = null, CancellationToken cancellation = default)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = ArrayPool<byte>.Shared.Rent(BufferSize);
        try
        {
            _file.Position = Offset;
            var left = CompressedLength;
            while (left > 0)
            {
                cancellation.ThrowIfCancellationRequested();
                var read = _file.Read(buffer, 0, (int)Math.Min(buffer.Length, left));
                if (read == 0)
                    throw new InvalidDataException("The payload is shorter than its manifest says.");
                hash.AppendData(buffer, 0, read);
                left -= read;
                progress?.Report(1 - (double)left / Math.Max(1, CompressedLength));
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
        if (PayloadManifest.Hex(hash.GetHashAndReset()) != Manifest.CompressedSha256)
            throw new InvalidDataException("The payload doesn't match its checksum.");
    }

    /// <summary>
    /// Unpacks every file into <paramref name="folder"/> and checks its size and SHA-256. Reports the bytes written.
    /// Throws <see cref="InvalidDataException"/> when a file doesn't match (the folder is then incomplete).
    /// </summary>
    public void ExtractTo(string folder, IProgress<long>? progress = null, CancellationToken cancellation = default)
    {
        _file.Position = Offset;
        using var compressed = new BoundedReadStream(_file, CompressedLength);
        using var brotli = new BrotliStream(compressed, CompressionMode.Decompress);
        var buffer = ArrayPool<byte>.Shared.Rent(BufferSize);
        long written = 0;
        try
        {
            foreach (var file in Manifest.Files)
            {
                cancellation.ThrowIfCancellationRequested();
                var target = Path.Combine(folder, file.Path);
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                using (var output = new FileStream(target, FileMode.CreateNew, FileAccess.Write, FileShare.None, BufferSize))
                {
                    var left = file.Size;
                    while (left > 0)
                    {
                        var read = brotli.Read(buffer, 0, (int)Math.Min(buffer.Length, left));
                        if (read == 0)
                            throw new InvalidDataException($"The payload ends inside {file.Path}.");
                        hash.AppendData(buffer, 0, read);
                        output.Write(buffer, 0, read);
                        left -= read;
                        written += read;
                        progress?.Report(written);
                    }
                }
                if (PayloadManifest.Hex(hash.GetHashAndReset()) != file.Sha256)
                    throw new InvalidDataException($"{file.Path} doesn't match its checksum.");
            }
            if (brotli.Read(buffer, 0, 1) != 0)
                throw new InvalidDataException("The payload has more data than its manifest lists.");
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    /// <summary>Writes the Setup program without the payload: the uninstaller.</summary>
    public void CopyProgramTo(string path)
    {
        _file.Position = 0;
        using var output = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, BufferSize);
        using var program = new BoundedReadStream(_file, Offset);
        program.CopyTo(output, BufferSize);
    }

    public void Dispose() => _file.Dispose();

    /// <summary>
    /// Appends <paramref name="appFolder"/> to a copy of <paramref name="setupProgram"/>: writes <paramref name="output"/>,
    /// the finished Setup. Brotli quality 11 with the largest window compresses best (and slowest).
    /// </summary>
    public static PayloadManifest Write(string setupProgram, string appFolder, string output, string version,
        IReadOnlyList<EngineOffer> engines, string engineMarker, int quality = 11, int window = 24, Action<string>? log = null)
    {
        appFolder = Path.GetFullPath(appFolder);
        // Similar files next to each other compress better: by extension, then by path.
        var paths = Directory.EnumerateFiles(appFolder, "*", SearchOption.AllDirectories)
            .Select(p => Path.GetRelativePath(appFolder, p))
            .OrderBy(p => Path.GetExtension(p), StringComparer.OrdinalIgnoreCase)
            .ThenBy(p => p, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var files = new List<PayloadFile>();
        var partial = output + ".partial";
        using (var target = new FileStream(partial, FileMode.Create, FileAccess.ReadWrite, FileShare.None, BufferSize))
        {
            using (var program = File.OpenRead(setupProgram))
            {
                if (SetupPayload.OverlayEnd(program) != program.Length)
                    throw new InvalidOperationException("Sign Setup after appending the payload, not before.");
                program.Position = 0;
                program.CopyTo(target);
            }
            var offset = target.Position;
            using var compressedHash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            using (var compressor = new BrotliWriteStream(target, quality, window, compressedHash))
            {
                foreach (var relative in paths)
                {
                    PayloadManifest.CheckPath(relative);
                    using var input = File.OpenRead(Path.Combine(appFolder, relative));
                    using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                    var buffer = new byte[BufferSize];
                    int read;
                    long size = 0;
                    while ((read = input.Read(buffer)) > 0)
                    {
                        hash.AppendData(buffer, 0, read);
                        compressor.Write(buffer, 0, read);
                        size += read;
                    }
                    files.Add(new PayloadFile(relative, size, PayloadManifest.Hex(hash.GetHashAndReset())));
                    log?.Invoke(relative);
                }
            }
            var length = target.Position - offset;
            var manifest = new PayloadManifest(version, files, PayloadManifest.Hex(compressedHash.GetHashAndReset()), engines, engineMarker);
            var json = manifest.ToJson();
            target.Write(json);
            var trailer = new byte[TrailerLength];
            Encoding.ASCII.GetBytes(Magic).CopyTo(trailer, 0);
            BinaryPrimitives.WriteInt64LittleEndian(trailer.AsSpan(16), offset);
            BinaryPrimitives.WriteInt64LittleEndian(trailer.AsSpan(24), length);
            BinaryPrimitives.WriteInt64LittleEndian(trailer.AsSpan(32), json.Length);
            SHA256.HashData(json).CopyTo(trailer, 40);
            target.Write(trailer);
        }
        File.Move(partial, output, overwrite: true);
        using var check = Open(output) ?? throw new InvalidDataException("The payload was not written.");
        return check.Manifest;
    }

    /// <summary>Reads at most <c>length</c> bytes from the current position of a stream it doesn't own.</summary>
    private sealed class BoundedReadStream(Stream inner, long length) : Stream
    {
        private long _left = length;

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (_left <= 0)
                return 0;
            var read = inner.Read(buffer, offset, (int)Math.Min(count, _left));
            _left -= read;
            return read;
        }

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    /// <summary>Brotli with a chosen window (BrotliStream only takes the quality), hashing what it writes.</summary>
    private sealed class BrotliWriteStream(Stream inner, int quality, int window, IncrementalHash hash) : Stream
    {
        private BrotliEncoder _encoder = new(quality, window);
        private readonly byte[] _output = new byte[BufferSize];

        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public override void Write(byte[] buffer, int offset, int count)
        {
            var input = buffer.AsSpan(offset, count);
            while (true)
            {
                var status = _encoder.Compress(input, _output, out var consumed, out var produced, isFinalBlock: false);
                Emit(produced);
                input = input[consumed..];
                if (status == OperationStatus.InvalidData)
                    throw new InvalidDataException("Brotli refused the data.");
                if (input.IsEmpty && status == OperationStatus.Done)
                    return;
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                while (true)
                {
                    var status = _encoder.Compress(ReadOnlySpan<byte>.Empty, _output, out _, out var produced, isFinalBlock: true);
                    Emit(produced);
                    if (status == OperationStatus.Done)
                        break;
                }
                _encoder.Dispose();
            }
            base.Dispose(disposing);
        }

        private void Emit(int produced)
        {
            if (produced == 0)
                return;
            inner.Write(_output, 0, produced);
            hash.AppendData(_output, 0, produced);
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
    }
}
