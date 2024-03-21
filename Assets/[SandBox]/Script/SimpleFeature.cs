using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

using Object = UnityEngine.Object;

public class SimpleFeature : ScriptableRendererFeature
{
    [SerializeField] private Settings settings;
    [SerializeField] private Shader shader;
    private Material material;
    private RenderPass renderPass;
    
    #region RENDER PASS 
    class RenderPass : ScriptableRenderPass
    {
        private static readonly int horizontalBlurId = Shader.PropertyToID("_HorizontalBlur");
        private static readonly int verticalBlurId = Shader.PropertyToID("_VerticalBlur");

        private Settings settings;
        private Material material;

        private RenderTextureDescriptor renderTextureDescriptor;
        private RTHandle textureHandle;

        public RenderPass(Material material, Settings settings)
        {
            this.material = material;
            this.settings = settings;

            renderTextureDescriptor = new RenderTextureDescriptor(Screen.width, Screen.height, RenderTextureFormat.Default, 0);
        }

        public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
        {
            renderTextureDescriptor.width = cameraTextureDescriptor.width;
            renderTextureDescriptor.height = cameraTextureDescriptor.height;

            RenderingUtils.ReAllocateIfNeeded(ref textureHandle, renderTextureDescriptor);
        }

        private void UpdateSetting()
        {
            if (material == null) return;

            material.SetFloat(horizontalBlurId, settings.horizontalBlur);
            material.SetFloat(verticalBlurId, settings.verticalBlur);
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            CommandBuffer cmd = CommandBufferPool.Get();

            RTHandle cameraTargetHandle = renderingData.cameraData.renderer.cameraColorTargetHandle;

            UpdateSetting();

            Blit(cmd, cameraTargetHandle, textureHandle, material, 0);

            Blit(cmd, textureHandle, cameraTargetHandle, material, 1);

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        public void Dispose()
        {
#if UNITY_EDITOR
            if (EditorApplication.isPlaying)
            {
                Object.Destroy(material);
            }
            else
            {
                Object.DestroyImmediate(material);
            }
#else
            Object.Destroy(material);
#endif
            if (textureHandle != null) textureHandle.Release();
        }
    }
#endregion

    public override void Create()
    {
        if (shader == null) return;

        material = new Material(shader);
        renderPass = new RenderPass(material, settings);

        renderPass.renderPassEvent = RenderPassEvent.AfterRenderingSkybox;
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (renderingData.cameraData.cameraType == CameraType.Game)
        {
            renderer.EnqueuePass(renderPass);
        }
    }

    protected override void Dispose(bool disposing)
    {
        renderPass.Dispose();
#if UNITY_EDITOR
        if (EditorApplication.isPlaying )
        {
            Destroy(material);
        }
        else
        {
            DestroyImmediate(material);
        }
#else
        Destroy(material);
#endif
    }
}

[Serializable]
public class Settings
{
    [Range(0, 0.4f)] public float horizontalBlur;
    [Range(0, 0.4f)] public float verticalBlur;
}