using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering.Universal;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class RenderMetaballsScreenSpace : ScriptableRendererFeature
{
    #region SETTINGS
    [System.Serializable]
    public class RenderMetaballsSettings
    {
        public bool IsEnabled = true;
        public RenderPassEvent Event = RenderPassEvent.AfterRenderingOpaques;

        public RenderObjects.FilterSettings FilterSettings = new RenderObjects.FilterSettings();

        public Material KawaseBlur;
        public Material BlitMaterial;

        [Range(1, 15)]
        public int BlurPasses = 1;

        [Range(1, 4)]
        public int BlurDistance = 1;
    }
    #endregion

    public RenderMetaballsSettings Settings = new RenderMetaballsSettings();

    const string _profilerTagRenderMetaballsDepthPass = "RenderMetaballsDepthPass";
    const string _profilerTagRenderMetaballsScreenSpacePass = "RenderMetaballsScreenSpacePass";

    const int MESH_DRAW_PASS = 0;
    const int KAWASE_BLUR_PASS = 1;

    RenderMetaballsDepthPass _renderMetaballsDepthPass;
    RenderMetaballsScreenSpacePass _renderMetaballsScreenSpacePass;

    RTHandle drawMeshRTHandle;

    #region RENDER METABALLS DEPTH PASS
    class RenderMetaballsDepthPass : ScriptableRenderPass
    {
        RTHandle _drawRenderersRTHandle;

        RenderMetaballsSettings _settings;

        readonly ProfilingSampler _profilingSampler;

        readonly List<ShaderTagId> _shaderTagIds = new List<ShaderTagId>();        
        FilteringSettings _filteringSettings;
        RenderStateBlock _renderStateBlock;

        public RenderMetaballsDepthPass(RenderMetaballsSettings settings)
        {
            _settings = settings;

            _profilingSampler = new ProfilingSampler(_profilerTagRenderMetaballsDepthPass);

            this.renderPassEvent = _settings.Event;            

            _filteringSettings = new FilteringSettings(null, _settings.FilterSettings.LayerMask);

            _shaderTagIds.Add(new ShaderTagId("SRPDefaultUnlit"));
            _shaderTagIds.Add(new ShaderTagId("UniversalForward"));
            _shaderTagIds.Add(new ShaderTagId("UniversalForwardOnly"));
            _shaderTagIds.Add(new ShaderTagId("LightweightForward"));

            _renderStateBlock = new RenderStateBlock(RenderStateMask.Nothing);            
        }

        public void SetupDrawRenderersRTHandle(RTHandle drawRenderersRTHandle)
        {
            this._drawRenderersRTHandle = drawRenderersRTHandle;
        }

        public void ReleaseHandles()
        {
            _drawRenderersRTHandle?.Release();
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            ConfigureTarget(_drawRenderersRTHandle, renderingData.cameraData.renderer.cameraDepthTargetHandle);
            ConfigureClear(ClearFlag.Color, Color.clear);
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            SortingCriteria sortingCriteria = SortingCriteria.CommonOpaque;
            DrawingSettings drawingSettings = CreateDrawingSettings(_shaderTagIds, ref renderingData, sortingCriteria);            
            drawingSettings.overrideMaterial = _settings.KawaseBlur;
            drawingSettings.overrideMaterialPassIndex = MESH_DRAW_PASS;

            CommandBuffer cmd = CommandBufferPool.Get(_profilingSampler.name);

            using (new ProfilingScope(cmd, _profilingSampler))
            {
                context.DrawRenderers(renderingData.cullResults, ref drawingSettings, ref _filteringSettings, ref _renderStateBlock);
            }

            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();
            CommandBufferPool.Release(cmd);
        }
    }
    #endregion

    #region RENDER METABALLS SCREEN SPACE PASS
    class RenderMetaballsScreenSpacePass : ScriptableRenderPass
    {
        RenderMetaballsSettings _settings;

        readonly ProfilingSampler _profilingSampler;

        readonly List<ShaderTagId> _shaderTagIds = new List<ShaderTagId>();
        
        FilteringSettings _filteringSettings;
        RenderStateBlock _renderStateBlock;

        RTHandle _metaballRT;
        RTHandle _metaballRT2;
        RTHandle _cameraTargetRT;

        private RTHandle source { get; set; }

        public RenderMetaballsScreenSpacePass(RenderMetaballsSettings settings)
        {
            this._settings = settings;

            _profilingSampler = new ProfilingSampler(_profilerTagRenderMetaballsScreenSpacePass);
            this.renderPassEvent = _settings.Event;

            _filteringSettings = new FilteringSettings(null, _settings.FilterSettings.LayerMask);

            _shaderTagIds.Add(new ShaderTagId("SRPDefaultUnlit"));
            _shaderTagIds.Add(new ShaderTagId("UniversalForward"));
            _shaderTagIds.Add(new ShaderTagId("UniversalForwardOnly"));
            _shaderTagIds.Add(new ShaderTagId("LightweightForward"));

            _renderStateBlock = new RenderStateBlock(RenderStateMask.Nothing);
        }

        public void SetDrawRenderersRTHandle(RTHandle source)
        {
            this.source = source;
        }

        public void ReleaseHandles()
        {
            _metaballRT?.Release();
            _metaballRT2?.Release();
        }

        public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
        {
            var width = cameraTextureDescriptor.width / _settings.BlurDistance;
            var height = cameraTextureDescriptor.height / _settings.BlurDistance;

            RenderTextureDescriptor renderTextureDescriptor = new RenderTextureDescriptor(width, height, RenderTextureFormat.ARGB32);
            RenderingUtils.ReAllocateIfNeeded(ref _metaballRT, renderTextureDescriptor);
            RenderingUtils.ReAllocateIfNeeded(ref _metaballRT2, renderTextureDescriptor);

            ConfigureTarget(_metaballRT);            
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {            
            _cameraTargetRT = renderingData.cameraData.renderer.cameraColorTargetHandle;
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            SortingCriteria sortingCriteria = SortingCriteria.CommonOpaque;

            DrawingSettings drawingSettings = CreateDrawingSettings(_shaderTagIds, ref renderingData, sortingCriteria);

            CommandBuffer cmd = CommandBufferPool.Get(_profilingSampler.name);
            using (new ProfilingScope(cmd, _profilingSampler))
            {
                cmd.ClearRenderTarget(true, true, Color.clear);
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();

                context.DrawRenderers(renderingData.cullResults, ref drawingSettings, ref _filteringSettings, ref _renderStateBlock);
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();

                cmd.SetGlobalFloat("_Offset", 1.5f);
                cmd.Blit(_metaballRT, _metaballRT2, _settings.KawaseBlur, KAWASE_BLUR_PASS);
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();

                var rttmp = _metaballRT;
                _metaballRT = _metaballRT2;
                _metaballRT2 = rttmp;

                for (var i = 1; i < _settings.BlurPasses - 1; i++)
                {

                    cmd.SetGlobalFloat("_Offset", 0.5f + i);
                    cmd.Blit(_metaballRT, _metaballRT2, _settings.KawaseBlur, KAWASE_BLUR_PASS);
                    context.ExecuteCommandBuffer(cmd);
                    cmd.Clear();

                    rttmp = _metaballRT;
                    _metaballRT = _metaballRT2;
                    _metaballRT2 = rttmp;
                }

                //cmd.SetGlobalFloat("_Offset", 0.5f + _settings.BlurPasses - 1f);
                //cmd.Blit(_metaballRT, _cameraTargetRT, _settings.BlitMaterial);
            }

            context.ExecuteCommandBuffer(cmd);            
            CommandBufferPool.Release(cmd);
        }

        public override void OnCameraCleanup(CommandBuffer cmd)
        {            
        }
    }
    #endregion

    public override void Create()
    {
        if (!Settings.IsEnabled) return;

        _renderMetaballsDepthPass = new RenderMetaballsDepthPass(Settings);
        _renderMetaballsScreenSpacePass = new RenderMetaballsScreenSpacePass(Settings);
    }

    public override void SetupRenderPasses(ScriptableRenderer renderer, in RenderingData renderingData)
    {
        if (_renderMetaballsDepthPass == null) return;

        RenderTextureDescriptor cameraTargetDesc = renderingData.cameraData.cameraTargetDescriptor;
        cameraTargetDesc.depthBufferBits = 0;

        RenderingUtils.ReAllocateIfNeeded(ref drawMeshRTHandle, cameraTargetDesc, FilterMode.Bilinear);

        _renderMetaballsDepthPass.SetupDrawRenderersRTHandle(drawMeshRTHandle);

        RTHandle source = renderingData.cameraData.renderer.cameraColorTargetHandle;
        _renderMetaballsScreenSpacePass.SetDrawRenderersRTHandle(source);
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (_renderMetaballsDepthPass == null) return;

        renderer.EnqueuePass(_renderMetaballsDepthPass);
        renderer.EnqueuePass(_renderMetaballsScreenSpacePass);
    }

    protected override void Dispose(bool disposing)
    {
        if (_renderMetaballsDepthPass == null) return;

        _renderMetaballsDepthPass.ReleaseHandles();
        _renderMetaballsScreenSpacePass.ReleaseHandles();
    }
}