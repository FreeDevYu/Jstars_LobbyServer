namespace FieldStressHarness.FieldNet;

public sealed class MessageBuilder
{
    private readonly int _headerSize;
    private int _messageBufferLength;
    private byte[] _messageBuffer = Array.Empty<byte>();
    private int _currentIndex;

    public MessageBuilder()
    {
        _headerSize = NetworkDefine.NetworkHeaderSize;
    }

    public void Initialize(int bufferSize)
    {
        _messageBufferLength = bufferSize;
        _messageBuffer = new byte[_messageBufferLength];
        _currentIndex = 0;
    }

    public void InsertMessage(byte[] buffer, int offset, int count)
    {
        Array.Copy(buffer, offset, _messageBuffer, _currentIndex, count);
        _currentIndex += count;
    }

    public bool PopCompleteMessage(out byte[]? completeMessage)
    {
        if (_currentIndex < _headerSize)
        {
            completeMessage = null;
            return false;
        }

        MessageHeader header = new(_messageBuffer.AsSpan(0, _headerSize));
        int totalMessageSize = _headerSize + (int)header.BodySize;
        if (_currentIndex < totalMessageSize)
        {
            completeMessage = null;
            return false;
        }

        completeMessage = new byte[totalMessageSize];
        Array.Copy(_messageBuffer, 0, completeMessage, 0, totalMessageSize);
        Array.Copy(_messageBuffer, totalMessageSize, _messageBuffer, 0, _currentIndex - totalMessageSize);
        _currentIndex -= totalMessageSize;
        return true;
    }
}
