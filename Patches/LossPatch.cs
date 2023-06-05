using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HelheimHarmonizer.Util;
using HarmonyLib;
using JetBrains.Annotations;
using UnityEngine;
using YamlDotNet.Serialization;

namespace HelheimHarmonizer.Patches;

[HarmonyPatch(typeof(Player), nameof(Player.CreateTombStone))]
static class PatchItemLoss
{
    [UsedImplicitly]
    [HarmonyPriority(Priority.VeryHigh)]
    private static void Prefix(Player __instance, out List<ItemDrop.ItemData> __state)
    {
        List<ItemDrop.ItemData> toDrop = new();
        List<ItemDrop.ItemData> toDestroy = new();
        __state = new List<ItemDrop.ItemData>();

        foreach (ItemDrop.ItemData item in __instance.m_inventory.m_inventory)
        {
            string itemPrefab = Utils.GetPrefabName(item.m_dropPrefab);
            string inventoryLocation = item.m_gridPos.y == 0 ? "hotbar" : "inventory";
            ItemAction action = Functions.DetermineItemAction(inventoryLocation, itemPrefab);
            bool noItemLoss = HelheimHarmonizerPlugin.noItemLoss.Value == HelheimHarmonizerPlugin.Toggle.On;
            if (noItemLoss)
            {
                __state.Add(item);
                HelheimHarmonizerPlugin.HelheimHarmonizerLogger.LogDebug(
                    $"Keeping {itemPrefab} in {inventoryLocation} due to noItemLoss setting.");
            }
            else
            {
                switch (action)
                {
                    case ItemAction.Keep:
                        __state.Add(item);
                        HelheimHarmonizerPlugin.HelheimHarmonizerLogger.LogDebug(
                            $"Keeping {itemPrefab} in {inventoryLocation}.");
                        break;
                    case ItemAction.Destroy:
                        toDestroy.Add(item);
                        HelheimHarmonizerPlugin.HelheimHarmonizerLogger.LogDebug(
                            $"Destroying {itemPrefab} in {inventoryLocation}.");
                        break;
                    default: // ItemAction.Drop
                        toDrop.Add(item);
                        HelheimHarmonizerPlugin.HelheimHarmonizerLogger.LogDebug(
                            $"Dropping {itemPrefab} from {inventoryLocation}.");
                        break;
                }
            }
        }

        __instance.m_inventory.m_inventory = toDrop;
    }

    [UsedImplicitly]
    [HarmonyPriority(Priority.VeryLow)]
    private static void Postfix(Player __instance, List<ItemDrop.ItemData> __state)
    {
        foreach (ItemDrop.ItemData item in __state)
        {
            HelheimHarmonizerPlugin.HelheimHarmonizerLogger.LogDebug($"Adding {item.m_dropPrefab.name} back to inventory.");
            __instance.m_inventory.m_inventory.Add(item);
        }

        __instance.m_inventory.Changed();
    }
}



[HarmonyPatch(typeof(Player),nameof(Player.CreateDeathEffects))]
static class PlayerCreateDeathEffectsPatch
{
    static bool Prefix(Player __instance)
    {
        return HelheimHarmonizerPlugin.createDeathEffects.Value != HelheimHarmonizerPlugin.Toggle.Off;
    }
}

[HarmonyPatch(typeof(Skills),nameof(Skills.OnDeath))]
static class SkillsOnDeathPatch
{
    static bool Prefix(Skills __instance)
    {
        return HelheimHarmonizerPlugin.reduceSkills.Value != HelheimHarmonizerPlugin.Toggle.Off;
    }
}

[HarmonyPatch(typeof(Skills),nameof(Skills.LowerAllSkills))]
[HarmonyBefore("com.orianaventure.mod.WorldAdvancementProgression")]
static class SkillsLowerAllSkillsPatch
{
    static void Prefix(ref float factor)
    {
        // You can modify the 'factor' argument here as needed
        if (HelheimHarmonizerPlugin.skillReduceFactor.Value != -1f)
        {
        #if DEBUG
            HelheimHarmonizerPlugin.HelheimHarmonizerLogger.LogMessage($"Modifying LowerAllSkills factor from {factor} to {HelheimHarmonizerPlugin.skillReduceFactor.Value}");
        #endif
            factor = HelheimHarmonizerPlugin.skillReduceFactor.Value;
        }
    }
}


[HarmonyPatch(typeof(Player), nameof(Player.OnDeath))]
public static class PatchPlayerOnDeath
{
    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        List<CodeInstruction> codes = new(instructions);

        for (int i = 0; i < codes.Count; ++i)
        {
            CodeInstruction code = codes[i];
            #if DEBUG
            HelheimHarmonizerPlugin.HelheimHarmonizerLogger.LogMessage($"Processing instruction {i} with opcode {code.opcode} and operand {code.operand}");
            #endif

            // Toggle ClearFoods
            if (code.opcode == OpCodes.Callvirt && (MethodInfo)code.operand ==
                AccessTools.Method(typeof(List<Player.Food>), nameof(List<Player.Food>.Clear)))
            {
                if (HelheimHarmonizerPlugin.clearFoods.Value == HelheimHarmonizerPlugin.Toggle.Off)
                {
                #if DEBUG
                    HelheimHarmonizerPlugin.HelheimHarmonizerLogger.LogMessage("ClearFoods toggled off, inserting NoOp");
                #endif
                    code.opcode = OpCodes.Nop;
                }
            }

            yield return code;
        }
    }
}
