using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace CartTracking;

internal sealed class CartSnapshot
{
  public ZDOID Id;
  public Vector3 Position;
  public int PrefabHash;
}

internal static class CartSync
{
  private static float _timer;
  private static readonly List<CartSnapshot> Latest = new();
  private static readonly object Gate = new();
  private static int _lastLoggedCount = -1;
  private static bool _loggedProtocolMismatch;

  internal static IReadOnlyList<CartSnapshot> GetLatest()
  {
    lock (Gate)
      return Latest.ToArray();
  }

  internal static void Tick(float dt)
  {
    if (ZNet.instance == null || ZDOMan.instance == null)
      return;
    if (!ZNet.instance.IsServer())
      return;

    var interval = Mathf.Clamp(CartTrackingPlugin.SyncInterval.Value, 1f, 30f);
    _timer += dt;
    if (_timer < interval)
      return;
    _timer = 0f;

    try
    {
      BroadcastNow();
    }
    catch (System.Exception ex)
    {
      CartTrackingPlugin.Log.LogError($"Cart sync failed: {ex}");
    }
  }

  internal static void BroadcastNow()
  {
    if (ZNet.instance == null || !ZNet.instance.IsServer() || ZDOMan.instance == null)
      return;

    var carts = CollectCarts();
    var pkg = WritePackage(carts);

    lock (Gate)
    {
      Latest.Clear();
      Latest.AddRange(carts);
    }

    if (carts.Count != _lastLoggedCount)
    {
      _lastLoggedCount = carts.Count;
      CartTrackingPlugin.Log.LogInfo($"Broadcasting {carts.Count} cart pin(s) to clients.");
    }

    // Listen-server / singleplayer host draws pins locally.
    if (!ZNet.instance.IsDedicated() && Minimap.instance)
      CartPins.Apply(carts);

    ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, CartTrackingPlugin.RpcSync, pkg);
  }

  internal static void RequestFromServer()
  {
    if (ZNet.instance == null || ZRoutedRpc.instance == null)
      return;
    if (ZNet.instance.IsServer())
    {
      BroadcastNow();
      return;
    }
    var serverId = GetServerPeerIdSafe();
    if (serverId == 0L)
      return;
    ZRoutedRpc.instance.InvokeRoutedRPC(serverId, CartTrackingPlugin.RpcRequestSync);
  }

  internal static void HandleRequestSync(long sender)
  {
    if (ZNet.instance == null || !ZNet.instance.IsServer())
      return;
    var carts = CollectCarts();
    var pkg = WritePackage(carts);
    ZRoutedRpc.instance.InvokeRoutedRPC(sender, CartTrackingPlugin.RpcSync, pkg);
  }

  internal static void HandleSync(long sender, ZPackage pkg)
  {
    if (ZNet.instance == null)
      return;
    if (ZNet.instance.IsDedicated())
      return;
    if (pkg == null || pkg.Size() == 0)
      return;

    try
    {
      pkg.SetPos(0);
      if (!TryReadPackage(pkg, out var carts))
        return;

      lock (Gate)
      {
        Latest.Clear();
        Latest.AddRange(carts);
      }
      if (carts.Count != _lastLoggedCount)
      {
        _lastLoggedCount = carts.Count;
        CartTrackingPlugin.Log.LogInfo($"Received {carts.Count} cart pin(s) from server.");
      }
      CartPins.Apply(carts);
    }
    catch (System.Exception ex)
    {
      CartTrackingPlugin.Log.LogError($"Cart sync receive failed: {ex}");
    }
  }

  // Live GetServerPeerID is non-public; publicized libs lie. Call via reflection.
  private static long GetServerPeerIdSafe()
  {
    try
    {
      var method = AccessTools.Method(typeof(ZRoutedRpc), "GetServerPeerID");
      if (method != null && ZRoutedRpc.instance != null)
      {
        var result = method.Invoke(ZRoutedRpc.instance, null);
        if (result is long id)
          return id;
      }
    }
    catch (System.Exception ex)
    {
      CartTrackingPlugin.Log.LogWarning($"GetServerPeerID reflection failed: {ex.Message}");
    }

    try
    {
      var peer = ZNet.instance != null ? ZNet.instance.GetServerPeer() : null;
      if (peer != null)
      {
        var uidField = AccessTools.Field(peer.GetType(), "m_uid");
        if (uidField != null && uidField.GetValue(peer) is long uid)
          return uid;
      }
    }
    catch (System.Exception ex)
    {
      CartTrackingPlugin.Log.LogWarning($"GetServerPeer fallback failed: {ex.Message}");
    }

    return 0L;
  }

  private static List<CartSnapshot> CollectCarts()
  {
    var result = new List<CartSnapshot>();
    var zdoMan = ZDOMan.instance;
    if (zdoMan == null)
      return result;

    var seen = new HashSet<ZDOID>();

    void Consider(ZDO? zdo)
    {
      if (zdo == null || !zdo.IsValid())
        return;
      if (!seen.Add(zdo.m_uid))
        return;
      if (!CartCatalog.TryGet(zdo.GetPrefab(), out var info))
        return;

      var pos = zdo.GetPosition();
      result.Add(new CartSnapshot
      {
        Id = zdo.m_uid,
        Position = pos,
        PrefabHash = info.Hash,
      });
    }

    CollectFromObjectsById(zdoMan, Consider);

    if (result.Count == 0)
      CollectFromPrefabIterative(zdoMan, Consider);

    return result;
  }

  private static void CollectFromObjectsById(ZDOMan zdoMan, System.Action<ZDO?> consider)
  {
    try
    {
      var field = AccessTools.Field(typeof(ZDOMan), "m_objectsByID");
      var value = field?.GetValue(zdoMan);
      if (value is System.Collections.IDictionary dict)
      {
        foreach (var entry in dict.Values)
        {
          if (entry is ZDO zdo)
            consider(zdo);
        }
        return;
      }

      if (value is System.Collections.IEnumerable enumerable)
      {
        foreach (var entry in enumerable)
        {
          if (entry is ZDO zdo)
            consider(zdo);
          else if (entry is System.Collections.DictionaryEntry de && de.Value is ZDO zdo2)
            consider(zdo2);
        }
      }
    }
    catch (System.Exception ex)
    {
      CartTrackingPlugin.Log.LogWarning($"ZDO id scan failed: {ex.Message}");
    }
  }

  private static void CollectFromPrefabIterative(ZDOMan zdoMan, System.Action<ZDO?> consider)
  {
    var buffer = new List<ZDO>();
    foreach (var info in CartCatalog.Prefabs)
    {
      buffer.Clear();
      var index = 0;
      var guard = 0;
      while (!zdoMan.GetAllZDOsWithPrefabIterative(info.Prefab, buffer, ref index))
      {
        if (++guard > 100000)
        {
          CartTrackingPlugin.Log.LogWarning($"Prefab scan aborted for {info.Prefab}");
          break;
        }
      }

      foreach (var zdo in buffer)
        consider(zdo);
    }
  }

  private static ZPackage WritePackage(List<CartSnapshot> carts)
  {
    var pkg = new ZPackage();
    pkg.Write(CartTrackingPlugin.ProtocolVersion);
    pkg.Write(carts.Count);
    foreach (var cart in carts)
    {
      pkg.Write(cart.Id);
      pkg.Write(cart.Position);
      pkg.Write(cart.PrefabHash);
    }
    return pkg;
  }

  private static bool TryReadPackage(ZPackage pkg, out List<CartSnapshot> carts)
  {
    carts = new List<CartSnapshot>();
    var version = pkg.ReadInt();
    if (version != CartTrackingPlugin.ProtocolVersion)
    {
      NotifyProtocolMismatch(version);
      return false;
    }

    var count = pkg.ReadInt();
    if (count < 0 || count > 512)
    {
      CartTrackingPlugin.Log.LogWarning($"Cart sync rejected: unreasonable cart count {count}.");
      return false;
    }

    carts = new List<CartSnapshot>(count);
    for (var i = 0; i < count; i++)
    {
      carts.Add(new CartSnapshot
      {
        Id = pkg.ReadZDOID(),
        Position = pkg.ReadVector3(),
        PrefabHash = pkg.ReadInt(),
      });
    }
    return true;
  }

  private static void NotifyProtocolMismatch(int serverVersion)
  {
    if (_loggedProtocolMismatch)
      return;
    _loggedProtocolMismatch = true;

    var clientVersion = CartTrackingPlugin.ProtocolVersion;
    string playerMsg;
    if (serverVersion < clientVersion)
    {
      playerMsg =
        $"CartTracking: server mod is outdated (server protocol {serverVersion}, you have {clientVersion}). Ask the host to update CartTracking.";
    }
    else
    {
      playerMsg =
        $"CartTracking: your mod is outdated (server protocol {serverVersion}, you have {clientVersion}). Update CartTracking to match the server.";
    }

    CartTrackingPlugin.Log.LogWarning(playerMsg);
    try
    {
      if (MessageHud.instance)
        MessageHud.instance.ShowMessage(MessageHud.MessageType.Center, playerMsg);
    }
    catch (System.Exception ex)
    {
      CartTrackingPlugin.Log.LogWarning($"Could not show protocol mismatch HUD: {ex.Message}");
    }

    try
    {
      if (Chat.instance)
        Chat.instance.AddString("CartTracking", playerMsg, Talker.Type.Normal);
    }
    catch
    {
      // optional - HUD is enough
    }
  }
}
