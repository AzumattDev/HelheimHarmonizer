using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using HelheimHarmonizer.Patches;
using UnityEngine;

namespace HelheimHarmonizer.Util;

public enum ItemAction
{
    Drop,
    Keep,
    Destroy
}

public class Functions
{
    public static bool ShouldNotDrop(string inventoryLocation, string prefab)
    {
        if (HelheimHarmonizerPlugin.yamlData == null)
        {
            HelheimHarmonizerPlugin.HelheimHarmonizerLogger.LogError(
                "yamlData is null. Make sure to call DeserializeYamlFile() before using ShouldNotDrop.");
            return false;
        }

        if (!HelheimHarmonizerPlugin.yamlData.ContainsKey(inventoryLocation))
        {
            //HelheimHarmonizerPlugin.HelheimHarmonizerLogger.LogInfo($"Container '{container}' not found in yamlData.");
            return true; // Allow pulling by default if the container is not defined in yamlData
        }

        var containerData = HelheimHarmonizerPlugin.yamlData[inventoryLocation] as Dictionary<object, object>;
        if (containerData == null)
        {
            HelheimHarmonizerPlugin.HelheimHarmonizerLogger.LogError(
                $"Unable to cast containerData for container '{inventoryLocation}' to Dictionary<object, object>.");
            return false;
        }

        var excludeList = containerData.TryGetValue("exclude", out object? value1)
            ? value1 as List<object>
            : new List<object>();
        var includeOverrideList = containerData.TryGetValue("includeOverride", out object? value)
            ? value as List<object>
            : new List<object>();

        if (excludeList == null)
        {
            HelheimHarmonizerPlugin.HelheimHarmonizerLogger.LogError(
                $"Unable to cast excludeList for container '{inventoryLocation}' to List<object>.");
            return false;
        }

        if (includeOverrideList == null)
        {
            HelheimHarmonizerPlugin.HelheimHarmonizerLogger.LogError(
                $"Unable to cast includeOverrideList for container '{inventoryLocation}' to List<object>.");
            return false;
        }

        if (includeOverrideList.Contains(prefab))
        {
            return false;
        }

        foreach (var excludedItem in excludeList)
        {
            if (prefab.Equals(excludedItem))
            {
                return true;
            }

            if (GroupUtils.IsGroupDefined((string)excludedItem))
            {
                var groupItems = GroupUtils.GetItemsInGroup((string)excludedItem);
                if (groupItems.Contains(prefab))
                {
                    return true;
                }
            }
        }

        return false;
    }
    
    public static bool ShouldDestroy(string inventoryLocation, string prefab)
    {
        if (HelheimHarmonizerPlugin.yamlData == null)
        {
            HelheimHarmonizerPlugin.HelheimHarmonizerLogger.LogError(
                "yamlData is null. Make sure to call DeserializeYamlFile() before using ShouldDestroy.");
            return false;
        }

        if (!HelheimHarmonizerPlugin.yamlData.ContainsKey(inventoryLocation))
        {
            return false; // By default, don't destroy if the location is not defined in yamlData
        }

        var containerData = HelheimHarmonizerPlugin.yamlData[inventoryLocation] as Dictionary<object, object>;
        if (containerData == null)
        {
            HelheimHarmonizerPlugin.HelheimHarmonizerLogger.LogError(
                $"Unable to cast containerData for container '{inventoryLocation}' to Dictionary<object, object>.");
            return false;
        }

        var destroyList = containerData.TryGetValue("destroy", out object? value)
            ? value as List<object>
            : new List<object>();

        if (destroyList == null)
        {
            HelheimHarmonizerPlugin.HelheimHarmonizerLogger.LogError(
                $"Unable to cast destroyList for container '{inventoryLocation}' to List<object>.");
            return false;
        }

        foreach (var destroyItem in destroyList)
        {
            if (prefab.Equals(destroyItem))
            {
                return true;
            }

            if (GroupUtils.IsGroupDefined((string)destroyItem))
            {
                var groupItems = GroupUtils.GetItemsInGroup((string)destroyItem);
                if (groupItems.Contains(prefab))
                {
                    return true;
                }
            }
        }

        return false;
    }

    public static ItemAction DetermineItemAction(string inventoryLocation, string prefab)
    {
        if (HelheimHarmonizerPlugin.yamlData == null)
        {
            HelheimHarmonizerPlugin.HelheimHarmonizerLogger.LogError(
                "yamlData is null. Make sure to call DeserializeYamlFile() before using DetermineItemAction.");
            return ItemAction.Drop;
        }

        if (!HelheimHarmonizerPlugin.yamlData.ContainsKey(inventoryLocation))
        {
            return ItemAction.Drop; // By default, drop if the location is not defined in yamlData
        }

        var containerData = HelheimHarmonizerPlugin.yamlData[inventoryLocation] as Dictionary<object, object>;
        if (containerData == null)
        {
            HelheimHarmonizerPlugin.HelheimHarmonizerLogger.LogError(
                $"Unable to cast containerData for container '{inventoryLocation}' to Dictionary<object, object>.");
            return ItemAction.Drop;
        }

        // Check if the item is in the "destroy" list
        var destroyList = containerData.TryGetValue("destroy", out object? destroyValue)
            ? destroyValue as List<object>
            : new List<object>();
        if (destroyList != null)
        {
            if (destroyList.Contains(prefab))
            {
                return ItemAction.Destroy;
            }

            foreach (var destroyItem in destroyList)
            {
                if (GroupUtils.IsGroupDefined((string)destroyItem))
                {
                    var groupItems = GroupUtils.GetItemsInGroup((string)destroyItem);
                    if (groupItems.Contains(prefab))
                    {
                        return ItemAction.Destroy;
                    }
                }
            }
        }

        // For each action, check if it's defined and if the item is in the corresponding list
        var actions = new Dictionary<string, ItemAction> { { "includeOverride", ItemAction.Drop } };
        foreach (var action in actions)
        {
            var itemList = containerData.TryGetValue(action.Key, out object? value)
                ? value as List<object>
                : new List<object>();

            if (itemList == null)
            {
                HelheimHarmonizerPlugin.HelheimHarmonizerLogger.LogError(
                    $"Unable to cast {action.Key}List for container '{inventoryLocation}' to List<object>.");
                continue;
            }

            foreach (var actionItem in itemList)
            {
                if (prefab.Equals(actionItem))
                {
                    return action.Value;
                }

                if (GroupUtils.IsGroupDefined((string)actionItem))
                {
                    var groupItems = GroupUtils.GetItemsInGroup((string)actionItem);
                    if (groupItems.Contains(prefab))
                    {
                        return action.Value;
                    }
                }
            }
        }

        // Check if the item is in the "exclude" list
        var excludeList = containerData.TryGetValue("exclude", out object? excludeValue)
            ? excludeValue as List<object>
            : new List<object>();
        if (excludeList != null)
        {
            if (excludeList.Contains(prefab))
            {
                return ItemAction.Keep;
            }

            foreach (var excludeItem in excludeList)
            {
                if (GroupUtils.IsGroupDefined((string)excludeItem))
                {
                    var groupItems = GroupUtils.GetItemsInGroup((string)excludeItem);
                    if (groupItems.Contains(prefab))
                    {
                        return ItemAction.Keep;
                    }
                }
            }
        }

        return ItemAction.Drop; // By default, drop the item
    }



    public static string ReadYamlFile()
    {
        return File.ReadAllText(HelheimHarmonizerPlugin.yamlPath);
    }


    internal static void WriteConfigFileFromResource(string configFilePath)
    {
        Assembly assembly = Assembly.GetExecutingAssembly();
        string resourceName = $"HelheimHarmonizer.HelheimHarmonizer.defaultConfig.yml";

        using Stream resourceStream = assembly.GetManifestResourceStream(resourceName);
        if (resourceStream == null)
        {
            throw new FileNotFoundException($"Resource '{resourceName}' not found in the assembly.");
        }

        using StreamReader reader = new StreamReader(resourceStream);
        string contents = reader.ReadToEnd();

        File.WriteAllText(configFilePath, contents);
    }

    internal static bool CheckItemDropIntegrity(ItemDrop itemDropComp)
    {
        if (itemDropComp.m_itemData == null) return false;
        return itemDropComp.m_itemData.m_shared != null;
    }

    internal static void CreatePredefinedGroups(ObjectDB __instance)
    {
        foreach (GameObject gameObject in __instance.m_items.Where(x => x.GetComponentInChildren<ItemDrop>() != null))
        {
            var itemDrop = gameObject.GetComponentInChildren<ItemDrop>();
            if (!CheckItemDropIntegrity(itemDrop)) continue;
            itemDrop.m_itemData.m_dropPrefab = itemDrop.gameObject; // Fix all drop prefabs to be the actual item
            if (itemDrop.m_itemData.m_dropPrefab != null)
            {
                ItemDrop.ItemData.SharedData sharedData = itemDrop.m_itemData.m_shared;
                string groupName = "";

                if (sharedData.m_food > 0.0 && sharedData.m_foodStamina > 0.0)
                {
                    groupName = "Food";
                }

                if (sharedData.m_food > 0.0 && sharedData.m_foodStamina == 0.0)
                {
                    groupName = "Potion";
                }
                else if (sharedData.m_itemType == ItemDrop.ItemData.ItemType.Fish)
                {
                    groupName = "Fish";
                }

                switch (sharedData.m_itemType)
                {
                    case ItemDrop.ItemData.ItemType.OneHandedWeapon or ItemDrop.ItemData.ItemType.TwoHandedWeapon
                        or ItemDrop.ItemData.ItemType.TwoHandedWeaponLeft or ItemDrop.ItemData.ItemType.Bow:
                        switch (sharedData.m_skillType)
                        {
                            case Skills.SkillType.Swords:
                                groupName = "Swords";
                                break;
                            case Skills.SkillType.Bows:
                                groupName = "Bows";
                                break;
                            case Skills.SkillType.Crossbows:
                                groupName = "Crossbows";
                                break;
                            case Skills.SkillType.Axes:
                                groupName = "Axes";
                                break;
                            case Skills.SkillType.Clubs:
                                groupName = "Clubs";
                                break;
                            case Skills.SkillType.Knives:
                                groupName = "Knives";
                                break;
                            case Skills.SkillType.Pickaxes:
                                groupName = "Pickaxes";
                                break;
                            case Skills.SkillType.Polearms:
                                groupName = "Polearms";
                                break;
                            case Skills.SkillType.Spears:
                                groupName = "Spears";
                                break;
                        }
                        break;
                    case ItemDrop.ItemData.ItemType.Chest or ItemDrop.ItemData.ItemType.Legs
                        or ItemDrop.ItemData.ItemType.Hands or ItemDrop.ItemData.ItemType.Shoulder
                        or ItemDrop.ItemData.ItemType.Helmet:
                        groupName = "Armor";
                        break;
                    case ItemDrop.ItemData.ItemType.Torch:
                        groupName = "Equipment";
                        break;
                    case ItemDrop.ItemData.ItemType.Trophy:
                        string[] bossTrophies =
                            { "eikthyr", "elder", "bonemass", "dragonqueen", "goblinking", "SeekerQueen" };
                        groupName = bossTrophies.Any(sharedData.m_name.EndsWith) ? "Boss Trophy" : "Trophy";
                        break;
                    case ItemDrop.ItemData.ItemType.Material:
                        if (ObjectDB.instance.GetItemPrefab("Cultivator").GetComponent<ItemDrop>().m_itemData.m_shared
                                .m_buildPieces.m_pieces.FirstOrDefault(p =>
                                {
                                    Piece.Requirement[] requirements = p.GetComponent<Piece>().m_resources;
                                    return requirements.Length == 1 &&
                                           requirements[0].m_resItem.m_itemData.m_shared.m_name == sharedData.m_name;
                                }) is { } piece)
                        {
                            groupName = piece.GetComponent<Plant>()?.m_grownPrefabs[0].GetComponent<Pickable>()
                                ?.m_amount > 1
                                ? "Crops"
                                : "Seeds";
                        }

                        if (ZNetScene.instance.GetPrefab("smelter").GetComponent<Smelter>().m_conversion
                            .Any(c => c.m_from.m_itemData.m_shared.m_name == sharedData.m_name))
                        {
                            groupName = "Ores";
                        }

                        if (ZNetScene.instance.GetPrefab("smelter").GetComponent<Smelter>().m_conversion
                            .Any(c => c.m_to.m_itemData.m_shared.m_name == sharedData.m_name))
                        {
                            groupName = "Metals";
                        }

                        if (ZNetScene.instance.GetPrefab("blastfurnace").GetComponent<Smelter>().m_conversion
                            .Any(c => c.m_from.m_itemData.m_shared.m_name == sharedData.m_name))
                        {
                            groupName = "Ores";
                        }

                        if (ZNetScene.instance.GetPrefab("blastfurnace").GetComponent<Smelter>().m_conversion
                            .Any(c => c.m_to.m_itemData.m_shared.m_name == sharedData.m_name))
                        {
                            groupName = "Metals";
                        }

                        if (ZNetScene.instance.GetPrefab("charcoal_kiln").GetComponent<Smelter>().m_conversion
                            .Any(c => c.m_from.m_itemData.m_shared.m_name == sharedData.m_name))
                        {
                            groupName = "Woods";
                        }

                        if (sharedData.m_name == "$item_elderbark")
                        {
                            groupName = "Woods";
                        }

                        break;
                }

                if (!string.IsNullOrEmpty(groupName))
                {
                    AddItemToGroup(groupName, itemDrop);
                }

                if (sharedData != null)
                {
                    groupName = "All";
                    AddItemToGroup(groupName, itemDrop);
                }
            }
        }
    }

    private static void AddItemToGroup(string groupName, ItemDrop itemDrop)
    {
        // Check if the group exists, and if not, create it
        if (!GroupUtils.GroupExists(groupName))
        {
            HelheimHarmonizerPlugin.groups[groupName] = new HashSet<string>();
        }

        // Add the item to the group
        string prefabName = Utils.GetPrefabName(itemDrop.m_itemData.m_dropPrefab);
        if (HelheimHarmonizerPlugin.groups[groupName].Contains(prefabName)) return;
        HelheimHarmonizerPlugin.groups[groupName].Add(prefabName);
        HelheimHarmonizerPlugin.HelheimHarmonizerLogger.LogDebug(
            $"(CreatePredefinedGroups) Added {prefabName} to {groupName}");
    }

    public static Inventory GetRandysSlotInventory(Player __instance)
    {
        return (Inventory)HelheimHarmonizerPlugin.quickSlotsAssembly.GetType("EquipmentAndQuickSlots.PlayerExtensions")
            .GetMethod("GetQuickSlotInventory", BindingFlags.Public | BindingFlags.Static)
            .Invoke(null, new object[] { __instance });
    }
}