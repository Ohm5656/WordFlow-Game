using UnityEditor;
using UnityEngine;

/// Tools/Quest/Fog Shader Check
/// ShaderUtil compile-error/message dump for WordFlow/EdgeFog — unity_get_compilation_errors only
/// covers C# (CompilationPipeline), not shader HLSL errors, so this is the only way to see them.
public static class FogShaderCheck
{
    [MenuItem("Tools/Quest/Fog Shader Check")]
    public static void Check()
    {
        var shader = Shader.Find("WordFlow/EdgeFog");
        if (shader == null)
        {
            Debug.LogError("[FogShaderCheck] Shader.Find returned null for WordFlow/EdgeFog");
            return;
        }

        Debug.Log($"[FogShaderCheck] isSupported={shader.isSupported}");

        int count = ShaderUtil.GetShaderMessageCount(shader);
        Debug.Log($"[FogShaderCheck] message count = {count}");

        var messages = ShaderUtil.GetShaderMessages(shader);
        foreach (var m in messages)
        {
            Debug.LogWarning($"[FogShaderCheck] {m.severity} {m.file}:{m.line} {m.message}");
        }
    }
}
