using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

public class CdgGraphicStream : IDisposable
{
    private const int PacketSize = 24;
    private Stream _stream;

    public CdgGraphicStream(string path)
    {
        _stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
    }

    public CdgGraphicStream(Stream stream)
    {
        _stream = stream;
    }

    public async Task<IEnumerable<CdgPacket>> ReadFile()
    {
        _stream.Position = 0;
        var subCodePackets = new List<CdgPacket>();
        var buffer = new byte[_stream.Length];
        var bytesRead = await _stream.ReadAsync(buffer, 0, buffer.Length);

        for (var i = 0; i < bytesRead / PacketSize; i++)
        {
            var cdgPacket = new byte[PacketSize];
            Array.Copy(buffer, i * PacketSize, cdgPacket, 0, PacketSize);
            var subCodePacket = new CdgPacket(cdgPacket);
            subCodePackets.Add(subCodePacket);
        }
        return subCodePackets;
    }

    private async Task<IEnumerable<CdgPacket>> ReadSubCodeAsync(long numberOfPackets)
    {
        var subCodePackets = new List<CdgPacket>();
        var buffer = new byte[PacketSize * numberOfPackets];
        var bytesRead = await _stream.ReadAsync(buffer, 0, buffer.Length);

        for (var i = 0; i < bytesRead / PacketSize; i++)
        {
            var subCodePacket = new CdgPacket(buffer.Skip(i * PacketSize).Take(PacketSize).ToArray());
            subCodePackets.Add(subCodePacket);
        }
        return subCodePackets;
    }

    public void Dispose()
    {
        _stream?.Dispose();
        GC.SuppressFinalize(this);
    }
}