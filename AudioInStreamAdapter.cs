using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord.Audio;

namespace bot7
{
    //it's just adding read function to AudioInStream
    internal class AudioInStreamAdapter : System.IO.Stream
    {
        private readonly AudioInStream _audioStream;
        private byte[] _buffer = Array.Empty<byte>();
        private int _bufferOffset = 0;

        public AudioInStreamAdapter(AudioInStream stream)
        {
            _audioStream = stream ?? throw new ArgumentNullException(nameof(stream));
        }

        public override bool CanRead => true;
        public override bool CanWrite => false;
        public override bool CanSeek => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public override int Read(byte[] buffer, int offset, int count)
        {
            int totalRead = 0;

            while (totalRead < count)
            {
                if (_bufferOffset >= _buffer.Length)
                {
                    if (!_audioStream.TryReadFrame(CancellationToken.None, out var frame))
                        break;

                    _buffer = frame.Payload;
                    _bufferOffset = 0;
                }

                int bytesAvailable = _buffer.Length - _bufferOffset;
                int bytesToCopy = Math.Min(count - totalRead, bytesAvailable);
                Array.Copy(_buffer, _bufferOffset, buffer, offset + totalRead, bytesToCopy);
                _bufferOffset += bytesToCopy;
                totalRead += bytesToCopy;
            }

            return totalRead;
        }

        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
