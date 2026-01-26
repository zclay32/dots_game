using Unity.Entities;

/// <summary>
/// Tag component identifying the Crystal entity (singleton).
/// The Crystal is the central structure that must be protected.
/// </summary>
public struct Crystal : IComponentData { }

/// <summary>
/// Tracks the Crystal's accumulated soul power from harvested zombies.
/// Power is used for summoning units and heroes.
/// </summary>
public struct CrystalPower : IComponentData
{
    public float Current;       // Current accumulated power
    public float Lifetime;      // Total power ever collected (for stats)
}

/// <summary>
/// Configuration for the Crystal entity.
/// </summary>
public struct CrystalConfig : IComponentData
{
    public float SoulHarvestRange;      // Range within which zombie kills feed the crystal
    public float SoulValueBase;         // Base soul value per zombie kill
    public float ThreatRadius;          // How far the crystal's presence attracts zombies
    public int TileFootprint;           // Size in tiles (4 = 4x4)
}

/// <summary>
/// Tracks the current threat level based on crystal power.
/// Higher threat means more aggressive zombie behavior.
/// </summary>
public struct ThreatLevel : IComponentData
{
    public int Level;           // Current threat tier (0-5+)
    public float Multiplier;    // Zombie aggression multiplier
}
