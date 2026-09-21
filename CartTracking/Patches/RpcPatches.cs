using System;
using HarmonyLib;
using UnityEngine;

namespace CartTracking.Patches;

[HarmonyPatch(typeof(Game), nameof(Game.Start))]
internal static class GameStartPatch
{
  private static void Prefix()
  {
    ZRoutedRpc.instance.Register(CartTrackingPlugin.RpcRequestSync, new Action<long>(CartSync.HandleRequestSync));
    ZRoutedRpc.instance.Register<ZPackage>(CartTrackingPlugin.RpcSync, CartSync.HandleSync);
  }
}

[HarmonyPatch(typeof(Game), nameof(Game.Logout))]
internal static class GameLogoutPatch
{
  private static void Prefix() => CartPins.Clear();
}

[HarmonyPatch(typeof(Minimap), nameof(Minimap.OnDestroy))]
internal static class MinimapDestroyPatch
{
  private static void Postfix() => CartPins.Clear();
}

[HarmonyPatch(typeof(Minimap), nameof(Minimap.Awake))]
internal static class MinimapAwakePatch
{
  private static void Postfix() => CartSync.RequestFromServer();
}

[HarmonyPatch(typeof(Minimap), "UpdatePins")]
internal static class MinimapUpdatePinsPatch
{
  private static void Postfix() => CartPins.UpdateDrawnMarkers();
}

[HarmonyPatch(typeof(Minimap), "UpdateMap")]
internal static class MinimapUpdateRefreshPatch
{
  private static float _next;
  private static void Postfix()
  {
    if (Time.time < _next)
      return;
    _next = Time.time + 5f;
    var latest = CartSync.GetLatest();
    if (latest.Count > 0)
      CartPins.Apply(latest);
  }
}
