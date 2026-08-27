using System;
using UnityEngine;

/// <summary>
/// Configures preauthored screen-edge indicators for traversable room-exit portals outside the camera view.
/// </summary>
[Serializable]
public sealed class GameRoomRewardPortalIndicatorSettings
{
    #region Fields

    #region Serialized Fields
    [Tooltip("Enables screen-edge indicators for traversable room-exit portals outside the camera view.")]
    [SerializeField]
    private bool enabled;

    [Tooltip("Sprite rotated toward each open portal while that portal remains outside the camera view.")]
    [SerializeField]
    private Sprite indicatorSprite;

    [Tooltip("Color applied to every open-portal screen-edge indicator.")]
    [SerializeField]
    private Color indicatorColor = Color.white;

    [Tooltip("Square width and height of each open-portal indicator in screen pixels.")]
    [SerializeField]
    private float indicatorSizePixels = 64f;

    [Tooltip("Additional distance retained between each indicator and the nearest screen edge in pixels.")]
    [SerializeField]
    private float edgePaddingPixels = 20f;

    [Tooltip("Canvas sorting order used by portal indicators. Keep this below zero so primary gameplay HUD elements remain in front.")]
    [SerializeField]
    private int sortingOrder = -100;

    [Tooltip("World-space offset added to the authoritative portal center before camera projection.")]
    [SerializeField]
    private Vector3 worldOffset = new Vector3(0f, 1.5f, 0f);
    #endregion

    #endregion

    #region Properties
    public bool Enabled => enabled;
    public Sprite IndicatorSprite => indicatorSprite;
    public Color IndicatorColor => indicatorColor;
    public float IndicatorSizePixels => indicatorSizePixels;
    public float EdgePaddingPixels => edgePaddingPixels;
    public int SortingOrder => sortingOrder;
    public Vector3 WorldOffset => worldOffset;
    #endregion
}
