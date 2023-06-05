using HelheimHarmonizer.Util;
using HarmonyLib;

namespace HelheimHarmonizer.Patches;

[HarmonyPatch(typeof(ObjectDB), nameof(ObjectDB.Awake))]
static class PredefinedGroupGrab
{
    static void Postfix(ObjectDB __instance)
    {
        if (!ZNetScene.instance)
            return;
        Functions.CreatePredefinedGroups(__instance);
    }
}