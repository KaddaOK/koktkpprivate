using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Godot;

/// <summary>
/// Adapted from https://github.com/spektor56/KaraokePlayer/tree/master/CdgLib.
/// TODO: acknowledge license!
/// </summary>
public class CdgGraphicsFile
{

    private CdgGraphic _graphic;

    private CdgGraphicsFile()
    {
    }

    public long Duration => _graphic.Duration;

    public bool Transparent => true;

    public static async Task<CdgGraphicsFile> LoadAsync(string fileName)
    {
        var graphicsFile = new CdgGraphicsFile { _graphic = new CdgGraphic(await LoadFileAsync(fileName)) };
        return graphicsFile;
    }

    public static async Task<CdgGraphicsFile> LoadAsync(Stream stream)
    {
        var graphicsFile = new CdgGraphicsFile { _graphic = new CdgGraphic(await LoadStreamAsync(stream)) };
        return graphicsFile;
    }

    private static async Task<IEnumerable<CdgPacket>> LoadFileAsync(string fileName)
    {
        using (var fileStream = new CdgGraphicStream(fileName))
        {
            return await fileStream.ReadFile();
        }
    }

    private static async Task<IEnumerable<CdgPacket>> LoadStreamAsync(Stream stream)
    {
        using (var fileStream = new CdgGraphicStream(stream))
        {
            return await fileStream.ReadFile();
        }
    }

    public ImageTexture RenderAtTime(long time)
    {
        return _graphic.ToImageTexture(time);
    }
}