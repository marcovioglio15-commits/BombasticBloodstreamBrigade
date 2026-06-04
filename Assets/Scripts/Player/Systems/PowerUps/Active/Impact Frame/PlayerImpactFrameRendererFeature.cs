using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

/// <summary>
/// URP renderer feature that injects the active Player Impact Frame fullscreen filter into the camera color pipeline.
/// </summary>
public sealed class PlayerImpactFrameRendererFeature : ScriptableRendererFeature
{
    #region Constants
    private const string PassName = "Player Impact Frame";
    private const string TemporaryColorName = "_PlayerImpactFrameColor";
    #endregion

    #region Serialized Fields
    [Tooltip("Shader used by the Impact Frame fullscreen pass. Keep this assigned so the shader is included in player builds.")]
    [SerializeField] private Shader impactFrameShader;

    [Tooltip("URP event used to inject the Impact Frame filter after the scene and post-processing have rendered.")]
    [SerializeField] private RenderPassEvent renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;
    #endregion

    #region Fields
    private PlayerImpactFrameRenderPass renderPass;
    #endregion

    #region Methods

    #region Scriptable Renderer Feature
    /// <summary>
    /// Creates the reusable render pass instance used by this renderer feature.
    /// </summary>
    public override void Create()
    {
        renderPass = new PlayerImpactFrameRenderPass(PassName);
        renderPass.renderPassEvent = renderPassEvent;
    }

    /// <summary>
    /// Enqueues the Impact Frame render pass only when the ECS runtime has an active snapshot for the current camera.
    /// </summary>
    /// <param name="renderer">URP renderer currently building the camera pass list.</param>
    /// <param name="renderingData">Per-camera URP rendering data.</param>
    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (renderPass == null)
            Create();

        Material configuredMaterial;

        if (!PlayerImpactFramePresentationRuntime.TryConfigureMaterialForCamera(renderingData.cameraData.camera, impactFrameShader, out configuredMaterial))
            return;

        renderPass.renderPassEvent = renderPassEvent;
        renderPass.Setup(configuredMaterial);
        renderer.EnqueuePass(renderPass);
    }

    /// <summary>
    /// Releases compatibility-mode render targets owned by the render pass.
    /// </summary>
    /// <param name="disposing">True when Unity is disposing managed renderer feature resources.</param>
    protected override void Dispose(bool disposing)
    {
        if (renderPass == null)
            return;

        renderPass.Dispose();
        renderPass = null;
    }
    #endregion

    #endregion

    /// <summary>
    /// URP render pass that applies the configured Impact Frame material to the active camera color texture.
    /// </summary>
    private sealed class PlayerImpactFrameRenderPass : ScriptableRenderPass
    {
        #region Fields
        private Material material;
#if URP_COMPATIBILITY_MODE
        private RTHandle temporaryColorHandle;
#endif
        #endregion

        #region Constructors
        /// <summary>
        /// Creates the render pass and profiling scope used by URP.
        /// </summary>
        /// <param name="passName">Readable pass name displayed in URP profiling tools.</param>
        public PlayerImpactFrameRenderPass(string passName)
        {
            profilingSampler = new ProfilingSampler(passName);
        }
        #endregion

        #region Methods

        #region Setup
        /// <summary>
        /// Assigns the material configured from the latest ECS Impact Frame snapshot.
        /// </summary>
        /// <param name="configuredMaterial">Material instance configured immediately before the pass is enqueued.</param>
        public void Setup(Material configuredMaterial)
        {
            material = configuredMaterial;
            requiresIntermediateTexture = true;
        }

        /// <summary>
        /// Releases render targets allocated only when URP compatibility mode is enabled.
        /// </summary>
        public void Dispose()
        {
#if URP_COMPATIBILITY_MODE
            temporaryColorHandle?.Release();
            temporaryColorHandle = null;
#endif
        }
        #endregion

        #region RenderGraph
        /// <summary>
        /// Records the RenderGraph fullscreen blit and swaps the active camera color texture for downstream passes.
        /// </summary>
        /// <param name="renderGraph">URP RenderGraph builder for the current camera.</param>
        /// <param name="frameData">URP frame data containing active color and camera resources.</param>
        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            if (material == null)
                return;

            UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();

            if (resourceData.isActiveTargetBackBuffer)
                return;

            TextureHandle source = resourceData.activeColorTexture;

            if (!source.IsValid())
                return;

            TextureDesc destinationDescriptor = renderGraph.GetTextureDesc(source);
            destinationDescriptor.name = TemporaryColorName;
            destinationDescriptor.clearBuffer = false;
            TextureHandle destination = renderGraph.CreateTexture(destinationDescriptor);
            RenderGraphUtils.BlitMaterialParameters blitParameters = new RenderGraphUtils.BlitMaterialParameters(source, destination, material, 0);
            renderGraph.AddBlitPass(blitParameters, PassName);
            resourceData.cameraColor = destination;
        }
        #endregion

#if URP_COMPATIBILITY_MODE
        #region Compatibility Rendering
#pragma warning disable 618, 672
        /// <summary>
        /// Allocates the temporary color target used by the non-RenderGraph URP path.
        /// </summary>
        /// <param name="commandBuffer">Command buffer provided by URP during render pass setup.</param>
        /// <param name="cameraTextureDescriptor">Descriptor matching the current camera color target.</param>
        public override void Configure(CommandBuffer commandBuffer, RenderTextureDescriptor cameraTextureDescriptor)
        {
            RenderTextureDescriptor descriptor = cameraTextureDescriptor;
            descriptor.depthBufferBits = 0;
            descriptor.msaaSamples = 1;
            RenderingUtils.ReAllocateHandleIfNeeded(ref temporaryColorHandle,
                                                    descriptor,
                                                    FilterMode.Bilinear,
                                                    TextureWrapMode.Clamp,
                                                    name: TemporaryColorName);
        }

        /// <summary>
        /// Applies the fullscreen filter on the non-RenderGraph URP path.
        /// </summary>
        /// <param name="context">Scriptable render context used to execute the generated command buffer.</param>
        /// <param name="renderingData">Per-camera URP rendering data.</param>
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (material == null)
                return;

            RTHandle sourceHandle = renderingData.cameraData.renderer.cameraColorTargetHandle;
            CommandBuffer commandBuffer = CommandBufferPool.Get(PassName);

            using (new ProfilingScope(commandBuffer, profilingSampler))
            {
                Blitter.BlitCameraTexture(commandBuffer, sourceHandle, temporaryColorHandle);
                Blitter.BlitCameraTexture(commandBuffer, temporaryColorHandle, sourceHandle, material, 0);
            }

            context.ExecuteCommandBuffer(commandBuffer);
            commandBuffer.Clear();
            CommandBufferPool.Release(commandBuffer);
        }
#pragma warning restore 618, 672
        #endregion
#endif

        #endregion
    }
}
