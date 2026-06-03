using UnityEngine;

/// <summary>
/// Destroys a death-animation VFX instance using unscaled delta time so it can finish while gameplay time is frozen.
/// </summary>
[DisallowMultipleComponent]
internal sealed class PlayerDeathAnimationUnscaledVfxLifetime : MonoBehaviour
{
    #region Fields
    private float remainingSeconds;
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Adds or reuses the lifetime driver on a VFX instance spawned by the death animation system.
    /// </summary>
    /// <param name="targetInstance">Runtime VFX instance that should be destroyed after an unscaled lifetime.</param>
    /// <param name="lifetimeSeconds">Unscaled lifetime in seconds.</param>
    public static void Attach(GameObject targetInstance, float lifetimeSeconds)
    {
        if (targetInstance == null)
            return;

        PlayerDeathAnimationUnscaledVfxLifetime lifetime = targetInstance.GetComponent<PlayerDeathAnimationUnscaledVfxLifetime>();

        if (lifetime == null)
            lifetime = targetInstance.AddComponent<PlayerDeathAnimationUnscaledVfxLifetime>();

        lifetime.Initialize(lifetimeSeconds);
    }
    #endregion

    #region Unity Methods
    /// <summary>
    /// Advances the unscaled lifetime and destroys the owning VFX GameObject once it expires.
    /// </summary>
    private void Update()
    {
        remainingSeconds -= Time.unscaledDeltaTime;

        if (remainingSeconds > 0f)
            return;

        Destroy(gameObject);
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Resets the remaining unscaled lifetime for a reused VFX instance.
    /// </summary>
    /// <param name="lifetimeSeconds">Requested lifetime in seconds.</param>
    private void Initialize(float lifetimeSeconds)
    {
        remainingSeconds = Mathf.Max(0f, lifetimeSeconds);
    }
    #endregion

    #endregion
}
