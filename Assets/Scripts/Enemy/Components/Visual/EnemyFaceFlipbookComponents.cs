using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using UnityEngine;

/// <summary>
/// Identifies the face atlas state currently displayed by the shared enemy face shader.
/// </summary>
public enum EnemyFaceFlipbookState : byte
{
    Idle = 0,
    Attack = 1,
    Damage = 2
}

/// <summary>
/// Stores immutable face flipbook settings baked from the enemy visual preset.
/// </summary>
public struct EnemyFaceFlipbookConfig : IComponentData
{
    #region Fields
    public byte Enabled;
    public byte IdleEnabled;
    public byte AttackEnabled;
    public byte DamageEnabled;
    public float4 IdleGrid;
    public float4 AttackGrid;
    public float4 DamageGrid;
    public float IdleFramesPerSecond;
    public float AttackFramesPerSecond;
    public float DamageFramesPerSecond;
    public float IdleStartFrame;
    public float AttackStartFrame;
    public float DamageStartFrame;
    public float AttackDurationSeconds;
    public float DamageDurationSeconds;
    public UnityObjectRef<Texture2D> IdleAtlas;
    public UnityObjectRef<Texture2D> AttackAtlas;
    public UnityObjectRef<Texture2D> DamageAtlas;
    #endregion
}

/// <summary>
/// Stores mutable face playback state so damage and attack expressions can run with independent durations.
/// </summary>
public struct EnemyFaceFlipbookStateData : IComponentData
{
    #region Fields
    public EnemyFaceFlipbookState CurrentState;
    public float AttackRemainingSeconds;
    public float DamageRemainingSeconds;
    public float IdlePlaybackPhaseSeconds;
    public float AttackPlaybackPhaseSeconds;
    public float DamagePlaybackPhaseSeconds;
    public float LastObservedDamageLifetimeSeconds;
    public byte HasObservedDamage;
    public byte WasEngagementActive;
    #endregion
}

/// <summary>
/// Custom Entities Graphics override for enabling or disabling face flipbook playback per renderer.
/// </summary>
[MaterialProperty("_FaceFlipbookEnabled")]
public struct MaterialFaceFlipbookEnabled : IComponentData
{
    #region Fields
    public float Value;
    #endregion
}

/// <summary>
/// Custom Entities Graphics override selecting the displayed face state.
/// </summary>
[MaterialProperty("_FaceFlipbookState")]
public struct MaterialFaceFlipbookState : IComponentData
{
    #region Fields
    public float Value;
    #endregion
}

/// <summary>
/// Custom Entities Graphics override for face playback speed, phase and start frame.
/// </summary>
[MaterialProperty("_FaceFlipbookPlayback")]
public struct MaterialFaceFlipbookPlayback : IComponentData
{
    #region Fields
    public float4 Value;
    #endregion
}

/// <summary>
/// Custom Entities Graphics override for the idle atlas grid.
/// </summary>
[MaterialProperty("_FaceIdleGrid")]
public struct MaterialFaceIdleGrid : IComponentData
{
    #region Fields
    public float4 Value;
    #endregion
}

/// <summary>
/// Custom Entities Graphics override for the attack atlas grid.
/// </summary>
[MaterialProperty("_FaceAttackGrid")]
public struct MaterialFaceAttackGrid : IComponentData
{
    #region Fields
    public float4 Value;
    #endregion
}

/// <summary>
/// Custom Entities Graphics override for the damage atlas grid.
/// </summary>
[MaterialProperty("_FaceDamageGrid")]
public struct MaterialFaceDamageGrid : IComponentData
{
    #region Fields
    public float4 Value;
    #endregion
}
