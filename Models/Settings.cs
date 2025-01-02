using System;
using System.Collections.Generic;
using System.IO;
using Godot;
using Newtonsoft.Json;

[Serializable]
public partial class Settings
{
    public int DisplayScreenMonitor { get; set; } = 0;
    public int CountdownLengthSeconds { get; set; } = 10;
    public bool BgMusicEnabled { get; set; }
    public double BgMusicVolumePercent { get; set; } = 40;
    public List<string> BgMusicFiles { get; set; } = new List<string>();

    private static string settingsFileName = Path.Combine(Utils.GetAppStoragePath(), "settings.json");

    public static Settings LoadFromDiskIfExists()
    {
        var settings = new Settings();
        try
        {
            // Check if the settings file exists
            if (File.Exists(settingsFileName))
            {
                GD.Print("Loading settings from disk...");
                // Read the JSON content from the file
                var settingsJson = File.ReadAllText(settingsFileName);
                GD.Print($"Settings JSON: {settingsJson}");
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

    public void SaveToDisk()
    {
        try
        {
            var settingsJson = JsonConvert.SerializeObject(this, Formatting.Indented);
            GD.Print($"Settings JSON: {settingsJson}");
            // Write the JSON to the file
            File.WriteAllText(settingsFileName, settingsJson);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to save settings to disk: {ex.Message}");
        }
    }
}