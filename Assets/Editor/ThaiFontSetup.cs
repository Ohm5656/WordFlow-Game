using System.IO;
using System.Text;
using PhEngine.ThaiTextCare.Editor;
using TMPro;
using UnityEditor;
using UnityEngine;

internal static class ThaiFontSetup
{
    private const string FontAssetPath = "Assets/Fonts/LeelawUI SDF.asset";
    private const string DoctorAssetPath = "Assets/Fonts/ThaiFontDoctor_LeelawUI.asset";

    [InitializeOnLoadMethod]
    private static void Run()
    {
        EditorApplication.delayCall += RunOnce;
    }

    private static void RunOnce()
    {
        var fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
        if (fontAsset == null)
        {
            Debug.LogWarning($"[ThaiFontSetup] Font asset not found at {FontAssetPath}");
            return;
        }

        PopulateThaiGlyphs(fontAsset);
        ApplyGlyphAdjustments(fontAsset);
    }

    private static void PopulateThaiGlyphs(TMP_FontAsset fontAsset)
    {
        var sb = new StringBuilder();
        for (int c = 0x0E01; c <= 0x0E5B; c++) sb.Append((char)c);   // Thai script block
        for (int c = 0x0020; c <= 0x007E; c++) sb.Append((char)c);   // basic Latin + digits + punctuation

        bool allAdded = fontAsset.TryAddCharacters(sb.ToString(), out string missing);
        EditorUtility.SetDirty(fontAsset);
        AssetDatabase.SaveAssets();

        Debug.Log(allAdded
            ? "[ThaiFontSetup] All requested Thai + Latin glyphs added to LeelawUI SDF."
            : $"[ThaiFontSetup] Added glyphs to LeelawUI SDF; {missing.Length} unicode points unavailable in the source font (skipped).");
    }

    private static void ApplyGlyphAdjustments(TMP_FontAsset fontAsset)
    {
        var doctor = AssetDatabase.LoadAssetAtPath<ThaiFontDoctor>(DoctorAssetPath);
        if (doctor == null)
        {
            doctor = ScriptableObject.CreateInstance<ThaiFontDoctor>();
            AssetDatabase.CreateAsset(doctor, DoctorAssetPath);
        }

        doctor.SetFontAsset(fontAsset);
        doctor.glyphCombinationList.Clear();
        doctor.glyphCombinationList.Add(Combo(ThaiGlyphPreset.Custom, "ิีื็ัึ", 0, 0, ThaiGlyphPreset.Custom, "์่้๊๋", 10, 18));
        doctor.glyphCombinationList.Add(Combo(ThaiGlyphPreset.UpperVowels, null, 0, 0, ThaiGlyphPreset.Custom, "๋", 0, 20));
        doctor.glyphCombinationList.Add(Combo(ThaiGlyphPreset.UpperVowels, null, 0, 0, ThaiGlyphPreset.Custom, "่", 0, 20));
        doctor.glyphCombinationList.Add(Combo(ThaiGlyphPreset.ToneMarks, null, 5, 22, ThaiGlyphPreset.Custom, "ำ", 0, 0));
        doctor.glyphCombinationList.Add(Combo(ThaiGlyphPreset.DescenderConsonants, null, 0, 0, ThaiGlyphPreset.LowerVowels, null, -5, -18));
        doctor.glyphCombinationList.Add(Combo(ThaiGlyphPreset.AscenderConsonants, null, 0, 0, ThaiGlyphPreset.Custom, "่", -15, 0));

        doctor.ApplyModifications();
        EditorUtility.SetDirty(doctor);
        AssetDatabase.SaveAssets();

        Debug.Log("[ThaiFontSetup] ThaiFontDoctor glyph-collision fix applied to LeelawUI SDF.");
    }

    private static GlyphCombination Combo(
        ThaiGlyphPreset firstPreset, string firstGlyphsOverride, float firstX, float firstY,
        ThaiGlyphPreset secondPreset, string secondGlyphsOverride, float secondX, float secondY)
    {
        var combo = new GlyphCombination();
        combo.first.AssignGroup(firstPreset);
        if (firstGlyphsOverride != null) combo.first.glyphs = firstGlyphsOverride;
        combo.first.xPlacement = firstX;
        combo.first.yPlacement = firstY;

        combo.second.AssignGroup(secondPreset);
        if (secondGlyphsOverride != null) combo.second.glyphs = secondGlyphsOverride;
        combo.second.xPlacement = secondX;
        combo.second.yPlacement = secondY;

        return combo;
    }
}
