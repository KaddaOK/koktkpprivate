public enum ItemType
{
    /// <summary>
    /// Karafun via browser automation (legacy mode, uses PerformanceLink URL)
    /// </summary>
    KarafunWeb,
    
    /// <summary>
    /// Karafun via remote control API (uses song ID in Identifier field)
    /// </summary>
    KarafunRemote,
    
    Youtube,
    LocalMp3G,
    LocalMp3GZip,
    LocalMp4
}