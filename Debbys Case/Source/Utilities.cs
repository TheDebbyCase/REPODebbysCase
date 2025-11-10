using REPOLib.Modules;
using REPOLib.Objects;
using UnityEngine;
namespace REPODebbysCase.Utilities
{
    public class Utils
    {
        public bool RegisterPrefab(string path, GameObject prefab)
        {
            PrefabRefResponse prefabRefResponse = NetworkPrefabs.RegisterNetworkPrefabInternal($"{char.ToUpper(path[0])}{path[1..]}/{prefab.name}", prefab);
            PrefabRef prefabRef = prefabRefResponse.PrefabRef;
            if (prefabRefResponse.Result != 0 || prefabRef == null)
            {
                DebbysCase.instance.log.LogWarning($"Failed to register prefab \"{prefab.name}\", result: \"{prefabRefResponse}\"");
                return false;
            }
            REPOLib.Modules.Utilities.FixAudioMixerGroups(prefab);
            return true;
        }
    }
}