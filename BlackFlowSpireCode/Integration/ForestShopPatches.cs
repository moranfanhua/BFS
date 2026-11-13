using BlackFlowSpire.Ui;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Screens.Shops;

namespace BlackFlowSpire.Integration;

[HarmonyPatch(typeof(NStandardMerchantRoom), nameof(NStandardMerchantRoom._Ready))]
internal static class ForestShopReadyPatch
{
    private static void Postfix(NMerchantRoom __instance)
    {
        if (ForestRuntime.IsActive) ForestShopUi.Attach(__instance);
    }
}

[HarmonyPatch(typeof(NStandardMerchantRoom), nameof(NStandardMerchantRoom.OpenInventory))]
internal static class ForestShopOpenPatch
{
    private static bool Prefix(NMerchantRoom __instance)
    {
        if (!ForestRuntime.IsActive) return true;
        ForestShopUi.Attach(__instance);
        return false;
    }
}

[HarmonyPatch(typeof(NMerchantInventory), nameof(NMerchantInventory.Open))]
internal static class ForestShopNativeInventoryOpenPatch
{
    private static bool Prefix(NMerchantInventory __instance)
    {
        if (!ForestRuntime.IsActive || NMerchantRoom.Instance is not { } room
            || !ReferenceEquals(room.Inventory, __instance)) return true;
        ForestShopUi.Attach(room);
        return false;
    }
}

[HarmonyPatch(typeof(NMerchantRoom), "get_DefaultFocusedControl")]
internal static class ForestShopFocusPatch
{
    private static bool Prefix(NMerchantRoom __instance, ref Control __result)
    {
        if (!ForestRuntime.IsActive || ForestShopUi.Find(__instance) is not { } shop || !shop.IsNodeReady()) return true;
        __result = shop.DefaultFocus;
        return false;
    }
}

