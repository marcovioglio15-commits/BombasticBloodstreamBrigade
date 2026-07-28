using UnityEngine;

/// <summary>
/// Carries one fully formatted room-reward entry from ECS presentation systems to a preauthored view.
/// </summary>
public readonly struct GameRoomRewardPresentationItem
{
    #region Properties
    public readonly string Text;
    public readonly string SpriteCaption;
    public readonly Color TextColor;
    public readonly Sprite Sprite;
    public readonly bool UseSprite;
    #endregion

    #region Methods

    #region Construction
    /// <summary>
    /// Creates one immutable text or sprite reward entry.
    /// </summary>
    /// <param name="text">Formatted summary used by colored-text mappings.</param>
    /// <param name="spriteCaption">Optional caption displayed beside a mapped sprite.</param>
    /// <param name="textColor">Mapped color applied to text and optional sprite captions.</param>
    /// <param name="sprite">Optional mapped sprite.</param>
    /// <param name="useSprite">True when the sprite is the primary representation.</param>
    public GameRoomRewardPresentationItem(string text,
                                          string spriteCaption,
                                          Color textColor,
                                          Sprite sprite,
                                          bool useSprite)
    {
        Text = text;
        SpriteCaption = spriteCaption;
        TextColor = textColor;
        Sprite = sprite;
        UseSprite = useSprite && sprite != null;
    }
    #endregion

    #endregion
}
