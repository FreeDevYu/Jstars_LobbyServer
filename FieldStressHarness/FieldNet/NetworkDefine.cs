using System.Runtime.InteropServices;

namespace FieldStressHarness.FieldNet;

public static class NetworkDefine
{
    public const int NetworkBufferSize = 2048;
    public const int NetworkOk = 1;
    public const int NetworkError = -1;
    public static readonly int NetworkHeaderSize = Marshal.SizeOf<MessageHeader>();
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct MessageHeader
{
    public uint BodySize;
    public uint ContentsType;

    public MessageHeader(uint bodySize, uint contentsType)
    {
        BodySize = bodySize;
        ContentsType = contentsType;
    }

    public MessageHeader(ReadOnlySpan<byte> span)
    {
        MessageHeader tempHeader = MemoryMarshal.Read<MessageHeader>(span);
        BodySize = tempHeader.BodySize;
        ContentsType = tempHeader.ContentsType;
    }

    public byte[] ToBytes()
    {
        byte[] buffer = new byte[NetworkDefine.NetworkHeaderSize];
        MemoryMarshal.Write(buffer, ref this);
        return buffer;
    }
}

public sealed class FieldMessage
{
    public required MessageHeader Header { get; init; }
    public required byte[] Body { get; init; }

    public protocol.Content ContentType => (protocol.Content)Header.ContentsType;
}
