using System;
using UnityEngine;

/// <summary>
/// Stores deterministic seed selection and bounded solver limits shared by every generated level.
/// </summary>
[Serializable]
public sealed class GameProceduralLevelGenerationSettings
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Determines whether each run receives a random seed, uses the authored fixed seed or waits for an external run seed.")]
    [SerializeField]
    private GameProceduralLevelSeedMode seedMode = GameProceduralLevelSeedMode.RandomPerRun;

    [Tooltip("Deterministic seed used only when Seed Mode is Fixed.")]
    [SerializeField]
    private uint fixedSeed = 1u;

    [Tooltip("Hard technical node limit that prevents an invalid preset from creating an unbounded graph.")]
    [SerializeField]
    private int maximumNodeCount = 128;

    [Tooltip("Hard technical depth limit that bounds graph storage and solver exploration.")]
    [SerializeField]
    private int maximumDepth = 64;

    [Tooltip("Maximum deterministic backtracking attempts before generation reports an explicit failure.")]
    [SerializeField]
    private int maximumGenerationAttempts = 128;
    #endregion

    #endregion

    #region Properties
    public GameProceduralLevelSeedMode SeedMode
    {
        get
        {
            return seedMode;
        }
    }

    public uint FixedSeed
    {
        get
        {
            return fixedSeed;
        }
    }

    public int MaximumNodeCount
    {
        get
        {
            return maximumNodeCount;
        }
    }

    public int MaximumDepth
    {
        get
        {
            return maximumDepth;
        }
    }

    public int MaximumGenerationAttempts
    {
        get
        {
            return maximumGenerationAttempts;
        }
    }
    #endregion
}
