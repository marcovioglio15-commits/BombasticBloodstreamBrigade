using System;
using UnityEngine;

/// <summary>
/// Stores fluid animation settings used by one player syringe.
/// </summary>
[Serializable]
public sealed class PlayerSyringeFluidSettings
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Enables continuous procedural movement inside the liquid.")]
    [SerializeField] private bool flowEnabled = true;

    [Tooltip("Base horizontal flow speed used by the procedural liquid layers.")]
    [Range(-4f, 4f)]
    [SerializeField] private float flowSpeed = 0.45f;

    [Tooltip("Maximum normalized displacement applied to the liquid surface.")]
    [Range(0f, 0.25f)]
    [SerializeField] private float waveAmplitude = 0.035f;

    [Tooltip("Number of liquid surface waves distributed along the syringe chamber.")]
    [Range(0f, 20f)]
    [SerializeField] private float waveFrequency = 3f;

    [Tooltip("Controls how slowly the liquid settles after movement impulses.")]
    [Range(0f, 1f)]
    [SerializeField] private float viscosity = 0.72f;

    [Tooltip("Enables deterministic procedural air bubbles without particles or GameObjects.")]
    [SerializeField] private bool bubblesEnabled = true;

    [Tooltip("Approximate normalized density of visible procedural bubbles.")]
    [Range(0f, 1f)]
    [SerializeField] private float bubbleDensity = 0.35f;

    [Tooltip("Minimum normalized bubble radius relative to the chamber height.")]
    [Range(0f, 0.25f)]
    [SerializeField] private float bubbleMinimumSize = 0.025f;

    [Tooltip("Maximum normalized bubble radius relative to the chamber height.")]
    [Range(0f, 0.25f)]
    [SerializeField] private float bubbleMaximumSize = 0.07f;

    [Tooltip("Vertical procedural bubble travel speed.")]
    [Range(-2f, 2f)]
    [SerializeField] private float bubbleRiseSpeed = 0.18f;

    [Tooltip("Horizontal procedural drift applied while bubbles rise.")]
    [Range(-2f, 2f)]
    [SerializeField] private float bubbleDrift = 0.12f;
    #endregion

    #endregion

    #region Properties
    public bool FlowEnabled => flowEnabled;
    public float FlowSpeed => flowSpeed;
    public float WaveAmplitude => waveAmplitude;
    public float WaveFrequency => waveFrequency;
    public float Viscosity => viscosity;
    public bool BubblesEnabled => bubblesEnabled;
    public float BubbleDensity => bubbleDensity;
    public float BubbleMinimumSize => bubbleMinimumSize;
    public float BubbleMaximumSize => bubbleMaximumSize;
    public float BubbleRiseSpeed => bubbleRiseSpeed;
    public float BubbleDrift => bubbleDrift;
    #endregion
}
