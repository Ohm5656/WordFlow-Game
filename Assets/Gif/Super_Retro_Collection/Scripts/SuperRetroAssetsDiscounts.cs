// ---------------------------------------------------------------------------------
// imports
using UnityEditor;
using UnityEngine;

// Note : I have created a bunch of discounts that automatically apply as long
//        as you onw this asset in the Unity Assets Store. Enjoy !

// ---------------------------------------------------------------------------------
// Dedicated namespace
namespace SuperRetroAssetsDiscounts.MainBundle
{
    // -----------------------------------------------------------------------------
    // Coupon class (links to UAS)
    public static class SuperRetroAssetsDiscounts
    {
        // -------------------------------------------------------------------------
        // Extension Bundle, 50% off
        [MenuItem("Tools/Super Retro Assets Discount/50% off Extension Bundle", false, 100)]
        public static void Fifty_DiscountExtensionBundle()
        {
            Application.OpenURL("https://assetstore.unity.com/packages/slug/364860");
        }
        
        // -------------------------------------------------------------------------
        // Music Bundle, 50% off
        [MenuItem("Tools/Super Retro Assets Discount/50% off Music Bundle", false, 110)]
        public static void Fifty_DiscountMusicBundle()
        {
            Application.OpenURL("https://assetstore.unity.com/packages/slug/261162");
        }
    }
}