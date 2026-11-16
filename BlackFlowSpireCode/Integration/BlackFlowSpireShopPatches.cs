using BlackFlowSpire.Ui;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Screens.Shops;

namespace BlackFlowSpire.Integration;

[HarmonyPatch(typeof(NStandardMerchantRoom), nameof(NStandardMerchantRoom._Ready))]
internal static class BlackFlowSpireShopReadyPatch
{
    private static void Postfix(NMerchantRoom __instance)
    {
        if (BlackFlowSpireRuntime.IsActive) BlackFlowSpireShopUi.Attach(__instance);
    }
}

[HarmonyPatch(typeof(NStandardMerchantRoom), nameof(NStandardMerchantRoom.OpenInventory))]
internal static class BlackFlowSpireShopOpenPatch
{
    private static bool Prefix(NMerchantRoom __instance)
    {
        if (!BlackFlowSpireRuntime.IsActive) return true;
        BlackFlowSpireShopUi.Attach(__instance);
        return false;
    }
}

[HarmonyPatch(typeof(NMerchantInventory), nameof(NMerchantInventory.Open))]
internal static class BlackFlowSpireShopNativeInventoryOpenPatch
{
    private static bool Prefix(NMerchantInventory __instance)
    {
        if (!BlackFlowSpireRuntime.IsActive || NMerchantRoom.Instance is not { } room
            || !ReferenceEquals(room.Inventory, __instance)) return true;
        BlackFlowSpireShopUi.Attach(room);
        return false;
    }
}

[HarmonyPatch(typeof(NMerchantRoom), "get_DefaultFocusedControl")]
internal static class BlackFlowSpireShopFocusPatch
{
    private static bool Prefix(NMerchantRoom __instance, ref Control __result)
    {
        if (!BlackFlowSpireRuntime.IsActive || BlackFlowSpireShopUi.Find(__instance) is not { } shop || !shop.IsNodeReady()) return true;
        __result = shop.DefaultFocus;
        return false;
    }
}

