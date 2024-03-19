using System;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[Serializable, VolumeComponentMenuForRenderPipeline("Custom/Outline", typeof(UniversalRenderPipeline))]
public class Outline : VolumeComponent, IPostProcessComponent
{
    public FloatParameter OutlineThickness = new FloatParameter(1);
    public FloatParameter DepthSensitivity = new FloatParameter(1);
    public FloatParameter NormalsSensitivity = new FloatParameter(1);
    public FloatParameter ColorSensitivity = new FloatParameter(1);

    public bool IsActive() => true;
    public bool IsTileCompatible() => true;
}
