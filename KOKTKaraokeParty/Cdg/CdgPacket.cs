using System;

/// <summary>
/// Adapted from https://github.com/spektor56/KaraokePlayer/tree/master/CdgLib.
/// TODO: acknowledge license!
/// </summary>
public class CdgPacket
{
    public CdgPacket(byte[] data)
    {
        Command = (CdgCommand)(data[0] & 0x3F);
        Instruction = (CdgInstruction)(data[1] & 0x3F);
        Array.Copy(data, 2, ParityQ, 0, 2);
        Array.Copy(data, 4, Data, 0, 16);
        Array.Copy(data, 20, ParityP, 0, 4);
    }

    public CdgCommand Command { get; }
    public CdgInstruction Instruction { get; }
    public byte[] ParityQ { get; } = new byte[2];
    public byte[] Data { get; } = new byte[16];
    public byte[] ParityP { get; } = new byte[4];
}