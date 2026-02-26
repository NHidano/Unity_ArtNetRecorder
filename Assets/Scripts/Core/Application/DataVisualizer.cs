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

        // ComputeShaderのスレッドグループサイズ(32)の倍数に切り上げ、最小32を保証
        this.maxUniverseNum = Mathf.Max(32, ((maxUniverseNum + 31) / 32) * 32);

        kernelIndex = dmxTextureBufferCompute.FindKernel("CSMain");
        dmxComputeBuffer = new ComputeBuffer(this.maxUniverseNum * 512, sizeof(float));
        dmxTextureBufferCompute.SetBuffer(kernelIndex, "_Buffer", dmxComputeBuffer);

        dmxBuffer = CreateRenderTexture(512, this.maxUniverseNum);
        dmxTextureBufferCompute.SetTexture(kernelIndex, "_Result", dmxBuffer);

        rawImage.texture = dmxBuffer;
        rawImage.enabled = true;
    }

    public void Exec(float[] dmxRaw)
    {
        if (dmxComputeBuffer == null || dmxBuffer == null) return;

        dmxComputeBuffer.SetData(dmxRaw);
        dmxTextureBufferCompute.Dispatch(kernelIndex, 512 / 32, maxUniverseNum / 32, 1);
    }
    
    private RenderTexture CreateRenderTexture(int width, int height)
    {
        var desc = new RenderTextureDescriptor(width, height, GraphicsFormat.R16_SFloat, 0);
        desc.enableRandomWrite = true;
        var renderTexture = new RenderTexture(desc);
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
        // RawImageの参照をGPUリソース解放前にクリアし、
        // Canvasが解放済みテクスチャを描画するのを防ぐ
        if (rawImage != null)
        {
            rawImage.texture = null;
            rawImage.enabled = false;
        }

        // GPUの未処理コマンドを送出してから解放する
        GL.Flush();

        dmxComputeBuffer?.Release();
        dmxComputeBuffer = null;
        dmxBuffer?.Release();
        dmxBuffer = null;
    }
}
