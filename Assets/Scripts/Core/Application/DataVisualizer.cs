using System;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.UI;

public class DataVisualizer : MonoBehaviour, IDisposable
{
    [SerializeField] private RawImage rawImage;
    [SerializeField] private ComputeShader dmxTextureBufferCompute;

    private ComputeBuffer dmxComputeBuffer;
    private RenderTexture dmxBuffer;
    private int kernelIndex;

    private int maxUniverseNum;

    public void Open()
    {
        rawImage.enabled = true;
    }

    public void Close()
    {
        rawImage.enabled = false;
    }

    /// <summary>
    /// ビジュアライザーを初期化する。
    /// maxUniverseNumはComputeShaderディスパッチのため32の倍数である必要がある。
    /// </summary>
    public void Initialize(int maxUniverseNum = 64)
    {
        // 再初期化時に既存リソースを解放してからリークを防止する
        Dispose();

        // ComputeShaderのスレッドグループサイズ(32)の倍数に切り上げ（安全策）
        this.maxUniverseNum = ((maxUniverseNum + 31) / 32) * 32;

        kernelIndex = dmxTextureBufferCompute.FindKernel("CSMain");
        dmxComputeBuffer = new ComputeBuffer(this.maxUniverseNum * 512, sizeof(float));
        dmxTextureBufferCompute.SetBuffer(kernelIndex, "_Buffer", dmxComputeBuffer);

        dmxBuffer = CreateRenderTexture(512, this.maxUniverseNum);
        dmxTextureBufferCompute.SetTexture(kernelIndex, "_Result", dmxBuffer);

        rawImage.texture = dmxBuffer;
    }

    // Update is called once per frame
    public void Exec(float[] dmxRaw)
    {

        dmxComputeBuffer.SetData(dmxRaw);
        dmxTextureBufferCompute.Dispatch(kernelIndex, 512 / 32, maxUniverseNum / 32, 1);

    }
    
    private RenderTexture CreateRenderTexture(int width, int height)
    {
        var renderTexture = new RenderTexture(width, height, 0, RenderTextureFormat.ARGBFloat);
        renderTexture.graphicsFormat = GraphicsFormat.R16_SFloat;
        renderTexture.enableRandomWrite = true;
        renderTexture.hideFlags = HideFlags.DontSave;
        renderTexture.filterMode = FilterMode.Point;
        renderTexture.wrapMode = TextureWrapMode.Repeat;
        renderTexture.Create();
        return renderTexture;
    }

    private void OnDestroy()
    {
        Dispose();
    }

    public void Dispose()
    {
        dmxComputeBuffer?.Release();
        dmxComputeBuffer = null;
        dmxBuffer?.Release();
        dmxBuffer = null;
    }
}
