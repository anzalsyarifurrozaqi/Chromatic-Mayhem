using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering.Universal;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using static Unity.Burst.Intrinsics.X86.Avx;

public class RenderMetaballsScreenSpace : ScriptableRendererFeature
{
    #region SETTINGS
    [System.Serializable]
    public class RenderMetaballsSettings
    {
        public bool IsEnabled = true;
        public RenderPassEvent Event = RenderPassEvent.AfterRenderingOpaques;

        public RenderObjects.FilterSettings FilterSettings = new RenderObjects.FilterSettings();
        
        public Shader KawaseShader;
        public Shader BlitShader;

        [Space(10)]
        [Range(0f, 1f)]
        public float ColorStep;
        [Range(0f, 1f)]
        public float ColorStep2;
        [Range(0f, 1f)]
        public float AlphaStep;
        [Range(0f, 1f)]
        public float AlphaStep2;
    }
    #endregion

    public RenderMetaballsSettings Settings = new RenderMetaballsSettings();

    const string _profilerTagRenderMetaballsDepthPass = "RenderMetaballsDepthPass";

    const int MESH_DRAW_PASS = 0;

    private Material _kawaseMaterial;
    private Material _blitMaterial;

    private RenderMetaballsDepthPass _renderMetaballsDepthPass;

    #region RENDER METABALLS DEPTH PASS
    class RenderMetaballsDepthPass : ScriptableRenderPass
    {
        const string MetaballDepthRTId = "_MetaballDepthRT";

        int _metaballDepthRTId;
        
        RTHandle _metaballRT;
        RTHandle _metaballRT2;
        RTHandle _metaballRT3;
        RTHandle _cameraTargetRT;

        RenderTextureDescriptor _renderTextureDescriptor;

        public Material Material;
        public Material BlitMaterial;

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

            if (settings.FilterSettings.PassNames != null && settings.FilterSettings.PassNames.Length > 0)
            {
                foreach (var passName in settings.FilterSettings.PassNames)
                    _shaderTagIds.Add(new ShaderTagId(passName));
            }
            else
            {
                _shaderTagIds.Add(new ShaderTagId("SRPDefaultUnlit"));
                _shaderTagIds.Add(new ShaderTagId("UniversalForward"));
                _shaderTagIds.Add(new ShaderTagId("UniversalForwardOnly"));
                _shaderTagIds.Add(new ShaderTagId("LightweightForward"));
            }

            _renderStateBlock = new RenderStateBlock(RenderStateMask.Nothing);

            _renderTextureDescriptor = new RenderTextureDescriptor(Screen.width, Screen.height, RenderTextureFormat.Default);
        }

        public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
        {
            _renderTextureDescriptor.width = cameraTextureDescriptor.width;
            _renderTextureDescriptor.height = cameraTextureDescriptor.height;
            _renderTextureDescriptor.colorFormat = RenderTextureFormat.ARGB32;
            
            RenderingUtils.ReAllocateIfNeeded(ref _metaballRT, _renderTextureDescriptor);
            RenderingUtils.ReAllocateIfNeeded(ref _metaballRT2, _renderTextureDescriptor);
            RenderingUtils.ReAllocateIfNeeded(ref _metaballRT3, _renderTextureDescriptor);

            ConfigureTarget(_metaballRT);            
            //ConfigureClear(ClearFlag.All, Color.clear);
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            SortingCriteria sortingCriteria = (_renderQueueType == RenderQueueType.Transparent) ?
                SortingCriteria.CommonTransparent : renderingData.cameraData.defaultOpaqueSortFlags;
            DrawingSettings drawingSettings = CreateDrawingSettings(_shaderTagIds, ref renderingData, sortingCriteria);

            CommandBuffer cmd = CommandBufferPool.Get();

            using (new ProfilingScope(cmd, _profilingSampler))
            {
                _cameraTargetRT = renderingData.cameraData.renderer.cameraColorTargetHandle;

                cmd.ClearRenderTarget(true, true, Color.clear);
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();

                drawingSettings.overrideMaterial = Material;
                drawingSettings.overrideMaterialPassIndex = 0;
                context.DrawRenderers(renderingData.cullResults, ref drawingSettings, ref _filteringSettings);

                Material.SetInt("_GridSize", 100);
                Material.SetFloat("_Spread", 100f);

                Blit(cmd, _metaballRT, _metaballRT2, Material, 2);
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();

                Blit(cmd, _metaballRT2, _metaballRT, Material, 3);
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();

                cmd.SetGlobalFloat("_ColorStep",     _settings.ColorStep);
                cmd.SetGlobalFloat("_ColorStep2",    _settings.ColorStep2);
                cmd.SetGlobalFloat("_AlphaStep",     _settings.AlphaStep);
                cmd.SetGlobalFloat("_AlphaStep2",    _settings.AlphaStep2);

                Material.SetTexture("_MainTex", _cameraTargetRT);
                Material.EnableKeyword("_ALPHATEST_ON");
                Material.SetFloat("_AlphaClip", 0.5f);
                Blit(cmd, _metaballRT, _cameraTargetRT, Material, 4);
            }

            context.ExecuteCommandBuffer(cmd);
            //cmd.Clear();
            CommandBufferPool.Release(cmd);
        }
    }
    #endregion
    public override void Create()
    {
        if (!Settings.IsEnabled || !Settings.KawaseShader) return;
        
        _kawaseMaterial = CoreUtils.CreateEngineMaterial(Settings.KawaseShader);
        _blitMaterial = CoreUtils.CreateEngineMaterial(Settings.BlitShader);

        _renderMetaballsDepthPass = new RenderMetaballsDepthPass(Settings)
        {            
            Material = _kawaseMaterial,
            BlitMaterial = _blitMaterial,
        };
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (_renderMetaballsDepthPass == null) return;

        if (renderingData.cameraData.cameraType == CameraType.Game)
        {
            renderer.EnqueuePass(_renderMetaballsDepthPass);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (_renderMetaballsDepthPass == null) return;

        CoreUtils.Destroy(_kawaseMaterial);
        CoreUtils.Destroy(_blitMaterial);
    }
}