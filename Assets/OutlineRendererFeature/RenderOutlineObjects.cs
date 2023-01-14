using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Serialization;

public class RenderOutlineObjects : ScriptableRendererFeature
{
    #region Render Objects

    class RenderObjectsPass : ScriptableRenderPass
    {
        readonly int _renderTargetId;
        readonly ProfilingSampler _profilingSampler;
        readonly List<ShaderTagId> _shaderTagIds = new List<ShaderTagId>();

        RenderTargetIdentifier _renderTargetIdentifier;
        FilteringSettings _filteringSettings;
        RenderStateBlock _renderStateBlock;

        public RenderObjectsPass(string profilerTag, int renderTargetId, LayerMask layerMask)
        {
            _profilingSampler = new ProfilingSampler(profilerTag);
            _renderTargetId = renderTargetId;

            _filteringSettings = new FilteringSettings(null, layerMask);

            _shaderTagIds.Add(new ShaderTagId("SRPDefaultUnlit"));
            _shaderTagIds.Add(new ShaderTagId("UniversalForward"));
            _shaderTagIds.Add(new ShaderTagId("UniversalForwardOnly"));
            _shaderTagIds.Add(new ShaderTagId("LightweightForward"));

            _renderStateBlock = new RenderStateBlock(RenderStateMask.Nothing);
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            RenderTextureDescriptor blitTargetDescriptor = renderingData.cameraData.cameraTargetDescriptor;
            blitTargetDescriptor.colorFormat = RenderTextureFormat.ARGB32;
            cmd.GetTemporaryRT(_renderTargetId, blitTargetDescriptor);
            _renderTargetIdentifier = new RenderTargetIdentifier(_renderTargetId);

            ConfigureTarget(_renderTargetIdentifier, renderingData.cameraData.renderer.cameraDepthTarget);
            ConfigureClear(ClearFlag.Color, Color.clear);
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            SortingCriteria sortingCriteria = SortingCriteria.CommonOpaque;
            DrawingSettings drawingSettings =
                CreateDrawingSettings(_shaderTagIds, ref renderingData, sortingCriteria);

            CommandBuffer cmd = CommandBufferPool.Get("RenderOutlineObjectsBlit");
            using (new ProfilingScope(cmd, _profilingSampler))
            {
                context.DrawRenderers(renderingData.cullResults, ref drawingSettings, ref _filteringSettings,
                    ref _renderStateBlock);
            }

            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();
            CommandBufferPool.Release(cmd);
        }
    }

    #endregion

    #region Sobel Filter

    class SobelRenderPass : ScriptableRenderPass
    {
        public Material SobelMaterial;
        public Material BlitMaterial;
        public int SobelPassIndex;
        public int BlitMaterialPassIndex;
        
        int _tmpId1;
        
        RenderTargetIdentifier _tmpRT1;
        
        readonly int _sobelSourceId;
        RenderTargetIdentifier _sobelSourceIdentifier;
        
        readonly ProfilingSampler _profilingSampler;
        
        public SobelRenderPass(string profilerTag, int sobelSourceId)
        {
            _profilingSampler = new ProfilingSampler(profilerTag);
            _sobelSourceId = sobelSourceId;
        }
        
        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            _sobelSourceIdentifier = new RenderTargetIdentifier(_sobelSourceId);
        }

        public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
        {
            var width = cameraTextureDescriptor.width;
            var height = cameraTextureDescriptor.height;
            
            _tmpId1 = Shader.PropertyToID("tmpOutlineRT");
            cmd.GetTemporaryRT(_tmpId1, width, height, 0, FilterMode.Bilinear, RenderTextureFormat.ARGB32);
            
            _tmpRT1 = new RenderTargetIdentifier(_tmpId1);
            
            ConfigureTarget(_tmpRT1);
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            RenderTextureDescriptor opaqueDesc = renderingData.cameraData.cameraTargetDescriptor;
            opaqueDesc.depthBufferBits = 0;
            
            CommandBuffer cmd = CommandBufferPool.Get();
            using (new ProfilingScope(cmd, _profilingSampler))
            {
                // first pass
                cmd.Blit(_sobelSourceIdentifier, _tmpRT1, SobelMaterial, SobelPassIndex);
                // final pass
                cmd.Blit(_tmpRT1, renderingData.cameraData.renderer.cameraColorTarget, BlitMaterial, BlitMaterialPassIndex);
            }
            
            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();
            
            CommandBufferPool.Release(cmd);
        }

        public override void OnCameraCleanup(CommandBuffer cmd)
        {
            cmd.ReleaseTemporaryRT(_tmpId1);
        }
    }

    #endregion

    #region Renderer Feature

    RenderObjectsPass _renderObjectsPass;
    SobelRenderPass _sobelPass;

    const string PassTag = "RenderOutlineObjects";
    [SerializeField] string _renderTargetId = "_RenderOutlineRT";
    [SerializeField] LayerMask _layerMask;
    [SerializeField] MaterialAndPass _sobelMaterial;
    [SerializeField] MaterialAndPass _blitMaterial;

    public override void Create()
    {
        int renderTargetId = Shader.PropertyToID(_renderTargetId);
        _renderObjectsPass = new RenderObjectsPass(PassTag, renderTargetId, _layerMask);
        
        _sobelPass = new SobelRenderPass("SobelFilter", renderTargetId)
        {
            SobelMaterial = _sobelMaterial.Material,
            BlitMaterial = _blitMaterial.Material,
            SobelPassIndex = _sobelMaterial.Index,
            BlitMaterialPassIndex = _blitMaterial.Index,
            renderPassEvent = RenderPassEvent.AfterRenderingTransparents
        };
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        renderer.EnqueuePass(_renderObjectsPass);
        renderer.EnqueuePass(_sobelPass);
    }

    #endregion
}