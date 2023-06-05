using System.Collections.Generic;
using System.IO;
using YamlDotNet.Serialization;

namespace HelheimHarmonizer.Util;

public static class YamlUtils
{
    internal static void ReadYaml(string yamlInput)
    {
        var deserializer = new DeserializerBuilder().Build();
        HelheimHarmonizerPlugin.yamlData = deserializer.Deserialize<Dictionary<string, object>>(yamlInput);
        HelheimHarmonizerPlugin.HelheimHarmonizerLogger.LogDebug($"yamlData:\n{yamlInput}");
    }

    internal static void ParseGroups()
    {
        // Check if the groups dictionary has been initialized
        if (HelheimHarmonizerPlugin.groups == null) HelheimHarmonizerPlugin.groups = new Dictionary<string, HashSet<string>>();

        if (HelheimHarmonizerPlugin.yamlData.TryGetValue("groups", out object groupData))
        {
            var groupDict = groupData as Dictionary<object, object>;
            if (groupDict != null)
            {
                foreach (var group in groupDict)
                {
                    string groupName = group.Key.ToString();
                    if (group.Value is List<object> prefabs)
                    {
                        HashSet<string> prefabNames = new HashSet<string>();
                        foreach (var prefab in prefabs)
                        {
                            prefabNames.Add(prefab.ToString());
                        }

                        HelheimHarmonizerPlugin.groups[groupName] = prefabNames;
                    }
                }
            }
        }
    }

    public static void WriteYaml(string filePath)
    {
        var serializer = new SerializerBuilder().Build();
        using var output = new StreamWriter(filePath);
        serializer.Serialize(output, HelheimHarmonizerPlugin.yamlData);

        // Serialize the data again to YAML format
        string serializedData = serializer.Serialize(HelheimHarmonizerPlugin.yamlData);

        // Append the serialized YAML data to the file
        File.AppendAllText(filePath, serializedData);
    }
}