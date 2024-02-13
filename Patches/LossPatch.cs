using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using BepInEx.Configuration;
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
    private static void Prefix(Player __instance, out Dictionary<ItemDrop.ItemData, bool> __state)
    {
        List<ItemDrop.ItemData> toDrop = new();
        List<ItemDrop.ItemData> toDestroy = new();
        __state = new Dictionary<ItemDrop.ItemData, bool>();

        foreach (ItemDrop.ItemData item in __instance.m_inventory.m_inventory)
        {
            string itemPrefab = Utils.GetPrefabName(item.m_dropPrefab);
            string inventoryLocation = item.m_gridPos.y == 0 ? "hotbar" : "inventory";
            bool noItemLoss = HelheimHarmonizerPlugin.noItemLoss.Value == HelheimHarmonizerPlugin.Toggle.On;
            bool keepEquipped = item.m_equipped && HelheimHarmonizerPlugin.keepEquipped.Value == HelheimHarmonizerPlugin.Toggle.On;

            if (noItemLoss || keepEquipped)
            {
                __state.Add(item, item.m_equipped);
                string reason = noItemLoss ? "noItemLoss setting" : "keepEquipped setting";
                HelheimHarmonizerPlugin.HelheimHarmonizerLogger.LogDebug($"Keeping {itemPrefab} in {inventoryLocation} due to the {reason}.");
                continue;
            }

            ItemAction action = Functions.DetermineItemAction(inventoryLocation, itemPrefab);
            switch (action)
            {
                case ItemAction.Keep:
                    __state.Add(item, item.m_equipped);
                    HelheimHarmonizerPlugin.HelheimHarmonizerLogger.LogDebug($"Keeping {itemPrefab} in {inventoryLocation}.");
                    break;
                case ItemAction.Destroy:
                    toDestroy.Add(item);
                    HelheimHarmonizerPlugin.HelheimHarmonizerLogger.LogDebug($"Destroying {itemPrefab} in {inventoryLocation}.");
                    break;
                default: // ItemAction.Drop
                    toDrop.Add(item);
                    HelheimHarmonizerPlugin.HelheimHarmonizerLogger.LogDebug($"Dropping {itemPrefab} from {inventoryLocation}.");
                    break;
            }
        }

        __instance.m_inventory.m_inventory = toDrop;
    }

    [UsedImplicitly]
    [HarmonyPriority(Priority.VeryLow)]
    private static void Postfix(Player __instance, Dictionary<ItemDrop.ItemData, bool> __state)
    {
        foreach (KeyValuePair<ItemDrop.ItemData, bool> item in __state)
        {
            HelheimHarmonizerPlugin.HelheimHarmonizerLogger.LogDebug($"Adding {item.Key.m_dropPrefab.name} back to inventory.");
            __instance.m_inventory.m_inventory.Add(item.Key);
            // Log if the item was equipped before death

            HelheimHarmonizerPlugin.HelheimHarmonizerLogger.LogDebug($"Item {item.Key.m_dropPrefab.name} was equipped: {item.Value}");

            if (item.Value)
            {
                __instance.EquipItem(item.Key, false);
            }
        }

        __instance.m_inventory.Changed();
    }
}

[HarmonyPatch(typeof(Player), nameof(Player.CreateDeathEffects))]
static class PlayerCreateDeathEffectsPatch
{
    static bool Prefix(Player __instance)
    {
        return HelheimHarmonizerPlugin.createDeathEffects.Value != HelheimHarmonizerPlugin.Toggle.Off;
    }
}

[HarmonyPatch(typeof(Skills), nameof(Skills.OnDeath))]
static class SkillsOnDeathPatch
{
    static bool Prefix(Skills __instance)
    {
        return HelheimHarmonizerPlugin.reduceSkills.Value != HelheimHarmonizerPlugin.Toggle.Off;
    }
}

[HarmonyPatch(typeof(Skills), nameof(Skills.LowerAllSkills))]
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
    private static void Prefix(Player __instance, out List<Player.Food> __state)
    {
        foreach (Player.Food food in __instance.m_foods)
        {
            if (food.m_item.m_dropPrefab != null)
                HelheimHarmonizerPlugin.HelheimHarmonizerLogger.LogDebug($"Adding {food.m_item.m_shared.m_name} [{food.m_item.m_dropPrefab.name}] to state");
        }

        __state = new List<Player.Food>(__instance.m_foods);
    }

    private static void Postfix(Player __instance, List<Player.Food> __state)
    {
        if (!ShouldClearFoods())
        {
            foreach (Player.Food food in __state)
            {
                if (food.m_item.m_dropPrefab != null)
                    HelheimHarmonizerPlugin.HelheimHarmonizerLogger.LogDebug($"Adding {food.m_item.m_shared.m_name} [{food.m_item.m_dropPrefab.name}] back to the player's stomach.");
                __instance.m_foods.Add(food);
            }
        }
        else
        {
            HelheimHarmonizerPlugin.HelheimHarmonizerLogger.LogDebug("Clearing the player's stomach.");
        }
    }

    public static bool ShouldClearFoods()
    {
        return HelheimHarmonizerPlugin.clearFoods.Value == HelheimHarmonizerPlugin.Toggle.On;
    }
}