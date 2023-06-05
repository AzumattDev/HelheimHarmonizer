using HarmonyLib;
using UnityEngine;

namespace HelheimHarmonizer.Patches;

[HarmonyPatch(typeof(Game), nameof(Game.FindSpawnPoint))]
static class FindSpawnPointPatch
{
    static bool Prefix(ref Vector3 point, ref bool usedLogoutPoint, bool ___m_firstSpawn, ref bool __result)
    {
        if (___m_firstSpawn)
            return true;

        if (HelheimHarmonizerPlugin.spawnAtStart.Value == HelheimHarmonizerPlugin.Toggle.On)
        {
            usedLogoutPoint = false;

            if (ZoneSystem.instance.GetLocationIcon(Game.instance.m_StartLocation, out Vector3 a))
            {
                point = a + Vector3.up * 2f;
                ZNet.instance.SetReferencePosition(point);
                __result = ZNetScene.instance.IsAreaReady(point);
                if (__result)
                    HelheimHarmonizerPlugin.HelheimHarmonizerLogger.LogDebug($"Respawning at start: {point}");
            }
            else
            {
                HelheimHarmonizerPlugin.HelheimHarmonizerLogger.LogDebug("Start point not found");
                ZNet.instance.SetReferencePosition(Vector3.zero);
                point = Vector3.zero;
                __result = false;
            }

            return false;
        }
        else if (HelheimHarmonizerPlugin.useFixedSpawnCoordinates.Value == HelheimHarmonizerPlugin.Toggle.On)
        {
            usedLogoutPoint = false;

            point = HelheimHarmonizerPlugin.fixedSpawnCoordinates.Value;
            ZNet.instance.SetReferencePosition(point);
            __result = ZNetScene.instance.IsAreaReady(point);
            if (__result)
                HelheimHarmonizerPlugin.HelheimHarmonizerLogger.LogDebug($"Respawning at custom point {point}");
            return false;
        }

        return true;
    }
}