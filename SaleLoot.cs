using HarmonyLib;
using Helpers;
using MCM.Abstractions.Attributes;
using MCM.Abstractions.Attributes.v2;
using MCM.Abstractions.Base.Global;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
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
    //  LOGGER
    // ============================================================
    internal static class SaleLootLog
    {
        private static readonly object _lock = new object();
        private static string _logPath = null;
        private static bool _initFailed = false;

        private static string LogPath
        {
            get
            {
                if (_logPath != null) return _logPath;
                if (_initFailed) return null;
                try
                {
                    string dir = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                        "Mount and Blade II Bannerlord",
                        "Configs",
                        "SaleLoot");
                    if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                    _logPath = Path.Combine(dir, "saleloot.log");
                    return _logPath;
                }
                catch
                {
                    _initFailed = true;
                    return null;
                }
            }
        }

        public static string GetLogPath() => LogPath;

        public static void Clear()
        {
            try
            {
                var p = LogPath;
                if (p != null && File.Exists(p)) File.Delete(p);
            }
            catch { }
        }

        public static void Write(string message)
        {
            try
            {
                var s = SaleLootSettings.Instance;
                if (s == null || !s.VerboseLog) return;

                var p = LogPath;
                if (p == null) return;

                var line = string.Format("[{0:HH:mm:ss.fff}] {1}\r\n", DateTime.Now, message);
                lock (_lock)
                {
                    File.AppendAllText(p, line, Encoding.UTF8);
                }
            }
            catch { }
        }

        public static void Error(string message)
        {
            try
            {
                var p = LogPath;
                if (p == null) return;

                var line = string.Format("[{0:HH:mm:ss.fff}] [ERROR] {1}\r\n", DateTime.Now, message);
                lock (_lock)
                {
                    File.AppendAllText(p, line, Encoding.UTF8);
                }
            }
            catch { }
        }

        public static void Exc(string where, Exception ex)
        {
            try
            {
                var p = LogPath;
                if (p == null) return;

                var sb = new StringBuilder();
                sb.AppendFormat("[{0:HH:mm:ss.fff}] [EXC] {1}: {2}\r\n", DateTime.Now, where, ex.Message);
                sb.Append(ex.StackTrace);
                sb.Append("\r\n");
                lock (_lock)
                {
                    File.AppendAllText(p, sb.ToString(), Encoding.UTF8);
                }
            }
            catch { }
        }
    }

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

        private bool _verboseLog = false;

        [SettingPropertyGroup("{=SaleLoot_Group_Tiers}Tiers", GroupOrder = 0)]
        [SettingPropertyBool("{=SaleLoot_VerboseLog_Name}Verbose log file", Order = 11,
            RequireRestart = false,
            HintText = "{=SaleLoot_VerboseLog_Hint}Write every step to Configs/SaleLoot/saleloot.log. Very verbose. For diagnostics.")]
        public bool VerboseLog
        {
            get { try { return _verboseLog; } catch { return false; } }
            set { try { if (_verboseLog != value) { _verboseLog = value; OnPropertyChanged(nameof(VerboseLog)); } } catch { } }
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

                SaleLootLog.Clear();
                SaleLootLog.Write("==== SaleLoot submodule loaded ====");

                InformationManager.DisplayMessage(new InformationMessage("Sale Loot loaded.", Colors.Yellow));
            }
            catch (Exception ex)
            {
                SaleLootLog.Exc("OnSubModuleLoad", ex);
                try { InformationManager.DisplayMessage(new InformationMessage("Sale Loot load error: " + ex.Message, Colors.Red)); } catch { }
            }
        }

        protected override void OnSubModuleUnloaded()
        {
            base.OnSubModuleUnloaded();
            try
            {
                SaleLootLog.Write("==== SaleLoot submodule unloaded ====");
                _harmony?.UnpatchAll("com.saleloot.patch");
            }
            catch (Exception ex) { SaleLootLog.Exc("OnSubModuleUnloaded", ex); }
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
                    SaleLootLog.Write("SaleLootBehavior added.");
                }
            }
            catch (Exception ex) { SaleLootLog.Exc("OnGameStart", ex); }
        }
    }

    // ============================================================
    //  BEHAVIOR
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
                SaleLootLog.Write("Menu option 'Sell Loot' registered on 'town'.");
            }
            catch (Exception ex) { SaleLootLog.Exc("OnSessionLaunched", ex); }
        }

        private bool OnMenuCondition(MenuCallbackArgs args)
        {
            try
            {
                var s = Settlement.CurrentSettlement;
                bool ok = s != null && s.IsTown && s.Town != null;
                SaleLootLog.Write("OnMenuCondition: settlement=" + (s != null ? s.Name.ToString() : "null") + " ok=" + ok);
                if (!ok) return false;
                args.optionLeaveType = GameMenuOption.LeaveType.Trade;
                return true;
            }
            catch (Exception ex) { SaleLootLog.Exc("OnMenuCondition", ex); return false; }
        }

        private void OnMenuConsequence(MenuCallbackArgs args)
        {
            try
            {
                SaleLootLog.Write(">>> OnMenuConsequence: user clicked 'Sell Loot'.");
                SaleLootLogic.OpenTradeAndStage(Settlement.CurrentSettlement);
            }
            catch (Exception ex) { SaleLootLog.Exc("OnMenuConsequence", ex); }
        }
    }

    // ============================================================
    //  CORE LOGIC
    // ============================================================
    internal enum LootCategory { None, Weapon, Ammo, Armor, HorseArmor }

    internal static class SaleLootLogic
    {
        private static bool _pendingStage = false;

        private static MethodInfo _processSellItemMethod = null;
        private static FieldInfo _invLogicField = null;

        public static void OpenTradeAndStage(Settlement town)
        {
            try
            {
                if (town == null || !town.IsTown || town.Town == null)
                {
                    SaleLootLog.Write("OpenTradeAndStage: invalid town, abort.");
                    return;
                }
                _pendingStage = true;
                SaleLootLog.Write("OpenTradeAndStage: town=" + town.Name.ToString() + " flag set, opening trade screen.");

                InventoryScreenHelper.ActivateTradeWithCurrentSettlement();
                SaleLootLog.Write("OpenTradeAndStage: trade screen opened.");
            }
            catch (Exception ex)
            {
                _pendingStage = false;
                SaleLootLog.Exc("OpenTradeAndStage", ex);
                Msg("SaleLoot error: " + ex.Message, SaleLootSettings.Instance);
            }
        }

        internal static void OnInventoryReady(SPInventoryVM vm, string caller)
        {
            try
            {
                if (!_pendingStage)
                {
                    SaleLootLog.Write("OnInventoryReady(" + caller + "): no pending stage, ignore.");
                    return;
                }
                if (vm == null)
                {
                    SaleLootLog.Write("OnInventoryReady(" + caller + "): vm null, ignore.");
                    return;
                }

                int listCount = 0;
                try { listCount = vm.RightItemListVM != null ? vm.RightItemListVM.Count : -1; } catch { listCount = -1; }

                int merchantGold = 0;
                try { merchantGold = vm.LeftInventoryOwnerGold; } catch { merchantGold = -1; }

                var inv = TryGetInventoryLogic(vm);
                int totalAmount = -99999;
                try { if (inv != null) totalAmount = inv.TotalAmount; } catch { }

                SaleLootLog.Write("OnInventoryReady(" + caller + "): listCount=" + listCount
                    + " LeftInventoryOwnerGold=" + merchantGold
                    + " inv.TotalAmount=" + totalAmount
                    + " inv=" + (inv != null ? "ok" : "null"));

                if (listCount <= 0)
                {
                    SaleLootLog.Write("OnInventoryReady: list empty, waiting for next call.");
                    return;
                }
                if (merchantGold <= 0)
                {
                    SaleLootLog.Write("OnInventoryReady: merchant gold <= 0, waiting for next call.");
                    return;
                }

                _pendingStage = false;
                SaleLootLog.Write("OnInventoryReady: conditions met, running ApplyStaging.");

                ApplyStaging(vm, merchantGold);
            }
            catch (Exception ex) { SaleLootLog.Exc("OnInventoryReady(" + caller + ")", ex); }
        }

        private static InventoryLogic TryGetInventoryLogic(SPInventoryVM vm)
        {
            try
            {
                if (_invLogicField == null)
                {
                    _invLogicField = typeof(SPInventoryVM).GetField(
                        "_inventoryLogic",
                        BindingFlags.Instance | BindingFlags.NonPublic);
                    SaleLootLog.Write("TryGetInventoryLogic: field resolved=" + (_invLogicField != null));
                }
                return _invLogicField?.GetValue(vm) as InventoryLogic;
            }
            catch (Exception ex) { SaleLootLog.Exc("TryGetInventoryLogic", ex); return null; }
        }

        private static void ApplyStaging(SPInventoryVM vm, int initialMerchantGold)
        {
            try
            {
                if (vm == null) { SaleLootLog.Write("ApplyStaging: vm null, abort."); return; }

                var settings = SaleLootSettings.Instance;
                if (settings == null) { SaleLootLog.Write("ApplyStaging: settings null, abort."); return; }

                SaleLootLog.Write("ApplyStaging: START. merchant gold=" + initialMerchantGold
                    + " tiers W/A/A/H=" + settings.MaxWeaponTier + "/" + settings.MaxAmmoTier + "/" + settings.MaxArmorTier + "/" + settings.MaxHorseArmorTier);

                long budgetRemaining = initialMerchantGold;

                var lockedKeys = GetLockedKeys();
                SaleLootLog.Write("ApplyStaging: locked keys count=" + lockedKeys.Count);

                if (_processSellItemMethod == null)
                {
                    try
                    {
                        _processSellItemMethod = typeof(SPInventoryVM).GetMethod(
                            "ProcessSellItem",
                            BindingFlags.Instance | BindingFlags.NonPublic);
                    }
                    catch (Exception ex) { SaleLootLog.Exc("resolve ProcessSellItem", ex); }
                    SaleLootLog.Write("ApplyStaging: ProcessSellItem method=" + (_processSellItemMethod != null));
                }

                if (_processSellItemMethod == null)
                {
                    SaleLootLog.Write("ApplyStaging: could not resolve ProcessSellItem, abort.");
                    Msg("SaleLoot: could not resolve SPInventoryVM.ProcessSellItem", settings);
                    return;
                }

                // ---- Collect candidates ----
                int totalScanned = 0;
                int rejectedNotTransferable = 0;
                int rejectedLocked = 0;
                int rejectedNotMerch = 0;
                int rejectedCategory = 0;
                int rejectedTier = 0;
                int rejectedLockKey = 0;
                int rejectedPrice = 0;

                var candidates = new List<Candidate>();
                foreach (var itemVM in vm.RightItemListVM)
                {
                    totalScanned++;
                    if (itemVM == null) continue;
                    if (!itemVM.IsTransferable) { rejectedNotTransferable++; continue; }
                    if (itemVM.IsLocked) { rejectedLocked++; continue; }

                    var eq = itemVM.ItemRosterElement.EquipmentElement;
                    var item = eq.Item;
                    if (item == null) continue;
                    if (item.NotMerchandise) { rejectedNotMerch++; continue; }

                    var cat = GetCategory(item);
                    if (cat == LootCategory.None) { rejectedCategory++; continue; }

                    int maxTier = GetMaxTier(cat, settings);
                    int itemTier = GetTierForUi(item);
                    if (itemTier > maxTier)
                    {
                        rejectedTier++;
                        SaleLootLog.Write("  [skip] " + item.Name + " tier=" + itemTier + " > max=" + maxTier + " cat=" + cat);
                        continue;
                    }

                    if (IsLocked(lockedKeys, itemVM.ItemRosterElement)) { rejectedLockKey++; continue; }

                    int price = 0;
                    try { price = itemVM.ItemCost; } catch { price = 0; }
                    if (price <= 0) { rejectedPrice++; continue; }

                    candidates.Add(new Candidate { VM = itemVM, Price = price });
                }

                SaleLootLog.Write("ApplyStaging: scanned=" + totalScanned
                    + " candidates=" + candidates.Count
                    + " | rejected: notTransferable=" + rejectedNotTransferable
                    + " lockedFlag=" + rejectedLocked
                    + " notMerch=" + rejectedNotMerch
                    + " category=None=" + rejectedCategory
                    + " tier=" + rejectedTier
                    + " lockKey=" + rejectedLockKey
                    + " price=0=" + rejectedPrice);

                if (candidates.Count == 0)
                {
                    SaleLootLog.Write("ApplyStaging: no candidates, sending chat msg.");
                    Msg("{=SaleLoot_Msg_Nothing}No suitable loot to sell. (list={L})", settings,
                        ("L", (vm.RightItemListVM != null ? vm.RightItemListVM.Count : 0).ToString()));
                    return;
                }

                candidates.Sort((a, b) => a.Price.CompareTo(b.Price));

                int logN = Math.Min(10, candidates.Count);
                for (int i = 0; i < logN; i++)
                {
                    var c = candidates[i];
                    string nm = "?";
                    try { nm = c.VM.ItemRosterElement.EquipmentElement.Item.Name.ToString(); } catch { }
                    int cnt = 0; try { cnt = c.VM.ItemCount; } catch { }
                    SaleLootLog.Write("  cheapest[" + i + "] " + nm + " price=" + c.Price + " count=" + cnt);
                }

                long stagedValue = 0;
                int stagedUnits = 0;
                int failures = 0;
                int stagedStacks = 0;

                // Make sure the vanilla ProcessSellItem picks up our TransactionCount
                // and takes the "cameFromTradeData:false" branch (which calls
                // ExecuteRemoveZeroCounts). Save/restore the UI modifier flags so we
                // don't interfere with real clicks.
                bool savedEntire = vm.IsEntireStackModifierActive;
                bool savedFive = vm.IsFiveStackModifierActive;
                vm.IsEntireStackModifierActive = false;
                vm.IsFiveStackModifierActive = false;

                try
                {
                    foreach (var c in candidates)
                    {
                        if (c.VM == null) continue;
                        if (budgetRemaining <= 0) break;
                        if (c.Price > budgetRemaining) continue;

                        int available = 0;
                        try { available = c.VM.ItemCount; } catch { available = 0; }
                        if (available <= 0) continue;

                        int maxAffordable = (int)Math.Min((long)available, budgetRemaining / c.Price);
                        if (maxAffordable <= 0) continue;

                        try
                        {
                            c.VM.TransactionCount = maxAffordable;

                            // cameFromTradeData: false →
                            //   ProcessSellItem uses item.TransactionCount, then calls
                            //   ExecuteRemoveZeroCounts(), which drops the 0-count row
                            //   from the visual list. This fixes the "0 шт" ghost entry.
                            _processSellItemMethod.Invoke(vm, new object[] { c.VM, false });

                            stagedUnits += maxAffordable;
                            stagedValue += (long)maxAffordable * c.Price;
                            budgetRemaining -= (long)maxAffordable * c.Price;
                            stagedStacks++;
                        }
                        catch (TargetInvocationException tie)
                        {
                            failures++;
                            string msg = (tie.InnerException != null) ? tie.InnerException.Message : tie.Message;
                            SaleLootLog.Write("ApplyStaging: invoke threw: " + msg);
                            if (failures == 1) Msg("SaleLoot invoke error: " + msg, settings);
                        }
                        catch (Exception ex)
                        {
                            failures++;
                            SaleLootLog.Exc("invoke", ex);
                            if (failures == 1) Msg("SaleLoot invoke error: " + ex.Message, settings);
                        }
                    }
                }
                finally
                {
                    vm.IsEntireStackModifierActive = savedEntire;
                    vm.IsFiveStackModifierActive = savedFive;
                }

                SaleLootLog.Write("ApplyStaging: END. stacks=" + stagedStacks
                    + " units=" + stagedUnits
                    + " value=" + stagedValue
                    + " budgetLeft=" + budgetRemaining
                    + " failures=" + failures);

                if (stagedUnits > 0)
                {
                    Msg("{=SaleLoot_Msg_Staged}Staged {COUNT} items for sale ({GOLD} denars). Review and confirm in the trade window.",
                        settings,
                        ("COUNT", stagedUnits.ToString()),
                        ("GOLD", stagedValue.ToString()));

                    int skippedLocked = rejectedLocked + rejectedLockKey;
                    int skippedNotForSale = rejectedNotMerch + rejectedNotTransferable;
                    int skippedOverTier = rejectedTier;

                    if (skippedLocked > 0 || skippedNotForSale > 0 || skippedOverTier > 0)
                    {
                        Msg("{=SaleLoot_Msg_Skipped}Skipped: {LOCKED} locked, {NOTFORSALE} not-for-sale, {OVERTIER} over tier.",
                            settings,
                            ("LOCKED", skippedLocked.ToString()),
                            ("NOTFORSALE", skippedNotForSale.ToString()),
                            ("OVERTIER", skippedOverTier.ToString()));
                    }
                }
                else
                {
                    Msg("SaleLoot diag: gold={G} cand={C} cheapest={P} fail={F} staged=0",
                        settings,
                        ("G", initialMerchantGold.ToString()),
                        ("C", candidates.Count.ToString()),
                        ("P", candidates[0].Price.ToString()),
                        ("F", failures.ToString()));
                }
            }
            catch (Exception ex) { SaleLootLog.Exc("ApplyStaging", ex); }
        }

        private struct Candidate
        {
            public SPItemVM VM;
            public int Price;
        }

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
            catch (Exception ex) { SaleLootLog.Exc("GetLockedKeys", ex); }
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

        private static void Msg(string text, SaleLootSettings settings, params (string, string)[] args)
        {
            try
            {
                var to = new TextObject(text);
                if (args != null)
                {
                    foreach (var (k, v) in args) to.SetTextVariable(k, v);
                }
                string final = to.ToString();
                SaleLootLog.Write("[CHAT] " + final);

                if (settings != null && !settings.ShowMessages) return;
                InformationManager.DisplayMessage(new InformationMessage(final, Colors.Yellow));
            }
            catch (Exception ex) { SaleLootLog.Exc("Msg", ex); }
        }
    }

    // ============================================================
    //  HARMONY PATCHES
    // ============================================================
    [HarmonyPatch(typeof(SPInventoryVM), "InitializeInventory")]
    internal static class Patch_InitializeInventory
    {
        [HarmonyPostfix]
        private static void Postfix(SPInventoryVM __instance)
        {
            try { SaleLootLogic.OnInventoryReady(__instance, "InitializeInventory"); }
            catch (Exception ex) { SaleLootLog.Exc("Postfix_InitializeInventory", ex); }
        }
    }

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
            try { SaleLootLogic.OnInventoryReady(__instance, "ctor"); }
            catch (Exception ex) { SaleLootLog.Exc("Postfix_Ctor", ex); }
        }
    }

    [HarmonyPatch(typeof(SPInventoryVM), "UpdateLeftCharacter")]
    internal static class Patch_UpdateLeftCharacter
    {
        [HarmonyPostfix]
        private static void Postfix(SPInventoryVM __instance)
        {
            try { SaleLootLogic.OnInventoryReady(__instance, "UpdateLeftCharacter"); }
            catch (Exception ex) { SaleLootLog.Exc("Postfix_UpdateLeftCharacter", ex); }
        }
    }
}