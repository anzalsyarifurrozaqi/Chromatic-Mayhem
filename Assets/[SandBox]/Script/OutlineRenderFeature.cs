using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class OutlineRenderFeature : ScriptableRendererFeature
{
    private OutlinePass _outlinePass;

    public override void Create()
    {
        _outlinePass = new OutlinePass();
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        renderer.EnqueuePass(_outlinePass);
    }


    class OutlinePass : ScriptableRenderPass
    {
        private Material _material;
        int tintId = Shader.PropertyToID("_Temp");
        RenderTargetIdentifier src, tint;

        public OutlinePass()
        {
            if(!_material)
            {
                _material = CoreUtils.CreateEngineMaterial("Hidden/Outline");
            }
            renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            RenderTextureDescriptor desc = renderingData.cameraData.cameraTargetDescriptor;            
            src = renderingData.cameraData.renderer.cameraColorTarget;
            cmd.GetTemporaryRT(tintId, desc, FilterMode.Bilinear);
            tint = new RenderTargetIdentifier(tintId);
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            CommandBuffer commandBufffer = CommandBufferPool.Get("OutlineRenderFeature");
            VolumeStack volumes = VolumeManager.instance.stack;
            Outline outline = volumes.GetComponent<Outline>();
            if (outline.IsActive())
            {                
                _material.SetFloat("_OutlineThickness", (float)outline.OutlineThickness);
                _material.SetFloat("_DepthSensitivity", (float)outline.DepthSensitivity);
                _material.SetFloat("_NormalsSensitivity", (float)outline.NormalsSensitivity);
                _material.SetFloat("_ColorSensitivity", (float)outline.ColorSensitivity);

                Blit(commandBufffer, src, tint, _material, 0);
                Blit(commandBufffer, tint, src);
            }

            context.ExecuteCommandBuffer(commandBufffer);
            CommandBufferPool.Release(commandBufffer);
        }

        public override void OnCameraCleanup(CommandBuffer cmd)
        {
            cmd.ReleaseTemporaryRT(tintId);
        }
    }
}
