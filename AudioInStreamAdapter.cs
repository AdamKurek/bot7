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
        private CancellationTokenSource _outerCancelationTokenSource;
        private readonly int _timerTimeout;

        public AudioInStreamAdapter(AudioInStream stream, CancellationTokenSource sneakyCancelationToken, int autoCancel = 300)
        {
            _audioStream = stream ?? throw new ArgumentNullException(nameof(stream));
            _outerCancelationTokenSource = sneakyCancelationToken;
            _timerTimeout = autoCancel;
        }


        //TODO this method should never be created, but you somehow must return and vosk wrapper blocks you from that
        public CancellationToken CreateNewToken()
        {
            _outerCancelationTokenSource = new CancellationTokenSource();
            return _outerCancelationTokenSource.Token;
        }

        public override bool CanRead => true;
        public override bool CanWrite => false;
        public override bool CanSeek => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public override int Read(byte[] buffer, int offset, int count)
        {

            var cts = new CancellationTokenSource();
            var token = cts.Token;



            int totalRead = 0;
            try
            {
                while (totalRead < count)
                {
                    if (_bufferOffset >= _buffer.Length)
                    {
                        var frame = _audioStream.ReadFrameAsync(token).GetAwaiter().GetResult();
                        _buffer = frame.Payload;
                        _bufferOffset = 0;
                        cts.CancelAfter(_timerTimeout);
                    }
                    int bytesAvailable = _buffer.Length - _bufferOffset;
                    int bytesToCopy = Math.Min(count - totalRead, bytesAvailable);
                    Array.Copy(_buffer, _bufferOffset, buffer, offset + totalRead, bytesToCopy);
                    _bufferOffset += bytesToCopy;
                    totalRead += bytesToCopy;
                }
            }
            catch (OperationCanceledException)
            {
                _outerCancelationTokenSource.Cancel();
                return 0;
            }
            finally
            {
                cts.Dispose();
            }

            return totalRead;
        }

        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
