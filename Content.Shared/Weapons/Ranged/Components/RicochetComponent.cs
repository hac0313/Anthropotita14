using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Timing;

namespace Content.Shared.Weapons.Ranged.Components;

/// <summary>
/// Gives this entity a chance to physically reflect (ricochet) projectiles.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class RicochetComponent : Component
{
    /// <summary>
    ///     Enables or disables ricochet for this entity.
    /// </summary>
    [DataField("canRicochet")]
    public bool CanRicochet = false;

    /// <summary>
    /// Chance (0-100) that the projectile will be reflected.
    /// </summary>
    [DataField("ricochetChance")]
    public int RicochetChance = 1;

    /// <summary>
    /// Angle spread of the reflection in degrees.
    /// </summary>
    [DataField("ricochetSpread")]
    public Angle RicochetSpread = Angle.FromDegrees(50);

    /// <summary>
    /// Sound played when a ricochet occurs.
    /// </summary>
    [DataField("ricochetSound")]
    public SoundSpecifier? RicochetSound;

    /// <summary>
    /// Maximum distance at which the ricochet sound can be heard.
    /// </summary>
    [DataField("ricochetSoundDistance")]
    public float RicochetSoundDistance = 10;

    /// <summary>
    /// Volume adjustment for the ricochet sound, in dB.
    /// </summary>
    [DataField("ricochetSoundVolume")]
    public float RicochetSoundVolume = -1;
}