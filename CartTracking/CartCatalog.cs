using System.Collections.Generic;

namespace CartTracking;

internal static class CartCatalog
{
  internal readonly struct PrefabInfo
  {
    public readonly string Prefab;
    public readonly string Label;
    public readonly int Hash;

    public PrefabInfo(string prefab, string label)
    {
      Prefab = prefab;
      Label = label;
      Hash = prefab.GetStableHashCode();
    }
  }

  // Vanilla cart prefab (Vagon component on GameObject "Cart").
  internal static readonly PrefabInfo[] Prefabs =
  {
    new("Cart", "Cart"),
  };

  private static readonly Dictionary<int, PrefabInfo> ByHash = BuildLookup();

  private static Dictionary<int, PrefabInfo> BuildLookup()
  {
    var map = new Dictionary<int, PrefabInfo>();
    foreach (var info in Prefabs)
      map[info.Hash] = info;
    return map;
  }

  internal static bool TryGet(int prefabHash, out PrefabInfo info) =>
    ByHash.TryGetValue(prefabHash, out info);
}
