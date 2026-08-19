using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Maps one used player stat or resource to its shared text or sprite presentation.
/// </summary>
[Serializable]
public sealed class GameRoomRewardPresentationDefinition
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Player data domain represented by this presentation mapping.")]
    [SerializeField]
    private GameRoomRewardTargetDomain targetDomain;

    [Tooltip("Scalable stat selected from the linked Player Progression preset when this mapping targets a stat.")]
    [SerializeField]
    private string targetStatName;

    [Tooltip("Player resource represented when this mapping targets a resource.")]
    [SerializeField]
    private GameRoomRewardResource resource;

    [Tooltip("Chooses colored text or a sprite as the primary representation in both reward views.")]
    [SerializeField]
    private GameRoomRewardPresentationMode mode;

    [Tooltip("Text color used when the mapping presentation mode is Colored Text.")]
    [SerializeField]
    private Color textColor = Color.white;

    [Tooltip("Sprite displayed instead of colored text when the mapping presentation mode is Sprite.")]
    [SerializeField]
    private Sprite sprite;

    [Tooltip("Short localized-ready label prepended to formatted reward values.")]
    [SerializeField]
    private string displayLabel;

    [Tooltip("Optional short caption displayed next to the sprite representation.")]
    [SerializeField]
    private string spriteCaption;

    [Tooltip("Authored order used by the presentation mapping tab.")]
    [SerializeField]
    private int sortOrder;
    #endregion

    #endregion

    #region Properties
    public GameRoomRewardTargetDomain TargetDomain
    {
        get
        {
            return targetDomain;
        }
    }

    public string TargetStatName
    {
        get
        {
            return targetStatName;
        }
    }

    public GameRoomRewardResource Resource
    {
        get
        {
            return resource;
        }
    }

    public GameRoomRewardPresentationMode Mode
    {
        get
        {
            return mode;
        }
    }

    public Color TextColor
    {
        get
        {
            return textColor;
        }
    }

    public Sprite Sprite
    {
        get
        {
            return sprite;
        }
    }

    public string DisplayLabel
    {
        get
        {
            return displayLabel;
        }
    }

    public string SpriteCaption
    {
        get
        {
            return spriteCaption;
        }
    }

    public int SortOrder
    {
        get
        {
            return sortOrder;
        }
    }
    #endregion
}

/// <summary>
/// Configures the fixed-capacity scrolling reward log displayed above the player.
/// </summary>
[Serializable]
public sealed class GameRoomRewardPlayerLogSettings
{
    #region Fields

    #region Serialized Fields
    [Tooltip("World-space offset applied to the preauthored reward log above the player visual.")]
    [SerializeField]
    private Vector3 worldOffset = new Vector3(0f, 2.2f, 0f);

    [Tooltip("Chooses detailed values or sign-only summaries for player reward rows.")]
    [SerializeField]
    private GameRoomRewardValueDisplayMode valueDisplayMode;

    [Tooltip("Text size applied to every preauthored player reward log row.")]
    [SerializeField]
    private float fontSize = 3.6f;

    [Tooltip("Vertical spacing in local canvas units between consecutive reward rows.")]
    [SerializeField]
    private float rowSpacing = 0.5f;

    [Tooltip("Maximum number of preauthored player log rows that may be visible simultaneously.")]
    [SerializeField]
    private int visibleRows = 6;

    [Tooltip("Maximum queued reward entries retained while prior entries animate.")]
    [SerializeField]
    private int queueCapacity = 24;

    [Tooltip("Seconds used by a reward row to enter the visible log area.")]
    [SerializeField]
    private float enterDuration = 0.18f;

    [Tooltip("Seconds a reward row remains fully visible before leaving.")]
    [SerializeField]
    private float holdDuration = 1.4f;

    [Tooltip("Seconds used by a reward row to leave and fade out.")]
    [SerializeField]
    private float exitDuration = 0.3f;

    [Tooltip("Local vertical distance travelled by a row during its enter and exit animation.")]
    [SerializeField]
    private float scrollDistance = 0.35f;

    [Tooltip("Optional font override used by every preauthored player reward log row.")]
    [SerializeField]
    private TMP_FontAsset font;
    #endregion

    #endregion

    #region Properties
    public Vector3 WorldOffset => worldOffset;
    public GameRoomRewardValueDisplayMode ValueDisplayMode => valueDisplayMode;
    public float FontSize => fontSize;
    public float RowSpacing => rowSpacing;
    public int VisibleRows => visibleRows;
    public int QueueCapacity => queueCapacity;
    public float EnterDuration => enterDuration;
    public float HoldDuration => holdDuration;
    public float ExitDuration => exitDuration;
    public float ScrollDistance => scrollDistance;
    public TMP_FontAsset Font => font;
    #endregion
}

/// <summary>
/// Configures the fixed-capacity horizontal reward log displayed by destination portals.
/// </summary>
[Serializable]
public sealed class GameRoomRewardPortalLogSettings
{
    #region Fields

    #region Serialized Fields
    [Tooltip("World-space offset applied to the preauthored portal reward log.")]
    [SerializeField]
    private Vector3 worldOffset = new Vector3(0f, 1.75f, 0f);

    [Tooltip("Chooses the existing scrolling layout or a scene-positioned panel with one reward per row.")]
    [SerializeField]
    private GameRoomRewardPortalLogLayoutMode layoutMode;

    [Tooltip("Chooses detailed values or sign-only summaries for destination reward entries.")]
    [SerializeField]
    private GameRoomRewardValueDisplayMode valueDisplayMode;

    [Tooltip("Text size applied to every preauthored portal log cell.")]
    [SerializeField]
    private float fontSize = 3.2f;

    [Tooltip("Horizontal spacing in local canvas units between consecutive reward cells.")]
    [SerializeField]
    private float cellSpacing = 2f;

    [Tooltip("Maximum number of preauthored log cells visible at the same time.")]
    [SerializeField]
    private int visibleCells = 1;

    [Tooltip("Horizontal scrolling speed measured in local canvas units per second.")]
    [SerializeField]
    private float scrollSpeed = 0.75f;

    [Tooltip("Pause in seconds before a rebuilt portal log starts scrolling.")]
    [SerializeField]
    private float initialPause = 0.5f;

    [Tooltip("Pause in seconds applied after the log completes one full loop.")]
    [SerializeField]
    private float loopPause = 0.35f;

    [Tooltip("Optional font override used by every preauthored portal log cell.")]
    [SerializeField]
    private TMP_FontAsset font;

    [Tooltip("Vertical gap in local canvas units between rewards in Static Rows mode.")]
    [SerializeField]
    private float staticRowSpacing = 0.35f;

    [Tooltip("Horizontal and vertical local canvas padding added around the Static Rows content.")]
    [SerializeField]
    private Vector2 staticPanelPadding = new Vector2(0.8f, 0.6f);

    [Tooltip("Smallest width and height allowed for the adaptive Static Rows background panel.")]
    [SerializeField]
    private Vector2 staticMinimumPanelSize = new Vector2(5f, 2f);

    [Tooltip("Optional sprite drawn behind the adaptive Static Rows reward panel.")]
    [SerializeField]
    private Sprite staticBackgroundSprite;

    [Tooltip("Color applied to the adaptive Static Rows background panel and its optional sprite.")]
    [SerializeField]
    private Color staticBackgroundColor = new Color(0f, 0f, 0f, 0.75f);

    [Tooltip("Transform-only animations started after ECS marks the portal as a traversable exit.")]
    [SerializeField]
    private List<GameRoomPortalTransformAnimationDefinition> activationAnimations =
        new List<GameRoomPortalTransformAnimationDefinition>();

    [Tooltip("Prefab-asset replacements for linked existing 3D scene objects, applied when ECS marks the portal as a traversable exit.")]
    [SerializeField]
    private List<GameRoomPortalPrefabReplacementDefinition> activationPrefabReplacements =
        new List<GameRoomPortalPrefabReplacementDefinition>();
    #endregion

    #endregion

    #region Properties
    public Vector3 WorldOffset => worldOffset;
    public GameRoomRewardPortalLogLayoutMode LayoutMode => layoutMode;
    public GameRoomRewardValueDisplayMode ValueDisplayMode => valueDisplayMode;
    public float FontSize => fontSize;
    public float CellSpacing => cellSpacing;
    public int VisibleCells => visibleCells;
    public float ScrollSpeed => scrollSpeed;
    public float InitialPause => initialPause;
    public float LoopPause => loopPause;
    public TMP_FontAsset Font => font;
    public float StaticRowSpacing => staticRowSpacing;
    public Vector2 StaticPanelPadding => staticPanelPadding;
    public Vector2 StaticMinimumPanelSize => staticMinimumPanelSize;
    public Sprite StaticBackgroundSprite => staticBackgroundSprite;
    public Color StaticBackgroundColor => staticBackgroundColor;
    public IReadOnlyList<GameRoomPortalTransformAnimationDefinition> ActivationAnimations =>
        activationAnimations;
    public IReadOnlyList<GameRoomPortalPrefabReplacementDefinition> ActivationPrefabReplacements =>
        activationPrefabReplacements;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Ensures optional effect collections exist without changing authored values.
    /// </summary>
    public void EnsureInitialized()
    {
        if (activationAnimations == null)
            activationAnimations = new List<GameRoomPortalTransformAnimationDefinition>();

        if (activationPrefabReplacements == null)
            activationPrefabReplacements = new List<GameRoomPortalPrefabReplacementDefinition>();
    }
    #endregion

    #endregion
}
