using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;

namespace CartTracking;

[BepInPlugin(GUID, NAME, VERSION)]
public class CartTrackingPlugin : BaseUnityPlugin
{
  public const string GUID = "matthb1.carttracking";
  public const string NAME = "CartTracking";
  public const string VERSION = "1.0.0";

  // Wire-format version for sync packages (independent of Thunderstore VERSION).
  public const int ProtocolVersion = 1;

  public const string RpcSync = "CartTracking.Sync";
  public const string RpcRequestSync = "CartTracking.RequestSync";

  internal static CartTrackingPlugin Instance = null!;
  internal static ConfigEntry<bool> ShowPins = null!;
  internal static ConfigEntry<float> SyncInterval = null!;

  internal static ManualLogSource Log = null!;

  private void Awake()
  {
    Instance = this;
    Log = Logger;
    ShowPins = Config.Bind("General", "ShowPins", true, "Show cart pins on the minimap and world map.");
    SyncInterval = Config.Bind("General", "SyncInterval", 3f, "Seconds between server cart-position syncs (1-30).");

    Logger.LogInfo($"{NAME} {VERSION} loaded - server syncs cart map pins.");
    new Harmony(GUID).PatchAll();
  }

  private void Update()
  {
    CartSync.Tick(UnityEngine.Time.deltaTime);
  }
}
