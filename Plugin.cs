using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Assets.Behaviour.UI.ItemStore;
using Assets.Source.Flow.Elements;
using Assets.Source.Items;
using Assets.Source.Items.Type;
using Assets.Source.Player;
using BepInEx;
using HarmonyLib;
using UnityEngine;

namespace KingdomGuildhouseCleanup
{
    [BepInPlugin(Id, "Guild House & Armory Cleanup", Version)]
    public sealed partial class Plugin : BaseUnityPlugin
    {
        public const string Id = "urakka.kingdominc.guildhousecleanup", Version = "1.1.0";
        internal static bool Diagnostic => Environment.GetCommandLineArgs().Contains("-guildhouse-cleanup-smoke");
        static readonly MethodInfo RebuildItems = AccessTools.Method(typeof(ArmoryPopup), "_updateItems");
        static readonly FieldInfo RowItem = AccessTools.Field(typeof(ArmoryItemRow), "_item");
        internal Harmony hooks;
        float nextRefresh;

        void Awake()
        {
            hooks = new Harmony(Id);
            hooks.CreateClassProcessor(typeof(AvailableItems)).Patch();
            if (Diagnostic) { InstallDiagnostics(); Application.runInBackground = true; StartCoroutine(RuntimeCheck()); }
            Logger.LogInfo("Guild House & Armory Cleanup " + Version + ": empty item and troop slots hidden; occupied slots return automatically.");
        }

        void Update()
        {
            if (Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + .2f;
            RefreshOpenPopup();
        }

        internal static ItemType[] VisibleTypes(ArmoryPopup popup) => popup.GetComponentsInChildren<ArmoryItemRow>()
            .Where(row => row.gameObject.activeInHierarchy)
            .Select(row => (ItemType)RowItem.GetValue(row)).Where(item => item != null).ToArray();

        internal static void RefreshOpenPopup()
        {
            var popup = ArmoryPopup.Instance;
            if (!popup || !popup.isActiveAndEnabled || GamePlayer.Current == null || !popup.Element ||
                !(popup.Element.Element is Armory armory)) return;

            var expected = GamePlayer.Current.Armory.GetAvailableItemTypes(armory.Channel)
                .Where(armory.CanAcceptItem).OrderBy(item => item.Ordinal);
            // Rebuild only when an item type appears/disappears, preserving scroll and clicks otherwise.
            if (!VisibleTypes(popup).SequenceEqual(expected)) RebuildItems.Invoke(popup, null);
        }

        [HarmonyPatch(typeof(ArmoryContents), nameof(ArmoryContents.GetAvailableItemTypes))]
        static class AvailableItems
        {
            static void Postfix(ArmoryContents __instance, int channel, ref IEnumerable<ItemType> __result)
            {
                __result = OccupiedItems(__instance, channel, __result);
            }

            static IEnumerable<ItemType> OccupiedItems(ArmoryContents contents, int channel, IEnumerable<ItemType> items)
            {
                foreach (var item in items)
                    if (contents.GetItemCount(channel, item) > 0)
                        yield return item;
            }
        }

        void OnDestroy() => hooks?.UnpatchSelf();
    }
}
