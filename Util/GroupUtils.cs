using System.Collections.Generic;
using System.Linq;

namespace HelheimHarmonizer.Util;

public class GroupUtils
{
    // Get a list of all excluded groups for a container
    public static List<string> GetExcludedGroups(string container)
    {
        if (HelheimHarmonizerPlugin.yamlData.TryGetValue(container, out object containerData))
        {
            var containerInfo = containerData as Dictionary<object, object>;
            if (containerInfo != null && containerInfo.TryGetValue("exclude", out object excludeData))
            {
                var excludeList = excludeData as List<object>;
                if (excludeList != null)
                {
                    return excludeList.Where(excludeItem =>
                            HelheimHarmonizerPlugin.groups.ContainsKey(excludeItem.ToString()))
                        .Select(excludeItem => excludeItem.ToString()).ToList();
                }
            }
        }

        return new List<string>();
    }

    public static bool IsGroupDefined(string groupName)
    {
        if (HelheimHarmonizerPlugin.yamlData == null)
        {
            HelheimHarmonizerPlugin.HelheimHarmonizerLogger.LogError(
                "yamlData is null. Make sure to call DeserializeYamlFile() before using IsGroupDefined.");
            return false;
        }

        bool groupInYaml = false;

        if (HelheimHarmonizerPlugin.yamlData.ContainsKey("groups"))
        {
            var groupsData = HelheimHarmonizerPlugin.yamlData["groups"] as Dictionary<object, object>;
            if (groupsData != null)
            {
                groupInYaml = groupsData.ContainsKey(groupName);
            }
            else
            {
                HelheimHarmonizerPlugin.HelheimHarmonizerLogger.LogError(
                    "Unable to cast groupsData to Dictionary<object, object>.");
            }
        }

        // Check for the group in both yamlData and predefined groups
        return groupInYaml || HelheimHarmonizerPlugin.groups.ContainsKey(groupName);
    }


// Check if a group exists in the container data
    public static bool GroupExists(string groupName)
    {
        return HelheimHarmonizerPlugin.groups.ContainsKey(groupName);
    }

// Get a list of all groups in the container data
    public static List<string> GetAllGroups()
    {
        return HelheimHarmonizerPlugin.groups.Keys.ToList();
    }

// Get a list of all items in a group
    public static List<string> GetItemsInGroup(string groupName)
    {
        if (HelheimHarmonizerPlugin.groups.TryGetValue(groupName, out HashSet<string> groupPrefabs))
        {
            return groupPrefabs.ToList();
        }

        return new List<string>();
    }
}