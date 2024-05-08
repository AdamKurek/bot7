using Discord.Audio;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace bot7
{
    public class EndOfStreamWrapper : Stream
    {
        private readonly AudioInStream _innerStream;

        public EndOfStreamWrapper(AudioInStream innerStream)
        {
            _innerStream = innerStream;
        }

        public override bool CanRead => _innerStream.CanRead;

        public override bool CanSeek => _innerStream.CanSeek;

        public override bool CanWrite => _innerStream.CanWrite;

        public override long Length => _innerStream.Length;

        public override long Position { get => _innerStream.Position; set => _innerStream.Position = value; }

        public override void Flush()
        {
            _innerStream.Flush();
        }

       

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (MemoryMarshal.TryGetArray(buffer, out ArraySegment<byte> array))
            {
                return new ValueTask<int>(ReadAsync(array.Array!, array.Offset, array.Count, cancellationToken));
            }

            byte[] sharedBuffer = ArrayPool<byte>.Shared.Rent(buffer.Length);
            return FinishReadAsync(ReadAsync(sharedBuffer, 0, buffer.Length, cancellationToken), sharedBuffer, buffer);

            static async ValueTask<int> FinishReadAsync(Task<int> readTask, byte[] localBuffer, Memory<byte> localDestination)
            {
                try
                {
                    int result = await readTask.ConfigureAwait(false);
                    new ReadOnlySpan<byte>(localBuffer, 0, result).CopyTo(localDestination.Span);
                    return result;
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(localBuffer);
                }
            }
        }

        new public Task CopyToAsync(Stream destination) => CopyToAsync(destination, GetCopyBufferSize());

        new public Task CopyToAsync(Stream destination, int bufferSize) => CopyToAsync(destination, bufferSize, CancellationToken.None);

        new public Task CopyToAsync(Stream destination, CancellationToken cancellationToken) => CopyToAsync(destination, GetCopyBufferSize(), cancellationToken);

        public override Task CopyToAsync(Stream destination, int bufferSize, CancellationToken cancellationToken)
        {
            ValidateCopyToArguments(destination, bufferSize);
            if (!CanRead)
            {
                if (CanWrite)
                {
                    throw new InvalidOperationException("you can't write tho");
                }
                throw new InvalidOperationException("you can't read tho");
            }

            return Core(this, destination, bufferSize, cancellationToken);

            static async Task Core(Stream source, Stream destination, int bufferSize, CancellationToken cancellationToken)
            {
                byte[] buffer = ArrayPool<byte>.Shared.Rent(bufferSize);
                var cts = new CancellationTokenSource();
                var token = cts.Token;

                using var timer = new System.Timers.Timer() { AutoReset = false,  };
                timer.Interval = 300;
                timer.Elapsed += (o,s)=>{ 
                        cts.Cancel();
                };
                timer.AutoReset = false; 
                try
                {
                    int bytesRead;
                    while ((bytesRead = await source.ReadAsync(new Memory<byte>(buffer), token).ConfigureAwait(false)) != 0)
                    {
                        timer.Stop();
                        await destination.WriteAsync(new ReadOnlyMemory<byte>(buffer, 0, bytesRead), token).ConfigureAwait(false);
                        timer.Start();
                    }
                    if (bytesRead == 0)
                    {
                        throw new InvalidOperationException("No more data to read from the source stream.");
                    }
                }
                finally
                {
                    timer.Dispose();
                    ArrayPool<byte>.Shared.Return(buffer);
                }
            }
        }


        public override long Seek(long offset, SeekOrigin origin)
        {
            return _innerStream.Seek(offset, origin);
        }

        public override void SetLength(long value)
        {
            _innerStream.SetLength(value);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            _innerStream.Write(buffer, offset, count);
        }

        private int GetCopyBufferSize()
        {
            const int DefaultCopyBufferSize = 81920;
            int bufferSize = DefaultCopyBufferSize;
            if (CanSeek)
            {
                long length = Length;
                long position = Position;
                if (length <= position) 
                {
                    bufferSize = 1;
                }
                else
                {
                    long remaining = length - position;
                    if (remaining > 0)
                    {
                        bufferSize = (int)Math.Min(bufferSize, remaining);
                    }
                }
            }
            return bufferSize;
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            return _innerStream.ReadAsync(buffer, offset, count, cancellationToken);
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return _innerStream.Read(buffer, offset, count);
        }
    }
}
