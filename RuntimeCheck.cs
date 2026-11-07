using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Assets.Behaviour.Flow.Elements;
using Assets.Behaviour.UI;
using Assets.Behaviour.UI.ItemStore;
using Assets.Source.Flow.Elements;
using Assets.Source.Items;
using Assets.Source.Items.Type;
using Assets.Source.Player;
using Assets.Source.Util;
using BepInEx;
using HarmonyLib;
using LightJson;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace KingdomGuildhouseCleanup
{
    public sealed partial class Plugin
    {
        readonly List<string> checks = new List<string>();
        readonly List<string> runtimeErrors = new List<string>();
        void Check(bool ok, string label) { if (!ok) throw new InvalidOperationException(label); checks.Add(label); }
        void WatchErrors(string text, string trace, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) runtimeErrors.Add(text + "\n" + trace);
        }

        void ModelChecks(ItemType troop)
        {
            for (int channel = 0; channel < 8; channel++)
            {
                var contents = new ArmoryContents();
                var requester = new Guildhouse();
                var first = new InventoryItem(troop);
                var second = new InventoryItem(troop);
                Check(!contents.GetAvailableItemTypes(channel).Contains(troop), "Channel " + channel + ": unused slot hidden");
                contents.AddRange(channel, new[] { first, second });
                Check(contents.GetAvailableItemTypes(channel).Contains(troop) && contents.GetItemCount(channel, troop) == 2,
                    "Channel " + channel + ": adding troops shows slot");
                Check(ReferenceEquals(contents.ConsumeItem(requester, channel, troop), first) && contents.GetAvailableItemTypes(channel).Contains(troop),
                    "Channel " + channel + ": partially draining preserves slot and troop identity");
                Check(ReferenceEquals(contents.ConsumeItem(requester, channel, troop), second) && !contents.GetAvailableItemTypes(channel).Contains(troop),
                    "Channel " + channel + ": last troop leaves, slot hides without reload");
                var lists = (List<InventoryItem>[,])AccessTools.Field(typeof(ArmoryContents), "_contents").GetValue(contents);
                Check(lists[channel, troop.Ordinal] != null && lists[channel, troop.Ordinal].Count == 0,
                    "Channel " + channel + ": preexisting vanilla stale list safely hidden");
                contents.AddItem(channel, first);
                Check(contents.GetAvailableItemTypes(channel).Contains(troop), "Channel " + channel + ": refill restores slot");
                Check(!contents.GetAvailableItemTypes((channel + 1) % 8).Contains(troop), "Channel " + channel + ": other channel stays empty");
                var restored = new ArmoryContents(); restored.FromJson(JsonValue.Parse(contents.ToJson().ToString()));
                Check(restored.GetItemCount(channel, troop) == 1 && restored.GetAvailableItemTypes(channel).Contains(troop),
                    "Channel " + channel + ": native save round trip preserves occupied slot");
            }

            var equipment = Enumerable.Range(0, ItemType.Count).Select(ItemType.GetById).First(item => item is WeaponItemType);
            var storage = new ArmoryContents(); var armory = new Armory();
            storage.AddItem(0, new InventoryItem(equipment)); storage.ConsumeItem(armory, 0, equipment);
            Check(!storage.GetAvailableItemTypes(0).Contains(equipment), "Empty equipment Armory entry is hidden");
            storage.AddItem(0, new InventoryItem(equipment));
            Check(storage.GetAvailableItemTypes(0).Contains(equipment), "Refilling equipment restores Armory slot");
            storage.ConsumeItem(armory, 0, equipment);
            Check(!storage.GetAvailableItemTypes(0).Contains(equipment), "Draining equipment hides Armory slot again");
            var conscript = Enumerable.Range(0, ItemType.Count).Select(ItemType.GetById).First(item => item is HumanItemType && !(item is AdventurerItemType));
            storage.AddItem(0, new InventoryItem(conscript)); storage.ConsumeItem(armory, 0, conscript);
            Check(!storage.GetAvailableItemTypes(0).Contains(conscript), "Empty conscript slot is hidden too");
            storage.AddItem(0, new InventoryItem(troop)); storage.ConsumeItem(armory, 0, troop);
            hooks.UnpatchSelf();
            Check(storage.GetAvailableItemTypes(0).Contains(troop), "Removing plugin restores native stale-entry behavior without data changes");
            Check(storage.GetAvailableItemTypes(0).Contains(equipment), "Removing plugin restores native empty equipment behavior");
            var saved = storage.ToJson(); var loaded = new ArmoryContents(); loaded.FromJson(JsonValue.Parse(saved.ToString()));
            Check(!loaded.GetAvailableItemTypes(0).Contains(troop), "Native reload without plugin accepts save and omits empty entry");
            Check(!loaded.GetAvailableItemTypes(0).Contains(equipment), "Native reload without plugin accepts save and omits empty equipment entry");
            hooks.CreateClassProcessor(typeof(AvailableItems)).Patch();
            Check(!storage.GetAvailableItemTypes(0).Contains(troop), "Reapplying fix hides existing empty slot immediately");
            Check(!storage.GetAvailableItemTypes(0).Contains(equipment), "Reapplying fix hides existing empty equipment slot immediately");
        }

        IEnumerator RuntimeCheck()
        {
            Application.logMessageReceived += WatchErrors;
            yield return new WaitForSecondsRealtime(5);
            string directory = Path.Combine(Paths.GameRootPath, "_Mods", "GuildhouseCleanup", "Verification");
            Directory.CreateDirectory(directory);
            string error = null; ItemType troop = null; Guildhouse guildhouse = null; Armory armoryBuilding = null; ArmoryPopup popup = null;
            ArmoryItemRow stableRow = null;
            try
            {
                Check(GamePlayer.Current == null && !GameUI.Instance, "No user save or normal kingdom loaded");
                var player = GamePlayer.CreateNewPlayer();
                troop = ItemType.Get("adventurer");
                Check(troop is AdventurerItemType, "Native adventurer definition available");
                ModelChecks(troop);
                player.AddTech("armory_unlock_channels"); player.AddTech("guildhouse"); player.AddTech("armory");
                player.ActiveFlow.Elements.Clear(); player.ActiveFlow.Pipes.Clear(); player.ActiveFlow.Decorations.Clear();
                guildhouse = new Guildhouse { Channel = 0 }; guildhouse.OnCreateElement();
                armoryBuilding = new Armory { Channel = 0, Position = new Vector2Int(5, 5) }; armoryBuilding.OnCreateElement();
                player.ActiveFlow.Elements.Add(guildhouse);
                player.ActiveFlow.Elements.Add(armoryBuilding);
                SceneManager.LoadScene("Game");
            }
            catch (Exception ex) { error = ex.ToString(); }
            yield return new WaitForSecondsRealtime(4);
            if (error == null) try
            {
                Check(guildhouse.World, "Native Guild House world created");
                var world = guildhouse.World.GetComponent<WorldFlowArmory>();
                popup = GameUI.Instance.ArmoryPopup; popup.SetElement(world); GameUI.Instance.TogglePopupMenu(popup, true);
                Check(popup.isActiveAndEnabled && !VisibleTypes(popup).Any(), "Opening empty Guild House shows no troop slots");
                Check(popup.GetComponentsInChildren<ArmoryChannelSelect>(true).Length == 8, "All eight color selectors remain available");
                Check(popup.GetComponentsInChildren<ArmoryItemRow>().Length == 1, "Input option remains available with zero troops");
                GamePlayer.Current.Armory.AddItem(0, new InventoryItem(troop));
            }
            catch (Exception ex) { error = ex.ToString(); }
            yield return new WaitForSecondsRealtime(.4f);
            if (error == null) try
            {
                Check(VisibleTypes(popup).SequenceEqual(new[] { troop }), "Adding troop while menu open automatically reveals slot");
                Capture(Path.Combine(directory, "guild-house-occupied.png"));
                stableRow = popup.GetComponentsInChildren<ArmoryItemRow>().First(row => RowItem.GetValue(row) == troop);
                GamePlayer.Current.Armory.AddItem(0, new InventoryItem(troop));
            }
            catch (Exception ex) { error = ex.ToString(); }
            yield return new WaitForSecondsRealtime(.4f);
            if (error == null) try
            {
                Check(stableRow && stableRow.gameObject.activeInHierarchy, "Count-only change keeps existing row for stable interaction");
                var contents = GamePlayer.Current.Armory;
                Check(contents.ConsumeItem(guildhouse, 0, troop) != null && contents.ConsumeItem(guildhouse, 0, troop) != null,
                    "All troops drain successfully while menu is open");
            }
            catch (Exception ex) { error = ex.ToString(); }
            yield return new WaitForSecondsRealtime(.4f);
            if (error == null) try
            {
                Check(!VisibleTypes(popup).Any(), "Last troop removed while menu open automatically hides slot");
                Capture(Path.Combine(directory, "guild-house-empty.png"));
                GamePlayer.Current.Armory.AddItem(1, new InventoryItem(troop));
                popup.ChannelSelected(1);
                Check(VisibleTypes(popup).SequenceEqual(new[] { troop }), "Switching color shows only that channel's troops");
                popup.ItemSelected(troop, false);
                Check(guildhouse.OutputType == troop && guildhouse.Channel == 1, "Selecting troop preserves native output configuration");
                GameUI.Instance.TogglePopupMenu(popup, false);
                guildhouse.Update(1f);
                Check(GamePlayer.Current.Armory.GetItemCount(1, troop) == 0 && guildhouse.Outputs[0].HasBuffer,
                    "Native output withdraws final troop into output buffer");
                var sent = guildhouse.Outputs[0].ConsumeItem();
                Check(sent != null && sent.Type == troop, "Withdrawn troop remains intact for downstream stockpile");
                popup.SetElement(guildhouse.World.GetComponent<WorldFlowArmory>()); GameUI.Instance.TogglePopupMenu(popup, true);
                Check(!VisibleTypes(popup).Any() && guildhouse.OutputType == troop, "Empty slot hides while output stays configured");
                GamePlayer.Current.Armory.AddItem(1, sent);
            }
            catch (Exception ex) { error = ex.ToString(); }
            yield return new WaitForSecondsRealtime(.4f);
            if (error == null) try
            {
                Check(VisibleTypes(popup).SequenceEqual(new[] { troop }) && guildhouse.OutputType == troop,
                    "Refill reveals previously hidden selected output automatically");
                var row = popup.GetComponentsInChildren<ArmoryItemRow>().First(r => RowItem.GetValue(r) == troop);
                Check(((GameObject)AccessTools.Field(typeof(ArmoryItemRow), "_selected").GetValue(row)).activeSelf,
                    "Refilled output row retains native selection highlight");
                popup.ChannelSelected(0);
                Check(!VisibleTypes(popup).Any(), "Switching back hides empty original channel slot");
                GameUI.Instance.TogglePopupMenu(popup, false);

                Check(armoryBuilding.World, "Native Armory world created");
                var armoryWorld = armoryBuilding.World.GetComponent<WorldFlowArmory>();
                popup.SetElement(armoryWorld); GameUI.Instance.TogglePopupMenu(popup, true);
                Check(popup.isActiveAndEnabled && !VisibleTypes(popup).Any(), "Opening empty Armory shows no equipment slots");
                var sword = Enumerable.Range(0, ItemType.Count).Select(ItemType.GetById).First(item => item is WeaponItemType);
                GamePlayer.Current.Armory.AddItem(0, new InventoryItem(sword));
            }
            catch (Exception ex) { error = ex.ToString(); }
            yield return new WaitForSecondsRealtime(.4f);
            if (error == null) try
            {
                var sword = Enumerable.Range(0, ItemType.Count).Select(ItemType.GetById).First(item => item is WeaponItemType);
                Check(VisibleTypes(popup).SequenceEqual(new[] { sword }), "Adding weapon while Armory open automatically reveals slot");
                Capture(Path.Combine(directory, "armory-occupied.png"));
                GamePlayer.Current.Armory.ConsumeItem(armoryBuilding, 0, sword);
            }
            catch (Exception ex) { error = ex.ToString(); }
            yield return new WaitForSecondsRealtime(.4f);
            if (error == null) try
            {
                Check(!VisibleTypes(popup).Any(), "Withdrawing weapon while Armory open automatically hides slot");
                Capture(Path.Combine(directory, "armory-empty.png"));
                GameUI.Instance.TogglePopupMenu(popup, false);
                Check(runtimeErrors.Count == 0, "Unity reported no runtime errors");
            }
            catch (Exception ex) { error = ex.ToString(); }

            string digest;
            using (var sha = SHA256.Create()) digest = BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(typeof(Plugin).Assembly.Location))).Replace("-", "").ToLowerInvariant();
            var names = new JsonArray(); foreach (var check in checks) names.Add(check);
            var errors = new JsonArray(); foreach (var issue in runtimeErrors) errors.Add(issue);
            var report = new JsonObject {
                { "passed", error == null }, { "version", Version }, { "count", checks.Count }, { "dllSha256", digest },
                { "gameVersion", Application.version }, { "unityVersion", Application.unityVersion },
                { "bepInExVersion", typeof(BaseUnityPlugin).Assembly.GetName().Version.ToString() },
                { "error", error }, { "runtimeErrors", errors }, { "checks", names }
            };
            File.WriteAllText(Path.Combine(directory, "runtime-tests.json"), report.ToString());
            if (error != null) Logger.LogError(error); else Logger.LogInfo("Guild House Cleanup: " + checks.Count + " checks passed.");
            Application.Quit(error == null ? 0 : 1);
        }

        static void Capture(string path)
        {
            // Rendering to a texture works even when the diagnostic window is hidden.
            Canvas.ForceUpdateCanvases();
            var camera = Camera.main;
            var overlays = FindObjectsByType<Canvas>(FindObjectsSortMode.None)
                .Where(c => c.isRootCanvas && c.renderMode == RenderMode.ScreenSpaceOverlay).ToArray();
            var cameras = overlays.Select(c => c.worldCamera).ToArray();
            var distances = overlays.Select(c => c.planeDistance).ToArray();
            var orders = overlays.Select(c => c.sortingOrder).ToArray();
            var target = RenderTexture.GetTemporary(Screen.width, Screen.height, 24);
            var previous = RenderTexture.active; var oldTarget = camera.targetTexture; int oldMask = camera.cullingMask;
            var texture = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
            try
            {
                foreach (var canvas in overlays) { canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 10; canvas.sortingOrder = 30000; }
                camera.cullingMask = -1; camera.targetTexture = target;
                Canvas.ForceUpdateCanvases(); camera.Render();
                RenderTexture.active = target; texture.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0); texture.Apply();
                File.WriteAllBytes(path, ImageConversion.EncodeToPNG(texture));
            }
            finally
            {
                camera.targetTexture = oldTarget; camera.cullingMask = oldMask;
                for (int i = 0; i < overlays.Length; i++) { overlays[i].renderMode = RenderMode.ScreenSpaceOverlay; overlays[i].worldCamera = cameras[i]; overlays[i].planeDistance = distances[i]; overlays[i].sortingOrder = orders[i]; }
                Canvas.ForceUpdateCanvases();
                RenderTexture.active = previous; RenderTexture.ReleaseTemporary(target); Destroy(texture);
            }
        }

        void InstallDiagnostics()
        {
            var safety = new Harmony(Id + ".diagnostics");
            foreach (var patch in new[] { typeof(NoSave), typeof(NoAutosave), typeof(NoLoad), typeof(NoAchievement), typeof(Offline) })
                safety.CreateClassProcessor(patch).Patch();
        }
        [HarmonyPatch(typeof(SaveGame), nameof(SaveGame.Store))] static class NoSave { static bool Prefix() => false; }
        [HarmonyPatch(typeof(SaveGame), nameof(SaveGame.StoreAutosaveState))] static class NoAutosave { static bool Prefix() => false; }
        [HarmonyPatch(typeof(SaveGame), nameof(SaveGame.LoadLatestSave))] static class NoLoad { static bool Prefix(ref bool __result) { __result = false; return false; } }
        [HarmonyPatch(typeof(SteamAchievement), nameof(SteamAchievement.Trigger))] static class NoAchievement { static bool Prefix() => false; }
        [HarmonyPatch(typeof(SteamManager), nameof(SteamManager.Initialized), MethodType.Getter)] static class Offline { static bool Prefix(ref bool __result) { __result = false; return false; } }
    }
}
