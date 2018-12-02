using System;
using System.Buffers;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using Interop = Zstandard.Net.ZstandardInterop;

// ReSharper disable once CheckNamespace
namespace Zstandard.Net
{
    /// <summary>
    /// Provides methods and properties for compressing and decompressing streams by using the Zstandard algorithm.
    /// </summary>
    public class ZstandardStream : Stream
    {
        private Stream _stream;
        private CompressionMode _mode;
        private Boolean _leaveOpen;
        private Boolean _isClosed;
        private Boolean _isDisposed;
        private Boolean _isInitialized;

        private IntPtr _zstream;
        private uint _zstreamInputSize;
        private uint _zstreamOutputSize;

        private byte[] _data;
        private bool _dataDepleted;
        private int _dataPosition;
        private int _dataSize;

        private ZstandardInterop.Buffer _outputBuffer = new ZstandardInterop.Buffer();
        private ZstandardInterop.Buffer _inputBuffer = new ZstandardInterop.Buffer();
        private ArrayPool<byte> _arrayPool = ArrayPool<byte>.Shared;

        /// <summary>
        /// Initializes a new instance of the <see cref="ZstandardStream"/> class by using the specified stream and compression mode, and optionally leaves the stream open.
        /// </summary>
        /// <param name="stream">The stream to compress.</param>
        /// <param name="mode">One of the enumeration values that indicates whether to compress or decompress the stream.</param>
        /// <param name="leaveOpen">true to leave the stream open after disposing the <see cref="ZstandardStream"/> object; otherwise, false.</param>
        public ZstandardStream(Stream stream, CompressionMode mode, bool leaveOpen = false)
        {
            _stream = stream ?? throw new ArgumentNullException(nameof(stream));
            _mode = mode;
            _leaveOpen = leaveOpen;

            if (mode == CompressionMode.Compress)
            {
                _zstreamInputSize = Interop.ZSTD_CStreamInSize().ToUInt32();
                _zstreamOutputSize = Interop.ZSTD_CStreamOutSize().ToUInt32();
                _zstream = Interop.ZSTD_createCStream();
                _data = _arrayPool.Rent((int)_zstreamOutputSize);
            }

            if (mode == CompressionMode.Decompress)
            {
                _zstreamInputSize = Interop.ZSTD_DStreamInSize().ToUInt32();
                _zstreamOutputSize = Interop.ZSTD_DStreamOutSize().ToUInt32();
                _zstream = Interop.ZSTD_createDStream();
                _data = _arrayPool.Rent((int)_zstreamInputSize);
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ZstandardStream"/> class  by using the specified stream and compression level, and optionally leaves the stream open.
        /// </summary>
        /// <param name="stream">The stream to compress.</param>
        /// <param name="compressionLevel">The compression level.</param>
        /// <param name="leaveOpen">true to leave the stream open after disposing the <see cref="ZstandardStream"/> object; otherwise, false.</param>
        public ZstandardStream(Stream stream, int compressionLevel, bool leaveOpen = false) : this(stream, CompressionMode.Compress, leaveOpen)
        {
            CompressionLevel = compressionLevel;
        }

        //-----------------------------------------------------------------------------------------
        //-----------------------------------------------------------------------------------------

        /// <summary>
        /// The version of the native Zstd library.
        /// </summary>
        public static Version Version
        {
            get
            {
                var version = (int)Interop.ZSTD_versionNumber();
                return new Version((version / 10000) % 100, (version / 100) % 100, version % 100);
            }
        }

        /// <summary>
        /// The maximum compression level supported by the native Zstd library.
        /// </summary>
        public static int MaxCompressionLevel
        {
            get
            {
                return Interop.ZSTD_maxCLevel();
            }
        }

        //-----------------------------------------------------------------------------------------
        //-----------------------------------------------------------------------------------------

        /// <summary>
        /// Gets or sets the compression level to use, the default is 6.
        /// </summary>
        /// <remarks>
        /// To get the maximum compression level see <see cref="MaxCompressionLevel"/>.
        /// </remarks>
        public int CompressionLevel { get; set; } = 6;

        /// <summary>
        /// Gets or sets the compression dictionary tp use, the default is null.
        /// </summary>
        /// <value>
        /// The compression dictionary.
        /// </value>
        public ZstandardDictionary CompressionDictionary { get; set; } = null;

        /// <summary>
        /// Gets whether the current stream supports reading.
        /// </summary>
        public override bool CanRead => _stream.CanRead && _mode == CompressionMode.Decompress;

        /// <summary>
        ///  Gets whether the current stream supports writing.
        /// </summary>
        public override bool CanWrite => _stream.CanWrite && _mode == CompressionMode.Compress;

        /// <summary>
        ///  Gets whether the current stream supports seeking.
        /// </summary>
        public override bool CanSeek => false;

        /// <summary>
        /// Gets the length in bytes of the stream.
        /// </summary>
        public override long Length => throw new NotSupportedException();

        /// <summary>
        /// Gets or sets the position within the current stream.
        /// </summary>
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        //-----------------------------------------------------------------------------------------
        //-----------------------------------------------------------------------------------------

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            if (_isDisposed == false)
            {
                _arrayPool.Return(_data);
                _isDisposed = true;
                _data = null;
            }
        }

        public override void Close()
        {
            if (_isClosed)
            {
                // do nothing
            }
            else if (_mode == CompressionMode.Compress)
            {
                ProcessStream((zcs, buffer) => Interop.ThrowIfError(Interop.ZSTD_flushStream(zcs, buffer)));
                ProcessStream((zcs, buffer) => Interop.ThrowIfError(Interop.ZSTD_endStream(zcs, buffer)));
                _stream.Flush();

                Interop.ZSTD_freeCStream(_zstream);
                if (!_leaveOpen) _stream.Close();
            }
            else if (_mode == CompressionMode.Decompress)
            {
                Interop.ZSTD_freeDStream(_zstream);
                if (!_leaveOpen) _stream.Close();
            }

            _isClosed = true;
            base.Close();
        }

        public override void Flush()
        {
            if (_mode == CompressionMode.Compress)
            {
                ProcessStream((zcs, buffer) => Interop.ThrowIfError(Interop.ZSTD_flushStream(zcs, buffer)));
                _stream.Flush();
            }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (CanRead == false) throw new NotSupportedException();

            // prevent the buffers from being moved around by the garbage collector
            var alloc1 = GCHandle.Alloc(buffer, GCHandleType.Pinned);
            var alloc2 = GCHandle.Alloc(_data, GCHandleType.Pinned);

            try
            {
                var length = 0;

                if (_isInitialized == false)
                {
                    _isInitialized = true;

                    // ReSharper disable once UnusedVariable
                    var result = CompressionDictionary == null
                        ? Interop.ZSTD_initDStream(_zstream)
                        : Interop.ZSTD_initDStream_usingDDict(_zstream, CompressionDictionary.GetDecompressionDictionary());
                }

                while (count > 0)
                {
                    var inputSize = _dataSize - _dataPosition;

                    // read data from input stream 
                    if (inputSize <= 0 && _dataDepleted == false)
                    {
                        _dataSize = _stream.Read(_data, 0, (int)_zstreamInputSize);
                        _dataDepleted = _dataSize <= 0;
                        _dataPosition = 0;
                        inputSize = _dataDepleted ? 0 : _dataSize;
                    }

                    // configure the inputBuffer
                    _inputBuffer.Data = _dataDepleted ? IntPtr.Zero : Marshal.UnsafeAddrOfPinnedArrayElement(_data, _dataPosition);
                    _inputBuffer.Size = _dataDepleted ? UIntPtr.Zero : new UIntPtr((uint)inputSize);
                    _inputBuffer.Position = UIntPtr.Zero;

                    // configure the outputBuffer
                    _outputBuffer.Data = Marshal.UnsafeAddrOfPinnedArrayElement(buffer, offset);
                    _outputBuffer.Size = new UIntPtr((uint)count);
                    _outputBuffer.Position = UIntPtr.Zero;

                    // decompress inputBuffer to outputBuffer
                    Interop.ThrowIfError(Interop.ZSTD_decompressStream(_zstream, _outputBuffer, _inputBuffer));

                    // calculate progress in outputBuffer
                    var outputBufferPosition = (int)_outputBuffer.Position.ToUInt32();
                    if (outputBufferPosition == 0 && _dataDepleted) break;
                    length += outputBufferPosition;
                    offset += outputBufferPosition;
                    count -= outputBufferPosition;

                    // calculate progress in inputBuffer
                    var inputBufferPosition = (int)_inputBuffer.Position.ToUInt32();
                    _dataPosition += inputBufferPosition;
                }

                return length;
            }
            finally
            {
                alloc1.Free();
                alloc2.Free();
            }
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            if (CanWrite == false) throw new NotSupportedException();

            // prevent the buffers from being moved around by the garbage collector
            var alloc1 = GCHandle.Alloc(buffer, GCHandleType.Pinned);
            var alloc2 = GCHandle.Alloc(_data, GCHandleType.Pinned);

            try
            {
                if (_isInitialized == false)
                {
                    _isInitialized = true;

                    // ReSharper disable once UnusedVariable
                    var result = CompressionDictionary == null
                        ? Interop.ZSTD_initCStream(_zstream, CompressionLevel)
                        : Interop.ZSTD_initCStream_usingCDict(_zstream, CompressionDictionary.GetCompressionDictionary(CompressionLevel));
                }

                while (count > 0)
                {
                    var inputSize = Math.Min((uint)count, _zstreamInputSize);

                    // configure the outputBuffer
                    _outputBuffer.Data = Marshal.UnsafeAddrOfPinnedArrayElement(_data, 0);
                    _outputBuffer.Size = new UIntPtr(_zstreamOutputSize);
                    _outputBuffer.Position = UIntPtr.Zero;

                    // configure the inputBuffer
                    _inputBuffer.Data = Marshal.UnsafeAddrOfPinnedArrayElement(buffer, offset);
                    _inputBuffer.Size = new UIntPtr(inputSize);
                    _inputBuffer.Position = UIntPtr.Zero;

                    // compress inputBuffer to outputBuffer
                    Interop.ThrowIfError(Interop.ZSTD_compressStream(_zstream, _outputBuffer, _inputBuffer));

                    // write data to output stream
                    var outputBufferPosition = (int)_outputBuffer.Position.ToUInt32();
                    _stream.Write(_data, 0, outputBufferPosition);

                    // calculate progress in inputBuffer
                    var inputBufferPosition = (int)_inputBuffer.Position.ToUInt32();
                    offset += inputBufferPosition;
                    count -= inputBufferPosition;
                }
            }
            finally
            {
                alloc1.Free();
                alloc2.Free();
            }
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotImplementedException();
        }

        public override void SetLength(long value)
        {
            throw new NotImplementedException();
        }

        //-----------------------------------------------------------------------------------------
        //-----------------------------------------------------------------------------------------

        private void ProcessStream(Action<IntPtr, ZstandardInterop.Buffer> outputAction)
        {
            var alloc = GCHandle.Alloc(_data, GCHandleType.Pinned);

            try
            {
                _outputBuffer.Data = Marshal.UnsafeAddrOfPinnedArrayElement(_data, 0);
                _outputBuffer.Size = new UIntPtr(_zstreamOutputSize);
                _outputBuffer.Position = UIntPtr.Zero;

                outputAction(_zstream, _outputBuffer);

                var outputBufferPosition = (int)_outputBuffer.Position.ToUInt32();
                _stream.Write(_data, 0, outputBufferPosition);
            }
            finally
            {
                alloc.Free();
            }
        }
    }
}
