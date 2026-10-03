using HarmonyLib;
using Helpers;
using MCM.Abstractions.Attributes;
using MCM.Abstractions.Attributes.v2;
using MCM.Abstractions.Base.Global;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.Inventory;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.ViewModelCollection.Inventory;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;

namespace SaleLoot
{
    // ============================================================
    //  MCM SETTINGS
    // ============================================================
    public class SaleLootSettings : AttributeGlobalSettings<SaleLootSettings>
    {
        public override string Id => "SaleLoot.Settings";
        public override string DisplayName => new TextObject("{=SaleLoot_ModName}Sale Loot").ToString();
        public override string FolderName => "SaleLoot";
        public override string FormatType => "json2";

        private int _maxWeaponTier = 6;

        [SettingPropertyGroup("{=SaleLoot_Group_Tiers}Tiers", GroupOrder = 0)]
        [SettingPropertyInteger("{=SaleLoot_MaxWeaponTier_Name}Max weapon tier", 1, 6, "0", Order = 0,
            RequireRestart = false,
            HintText = "{=SaleLoot_MaxWeaponTier_Hint}Weapons of this tier and below will be sold. Weapons above will be kept.")]
        public int MaxWeaponTier
        {
            get { try { if (_maxWeaponTier < 1) return 1; if (_maxWeaponTier > 6) return 6; return _maxWeaponTier; } catch { return 6; } }
            set { try { int v = value; if (v < 1) v = 1; if (v > 6) v = 6; if (_maxWeaponTier != v) { _maxWeaponTier = v; OnPropertyChanged(nameof(MaxWeaponTier)); } } catch { } }
        }

        private int _maxAmmoTier = 6;

        [SettingPropertyGroup("{=SaleLoot_Group_Tiers}Tiers", GroupOrder = 0)]
        [SettingPropertyInteger("{=SaleLoot_MaxAmmoTier_Name}Max ammo tier", 1, 6, "0", Order = 1,
            RequireRestart = false,
            HintText = "{=SaleLoot_MaxAmmoTier_Hint}Bows, crossbows, arrows, bolts, thrown weapons and shields of this tier and below will be sold.")]
        public int MaxAmmoTier
        {
            get { try { if (_maxAmmoTier < 1) return 1; if (_maxAmmoTier > 6) return 6; return _maxAmmoTier; } catch { return 6; } }
            set { try { int v = value; if (v < 1) v = 1; if (v > 6) v = 6; if (_maxAmmoTier != v) { _maxAmmoTier = v; OnPropertyChanged(nameof(MaxAmmoTier)); } } catch { } }
        }

        private int _maxArmorTier = 6;

        [SettingPropertyGroup("{=SaleLoot_Group_Tiers}Tiers", GroupOrder = 0)]
        [SettingPropertyInteger("{=SaleLoot_MaxArmorTier_Name}Max armor tier", 1, 6, "0", Order = 2,
            RequireRestart = false,
            HintText = "{=SaleLoot_MaxArmorTier_Hint}Armor of this tier and below will be sold. Armor above will be kept.")]
        public int MaxArmorTier
        {
            get { try { if (_maxArmorTier < 1) return 1; if (_maxArmorTier > 6) return 6; return _maxArmorTier; } catch { return 6; } }
            set { try { int v = value; if (v < 1) v = 1; if (v > 6) v = 6; if (_maxArmorTier != v) { _maxArmorTier = v; OnPropertyChanged(nameof(MaxArmorTier)); } } catch { } }
        }

        private int _maxHorseArmorTier = 6;

        [SettingPropertyGroup("{=SaleLoot_Group_Tiers}Tiers", GroupOrder = 0)]
        [SettingPropertyInteger("{=SaleLoot_MaxHorseArmorTier_Name}Max horse armor tier", 1, 6, "0", Order = 3,
            RequireRestart = false,
            HintText = "{=SaleLoot_MaxHorseArmorTier_Hint}Horse armor / harness of this tier and below will be sold. Above will be kept.")]
        public int MaxHorseArmorTier
        {
            get { try { if (_maxHorseArmorTier < 1) return 1; if (_maxHorseArmorTier > 6) return 6; return _maxHorseArmorTier; } catch { return 6; } }
            set { try { int v = value; if (v < 1) v = 1; if (v > 6) v = 6; if (_maxHorseArmorTier != v) { _maxHorseArmorTier = v; OnPropertyChanged(nameof(MaxHorseArmorTier)); } } catch { } }
        }

        private bool _showMessages = true;

        [SettingPropertyGroup("{=SaleLoot_Group_Tiers}Tiers", GroupOrder = 0)]
        [SettingPropertyBool("{=SaleLoot_ShowMessages_Name}Chat Messages", Order = 10,
            RequireRestart = false,
            HintText = "{=SaleLoot_ShowMessages_Hint}Show SaleLoot results in the in-game chat.")]
        public bool ShowMessages
        {
            get { try { return _showMessages; } catch { return true; } }
            set { try { if (_showMessages != value) { _showMessages = value; OnPropertyChanged(nameof(ShowMessages)); } } catch { } }
        }
    }

    // ============================================================
    //  SUBMODULE
    // ============================================================
    public class SaleLootSubModule : MBSubModuleBase
    {
        private Harmony _harmony;

        protected override void OnSubModuleLoad()
        {
            base.OnSubModuleLoad();
            try
            {
                _harmony = new Harmony("com.saleloot.patch");
                _harmony.PatchAll(typeof(SaleLootSubModule).Assembly);
                InformationManager.DisplayMessage(new InformationMessage("Sale Loot loaded.", Colors.Yellow));
            }
            catch (Exception ex)
            {
                try { InformationManager.DisplayMessage(new InformationMessage("Sale Loot load error: " + ex.Message, Colors.Red)); } catch { }
            }
        }

        protected override void OnSubModuleUnloaded()
        {
            base.OnSubModuleUnloaded();
            try { _harmony?.UnpatchAll("com.saleloot.patch"); } catch { }
        }

        protected override void OnGameStart(Game game, IGameStarter starterObject)
        {
            base.OnGameStart(game, starterObject);
            try
            {
                if (game.GameType is Campaign)
                {
                    var starter = (CampaignGameStarter)starterObject;
                    starter.AddBehavior(new SaleLootBehavior());
                }
            }
            catch { }
        }
    }

    // ============================================================
    //  BEHAVIOR  (menu hook)
    // ============================================================
    public class SaleLootBehavior : CampaignBehaviorBase
    {
        public override void RegisterEvents()
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
        }

        public override void SyncData(IDataStore dataStore) { }

        private void OnSessionLaunched(CampaignGameStarter starter)
        {
            try
            {
                string text = new TextObject("{=SaleLoot_MenuOption}Sell Loot").ToString();
                starter.AddGameMenuOption(
                    "town",
                    "saleloot_sell_option",
                    text,
                    new GameMenuOption.OnConditionDelegate(OnMenuCondition),
                    new GameMenuOption.OnConsequenceDelegate(OnMenuConsequence),
                    false, 4, false, null);
            }
            catch { }
        }

        private bool OnMenuCondition(MenuCallbackArgs args)
        {
            try
            {
                var s = Settlement.CurrentSettlement;
                if (s == null || !s.IsTown || s.Town == null) return false;
                args.optionLeaveType = GameMenuOption.LeaveType.Trade;
                return true;
            }
            catch { return false; }
        }

        private void OnMenuConsequence(MenuCallbackArgs args)
        {
            try { SaleLootLogic.OpenTradeAndStage(Settlement.CurrentSettlement); }
            catch { }
        }
    }

    // ============================================================
    //  CORE LOGIC
    // ============================================================
    internal enum LootCategory { None, Weapon, Ammo, Armor, HorseArmor }

    internal static class SaleLootLogic
    {
        // ---- Pending staging request (set by menu, consumed by patch) ----
        private static bool _pendingStage = false;
        private static Settlement _pendingTown = null;

        // Cached reflection handle to SPInventoryVM.ProcessSellItem(SPItemVM, bool).
        private static MethodInfo _processSellItemMethod = null;

        public static void OpenTradeAndStage(Settlement town)
        {
            if (town == null || !town.IsTown || town.Town == null) return;
            _pendingStage = true;
            _pendingTown = town;
            try
            {
                InventoryScreenHelper.ActivateTradeWithCurrentSettlement();
            }
            catch (Exception ex)
            {
                _pendingStage = false;
                _pendingTown = null;
                Msg("SaleLoot error: " + ex.Message, SaleLootSettings.Instance);
            }
        }

        // Called by the Harmony postfix on SPInventoryVM's constructor.
        internal static void OnInventoryVMCreated(SPInventoryVM vm)
        {
            if (!_pendingStage) return;
            var town = _pendingTown;
            _pendingStage = false;
            _pendingTown = null;
            try { ApplyStaging(vm, town); }
            catch (Exception ex) { Msg("SaleLoot stage error: " + ex.Message, SaleLootSettings.Instance); }
        }

        // --------------------------------------------------------
        //  STAGING
        // --------------------------------------------------------
        private static void ApplyStaging(SPInventoryVM vm, Settlement town)
        {
            if (vm == null || town == null || town.Town == null) return;

            var settings = SaleLootSettings.Instance;
            if (settings == null) return;

            // Merchant gold as shown in the trade window.
            int townGold = 0;
            try { townGold = vm.LeftInventoryOwnerGold; } catch { }
            if (townGold <= 0)
            {
                try { townGold = town.Town.Gold; } catch { townGold = 0; }
            }
            if (townGold <= 0)
            {
                Msg("{=SaleLoot_Msg_NoGold}The merchant has no gold to buy anything.", settings);
                return;
            }

            var lockedKeys = GetLockedKeys();

            // Resolve the private SPInventoryVM.ProcessSellItem method once.
            if (_processSellItemMethod == null)
            {
                try
                {
                    _processSellItemMethod = typeof(SPInventoryVM).GetMethod(
                        "ProcessSellItem",
                        BindingFlags.Instance | BindingFlags.NonPublic);
                }
                catch { _processSellItemMethod = null; }
            }

            if (_processSellItemMethod == null)
            {
                Msg("SaleLoot: could not resolve SPInventoryVM.ProcessSellItem", settings);
                return;
            }

            // Collect candidates from the player's side of the trade window.
            var candidates = new List<Candidate>();
            foreach (var itemVM in vm.RightItemListVM)
            {
                if (itemVM == null) continue;
                if (!itemVM.IsTransferable) continue;
                if (itemVM.IsLocked) continue;

                var eq = itemVM.ItemRosterElement.EquipmentElement;
                var item = eq.Item;
                if (item == null) continue;
                if (item.NotMerchandise) continue;

                var cat = GetCategory(item);
                if (cat == LootCategory.None) continue;

                int maxTier = GetMaxTier(cat, settings);
                int itemTier = GetTierForUi(item);
                if (itemTier > maxTier) continue;

                if (IsLocked(lockedKeys, itemVM.ItemRosterElement)) continue;

                int price = 0;
                try { price = itemVM.ItemCost; } catch { price = 0; }
                if (price <= 0) continue;

                candidates.Add(new Candidate { VM = itemVM, Price = price });
            }

            if (candidates.Count == 0)
            {
                Msg("{=SaleLoot_Msg_Nothing}No suitable loot to sell.", settings);
                return;
            }

            candidates.Sort((a, b) => a.Price.CompareTo(b.Price));

            long stagedValue = 0;
            int stagedUnits = 0;
            int failures = 0;

            foreach (var c in candidates)
            {
                if (stagedValue >= townGold) break;
                if (c.VM == null) continue;

                int available = 0;
                try { available = c.VM.ItemCount; } catch { available = 0; }
                if (available <= 0) continue;

                long remainingGold = (long)townGold - stagedValue;
                int maxAffordable = (int)Math.Min((long)available, remainingGold / c.Price);
                if (maxAffordable <= 0) continue;

                try
                {
                    c.VM.TransactionCount = maxAffordable;

                    // Call SPInventoryVM.ProcessSellItem(item, cameFromTradeData:true)
                    // on the CURRENT vm instance — this is exactly the code path
                    // the game uses when a player clicks the item.
                    _processSellItemMethod.Invoke(vm, new object[] { c.VM, true });

                    stagedUnits += maxAffordable;
                    stagedValue += (long)maxAffordable * c.Price;
                }
                catch (TargetInvocationException tie)
                {
                    failures++;
                    // Report only the first exception to avoid spam.
                    if (failures == 1)
                    {
                        string msg = (tie.InnerException != null) ? tie.InnerException.Message : tie.Message;
                        Msg("SaleLoot invoke error: " + msg, settings);
                    }
                }
                catch (Exception ex)
                {
                    failures++;
                    if (failures == 1)
                    {
                        Msg("SaleLoot invoke error: " + ex.Message, settings);
                    }
                }
            }

            if (stagedUnits > 0)
            {
                Msg("{=SaleLoot_Msg_Staged}Staged {COUNT} items for sale ({GOLD} denars). Review and confirm in the trade window.",
                    settings,
                    ("COUNT", stagedUnits.ToString()),
                    ("GOLD", stagedValue.ToString()));
            }
            else
            {
                Msg("SaleLoot diag: gold={G} cand={C} cheapest={P} fail={F} staged=0",
                    settings,
                    ("G", townGold.ToString()),
                    ("C", candidates.Count.ToString()),
                    ("P", candidates[0].Price.ToString()),
                    ("F", failures.ToString()));
            }
        }

        private struct Candidate
        {
            public SPItemVM VM;
            public int Price;
        }

        // --------------------------------------------------------
        //  CATEGORY / TIER
        // --------------------------------------------------------
        private static LootCategory GetCategory(ItemObject item)
        {
            if (item == null) return LootCategory.None;
            var t = item.ItemType;

            switch (t)
            {
                case ItemObject.ItemTypeEnum.OneHandedWeapon:
                case ItemObject.ItemTypeEnum.TwoHandedWeapon:
                case ItemObject.ItemTypeEnum.Polearm:
                    return LootCategory.Weapon;

                case ItemObject.ItemTypeEnum.Bow:
                case ItemObject.ItemTypeEnum.Crossbow:
                case ItemObject.ItemTypeEnum.Arrows:
                case ItemObject.ItemTypeEnum.Bolts:
                case ItemObject.ItemTypeEnum.Thrown:
                case ItemObject.ItemTypeEnum.Shield:
                case ItemObject.ItemTypeEnum.Sling:
                case ItemObject.ItemTypeEnum.SlingStones:
                case ItemObject.ItemTypeEnum.Pistol:
                case ItemObject.ItemTypeEnum.Musket:
                case ItemObject.ItemTypeEnum.Bullets:
                    return LootCategory.Ammo;

                case ItemObject.ItemTypeEnum.HeadArmor:
                case ItemObject.ItemTypeEnum.BodyArmor:
                case ItemObject.ItemTypeEnum.ChestArmor:
                case ItemObject.ItemTypeEnum.LegArmor:
                case ItemObject.ItemTypeEnum.HandArmor:
                case ItemObject.ItemTypeEnum.Cape:
                    return LootCategory.Armor;

                case ItemObject.ItemTypeEnum.HorseHarness:
                    return LootCategory.HorseArmor;
            }

            string name = t.ToString();
            switch (name)
            {
                case "OneHandedWeapon":
                case "TwoHandedWeapon":
                case "Polearm":
                    return LootCategory.Weapon;
                case "Bow":
                case "Crossbow":
                case "Arrows":
                case "Bolts":
                case "Thrown":
                case "Shield":
                case "Sling":
                case "SlingStones":
                case "Pistol":
                case "Musket":
                case "Bullets":
                    return LootCategory.Ammo;
                case "HeadArmor":
                case "BodyArmor":
                case "ChestArmor":
                case "LegArmor":
                case "HandArmor":
                case "Cape":
                    return LootCategory.Armor;
                case "HorseHarness":
                case "HorseArmor":
                    return LootCategory.HorseArmor;
            }
            return LootCategory.None;
        }

        private static int GetTierForUi(ItemObject item)
        {
            if (item == null) return 0;
            try
            {
                string s = item.Tier.ToString();
                if (s.Length > 4 && s.StartsWith("Tier", StringComparison.Ordinal))
                {
                    if (int.TryParse(s.Substring(4), out int n)) return n;
                }
                return (int)item.Tier + 1;
            }
            catch
            {
                try { return (int)item.Tier + 1; } catch { return 0; }
            }
        }

        private static int GetMaxTier(LootCategory cat, SaleLootSettings s)
        {
            if (s == null) return -1;
            switch (cat)
            {
                case LootCategory.Weapon: return s.MaxWeaponTier;
                case LootCategory.Ammo: return s.MaxAmmoTier;
                case LootCategory.Armor: return s.MaxArmorTier;
                case LootCategory.HorseArmor: return s.MaxHorseArmorTier;
            }
            return -1;
        }

        // --------------------------------------------------------
        //  LOCKS
        // --------------------------------------------------------
        private static HashSet<string> GetLockedKeys()
        {
            var set = new HashSet<string>(StringComparer.Ordinal);
            try
            {
                var campaign = Campaign.Current;
                if (campaign == null) return set;

                var tracker = campaign.GetCampaignBehavior<IViewDataTracker>();
                if (tracker == null) return set;

                var mi = tracker.GetType().GetMethod("InventoryGetLocks")
                      ?? tracker.GetType().GetMethod("GetInventoryLocks");
                if (mi == null) return set;

                var ids = mi.Invoke(tracker, null) as IEnumerable;
                if (ids == null) return set;

                foreach (var id in ids)
                {
                    var str = id as string;
                    if (!string.IsNullOrWhiteSpace(str)) set.Add(str);
                }
            }
            catch { }
            return set;
        }

        private static bool IsLocked(HashSet<string> keys, ItemRosterElement e)
        {
            if (keys == null || keys.Count == 0) return false;
            try
            {
                var item = e.EquipmentElement.Item;
                if (item == null) return false;

                string baseKey = item.StringId ?? "";
                string modId = e.EquipmentElement.ItemModifier != null
                    ? e.EquipmentElement.ItemModifier.StringId
                    : null;
                string modKey = baseKey + (modId ?? "");

                return keys.Contains(baseKey) || (modKey.Length > 0 && keys.Contains(modKey));
            }
            catch { return false; }
        }

        // --------------------------------------------------------
        //  CHAT
        // --------------------------------------------------------
        private static void Msg(string text, SaleLootSettings settings, params (string, string)[] args)
        {
            try
            {
                if (settings != null && !settings.ShowMessages) return;

                var to = new TextObject(text);
                if (args != null)
                {
                    foreach (var (k, v) in args)
                    {
                        to.SetTextVariable(k, v);
                    }
                }
                InformationManager.DisplayMessage(new InformationMessage(to.ToString(), Colors.Yellow));
            }
            catch { }
        }
    }

    // ============================================================
    //  HARMONY PATCH — SPInventoryVM constructor
    // ============================================================
    [HarmonyPatch(typeof(SPInventoryVM), MethodType.Constructor, new Type[]
    {
        typeof(InventoryLogic),
        typeof(bool),
        typeof(Func<WeaponComponentData, ItemObject.ItemUsageSetFlags>)
    })]
    internal static class Patch_SPInventoryVM_Ctor
    {
        [HarmonyPostfix]
        private static void Postfix(SPInventoryVM __instance)
        {
            try { SaleLootLogic.OnInventoryVMCreated(__instance); }
            catch { }
        }
    }
}