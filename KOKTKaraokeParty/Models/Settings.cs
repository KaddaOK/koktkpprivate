using System;
using System.Collections.Generic;
using System.IO;
using Godot;
using KOKTKaraokeParty.Controls.SessionPrepWizard;
using Newtonsoft.Json;

[Serializable]
public partial class Settings
{
    public int DisplayScreenMonitor { get; set; } = 0;
    public int CountdownLengthSeconds { get; set; } = 10;
    public bool BgMusicEnabled { get; set; }
    public double BgMusicVolumePercent { get; set; } = 40;
    public List<string> BgMusicFiles { get; set; } = new List<string>();
    
    /// <summary>
    /// The six-digit room code for connecting to Karafun's remote control.
    /// This is displayed in the Karafun web player and allows remote control of playback.
    /// </summary>
    public string KarafunRoomCode { get; set; }
    
    /// <summary>
    /// Whether to use locally scanned files in this session
    /// </summary>
    public bool UseLocalFiles { get; set; } = true;
    
    /// <summary>
    /// Whether to use YouTube (KaraokeNerds) in this session
    /// </summary>
    public bool UseYouTube { get; set; } = true;
    
    /// <summary>
    /// Whether to use Karafun subscription in this session
    /// </summary>
    public bool UseKarafun { get; set; } = true;
    
    /// <summary>
    /// The mode for Karafun usage: ControlledBrowser or InstalledApp
    /// </summary>
    public KarafunMode KarafunMode { get; set; } = KarafunMode.ControlledBrowser;

    private static string settingsFileName = Path.Combine(Utils.GetAppStoragePath(), "settings.json");

    public static Settings LoadFromDiskIfExists(IFileWrapper fileWrapper)
    {
        var settings = new Settings();
        try
        {
            // Check if the settings file exists
            if (fileWrapper.Exists(settingsFileName))
            {
                GD.Print("Loading settings from disk...");
                // Read the JSON content from the file
                var settingsJson = fileWrapper.ReadAllText(settingsFileName);
                //GD.Print($"Settings JSON: {settingsJson}");
                // Deserialize the JSON into the settings value to return
                settings = JsonConvert.DeserializeObject<Settings>(settingsJson);
            }
        }
        catch (Exception ex)
        {
            GD.PrintErr($"Failed to load settings from disk: {ex.Message}");
        }
        return settings;
    }

    public void SaveToDisk(IFileWrapper fileWrapper)
    {
        try
        {
            var settingsJson = JsonConvert.SerializeObject(this, Formatting.Indented);
            //GD.Print($"Settings JSON: {settingsJson}");
            // Write the JSON to the file
            fileWrapper.WriteAllText(settingsFileName, settingsJson);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to save settings to disk: {ex.Message}");
        }
    }
}