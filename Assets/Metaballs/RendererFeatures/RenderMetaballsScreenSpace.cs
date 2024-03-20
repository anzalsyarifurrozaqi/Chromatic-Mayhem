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

        public Shader WriteDepthShader;
        public Shader BlurShader;
        public Shader BlitShader;

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

    const string CameraColorTargetId = "_CameraColorTexture";
    const string CameraDepthTargetId = "_CameraDepthTexture";

    int _cameraColorTargetId;
    int _cameraDepthTargetId;

    RTHandle cameraDepthTargetRT;
    RTHandle cameraColorTargetRT;

    RenderMetaballsDepthPass _renderMetaballsDepthPass;
    RenderMetaballsScreenSpacePass _renderMetaballsScreenSpacePass;
    
    private Material _writeDepthMaterial;    
    private Material _blitCopyDepthMaterial;    
    private Material _blurMaterial;
    private Material _blitMaterial;

    #region RENDER METABALLS DEPTH PASS
    class RenderMetaballsDepthPass : ScriptableRenderPass
    {
        const string MetaballDepthRTId = "_MetaballDepthRT";

        int _metaballDepthRTId;

        RTHandle _metaballDepthRT;

        public Material WriteDepthMaterial;

        RenderMetaballsSettings _settings;


        readonly ProfilingSampler _profilingSampler;

        readonly List<ShaderTagId> _shaderTagIds = new List<ShaderTagId>();
        RenderQueueType _renderQueueType;
        FilteringSettings _filteringSettings;
        RenderStateBlock _renderStateBlock;

        public RenderMetaballsDepthPass(RenderMetaballsSettings settings)
        {
            _settings = settings;

            _profilingSampler = new ProfilingSampler(_profilerTagRenderMetaballsDepthPass);

            this.renderPassEvent = _settings.Event;            
            this._renderQueueType = _settings.FilterSettings.RenderQueueType;
            RenderQueueRange renderQueueRange = (_renderQueueType == RenderQueueType.Transparent) ?
                RenderQueueRange.transparent : RenderQueueRange.opaque;
            _filteringSettings = new FilteringSettings(renderQueueRange, _settings.FilterSettings.LayerMask);

            _shaderTagIds.Add(new ShaderTagId("SRPDefaultUnlit"));
            _shaderTagIds.Add(new ShaderTagId("UniversalForward"));
            _shaderTagIds.Add(new ShaderTagId("UniversalForwardOnly"));
            _shaderTagIds.Add(new ShaderTagId("LightweightForward"));

            _renderStateBlock = new RenderStateBlock(RenderStateMask.Nothing);            
        }

        public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
        {
            _metaballDepthRTId = Shader.PropertyToID(MetaballDepthRTId);            

            _metaballDepthRT = RTHandles.Alloc(_metaballDepthRTId, name: MetaballDepthRTId);            

            ConfigureTarget(_metaballDepthRT);
            ConfigureClear(ClearFlag.Color, Color.clear);
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            SortingCriteria sortingCriteria =(_renderQueueType == RenderQueueType.Transparent) ?
                SortingCriteria.CommonTransparent : renderingData.cameraData.defaultOpaqueSortFlags;
            DrawingSettings drawingSettings = CreateDrawingSettings(_shaderTagIds, ref renderingData, sortingCriteria);

            CommandBuffer cmd = CommandBufferPool.Get(_profilingSampler.name);

            using (new ProfilingScope(cmd, _profilingSampler))
            {
                drawingSettings.overrideMaterial = WriteDepthMaterial;
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
        const string MetaballRTId = "_MetaballRT";
        const string MetaballRT2Id = "_MetaballRT2";
        const string MetaballDepthRTId = "_MetaballDepthRT";

        int _metaballRTId;
        int _metaballRT2Id;
        int _metaballDepthRTId;        

        RTHandle _metaballRT;
        RTHandle _metaballRT2;
        RTHandle _metaballDepthRT;
        RTHandle _cameraDepthTargetRT;
        RTHandle _cameraColorTargetRT;

        RenderMetaballsSettings _settings;

        readonly ProfilingSampler _profilingSampler;

        readonly List<ShaderTagId> _shaderTagIds = new List<ShaderTagId>();

        RenderQueueType _renderQueueType;
        FilteringSettings _filteringSettings;
        RenderStateBlock _renderStateBlock;        

        public Material BlitCopyDepthMaterial;
        public Material BlurMaterial;
        public Material BlitMaterial;

        public RenderMetaballsScreenSpacePass(RenderMetaballsSettings settings)
        {
            this._settings = settings;

            _profilingSampler = new ProfilingSampler(_profilerTagRenderMetaballsScreenSpacePass);
            this.renderPassEvent = _settings.Event;
            this._renderQueueType = _settings.FilterSettings.RenderQueueType;
            RenderQueueRange renderQueueRange = (_renderQueueType == RenderQueueType.Transparent) ?
                RenderQueueRange.transparent : RenderQueueRange.opaque;

            _filteringSettings = new FilteringSettings(renderQueueRange, _settings.FilterSettings.LayerMask);

            _shaderTagIds.Add(new ShaderTagId("SRPDefaultUnlit"));
            _shaderTagIds.Add(new ShaderTagId("UniversalForward"));
            _shaderTagIds.Add(new ShaderTagId("UniversalForwardOnly"));
            _shaderTagIds.Add(new ShaderTagId("LightweightForward"));

            _renderStateBlock = new RenderStateBlock(RenderStateMask.Nothing);
        }

        public void SetDrawRenderersRTHandle(RTHandle cameraDepthTargetRT, RTHandle cameraColorTargetRT)
        {
            this._cameraDepthTargetRT = cameraDepthTargetRT;
            this._cameraColorTargetRT = cameraColorTargetRT;
        }

        public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
        {
            var width = cameraTextureDescriptor.width / _settings.BlurPasses;
            var height = cameraTextureDescriptor.height / _settings.BlurPasses;

            RenderTextureDescriptor renderTextureDescriptor = new RenderTextureDescriptor(width, height, RenderTextureFormat.ARGB32);
            RenderingUtils.ReAllocateIfNeeded(ref _metaballRT, renderTextureDescriptor, FilterMode.Bilinear, name: MetaballRTId);
            RenderingUtils.ReAllocateIfNeeded(ref _metaballRT2, renderTextureDescriptor, FilterMode.Bilinear, name: MetaballRT2Id);
            RenderingUtils.ReAllocateIfNeeded(ref _cameraDepthTargetRT, renderTextureDescriptor, FilterMode.Bilinear, name: "_CameraDepthTexture");
            RenderingUtils.ReAllocateIfNeeded(ref _cameraColorTargetRT, renderTextureDescriptor, FilterMode.Bilinear, name: "_CameraColorTexture");

            ConfigureTarget(_metaballRT);            
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            SortingCriteria sortingCriteria = (_renderQueueType == RenderQueueType.Transparent) ?
                SortingCriteria.CommonTransparent : renderingData.cameraData.defaultOpaqueSortFlags;

            DrawingSettings drawingSettings = CreateDrawingSettings(_shaderTagIds, ref renderingData, sortingCriteria);

            CommandBuffer cmd = CommandBufferPool.Get(_profilingSampler.name);

            using (new ProfilingScope(cmd, _profilingSampler))
            {
                // Clear small RT
                cmd.ClearRenderTarget(true, true, Color.clear);
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();

                // Blit Camera Depth Texture                
                cmd.Blit(_cameraDepthTargetRT, _metaballRT, BlitCopyDepthMaterial);
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();

                // Draw to RT
                context.DrawRenderers(renderingData.cullResults, ref drawingSettings, ref _filteringSettings, ref _renderStateBlock);
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();

                // Blur
                cmd.SetGlobalTexture("_BlurDepthTex", _metaballDepthRT);
                cmd.SetGlobalFloat("_BlurDistance", _settings.BlurDistance);
                float offset = 1.5f;
                cmd.SetGlobalFloat("_Offset", offset);
                cmd.Blit(_metaballRT, _metaballRT2, BlurMaterial);
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();

                var tmpRT = _metaballRT;
                _metaballRT = _metaballRT2;
                _metaballRT2 = tmpRT;

                for (int i = 1; i < _settings.BlurPasses; ++i)
                {
                    offset += 1.0f;
                    cmd.SetGlobalFloat("_Offset", offset);
                    cmd.Blit(_metaballRT, _metaballRT2, BlurMaterial);
                    context.ExecuteCommandBuffer(cmd);
                    cmd.Clear();

                    tmpRT = _metaballRT;
                    _metaballRT = _metaballRT2;
                    _metaballRT2 = tmpRT;
                }

                //cmd.Blit(_metaballRT, _cameraColorTargetRT, BlitMaterial);
            }

            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();
            CommandBufferPool.Release(cmd);            
        }
    }
    #endregion

    public override void Create()
    {
        if (!Settings.IsEnabled || !Settings.WriteDepthShader) return;

        _writeDepthMaterial = CoreUtils.CreateEngineMaterial(Settings.WriteDepthShader);
        _blitCopyDepthMaterial = CoreUtils.CreateEngineMaterial(Shader.Find("Hidden/BlitToDepth"));
        _blurMaterial = CoreUtils.CreateEngineMaterial(Settings.BlurShader);
        _blitMaterial = CoreUtils.CreateEngineMaterial(Settings.BlitShader);

        _renderMetaballsDepthPass = new RenderMetaballsDepthPass(Settings)
        {
            WriteDepthMaterial = _writeDepthMaterial,
        };

        _renderMetaballsScreenSpacePass = new RenderMetaballsScreenSpacePass(Settings)
        {
            BlitCopyDepthMaterial = _blitCopyDepthMaterial,
            BlurMaterial = _blurMaterial,
            BlitMaterial = _blitMaterial,
        };
    }

    public override void SetupRenderPasses(ScriptableRenderer renderer, in RenderingData renderingData)
    {
        _cameraDepthTargetId = Shader.PropertyToID(CameraDepthTargetId);
        cameraDepthTargetRT = RTHandles.Alloc(_cameraDepthTargetId);

        _cameraColorTargetId = Shader.PropertyToID(CameraColorTargetId);
        cameraColorTargetRT = RTHandles.Alloc(CameraColorTargetId);

        _renderMetaballsScreenSpacePass.SetDrawRenderersRTHandle(cameraDepthTargetRT, cameraColorTargetRT);
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

        CoreUtils.Destroy(_writeDepthMaterial);
        CoreUtils.Destroy(_blitCopyDepthMaterial);
        CoreUtils.Destroy(_blurMaterial);
        CoreUtils.Destroy(_blitMaterial);
    }
}