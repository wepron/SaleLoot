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
using TaleWorlds.CampaignSystem.Inventory;
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

        // ---- Master switch ----
        private bool _enabled = true;

        [SettingPropertyGroup("{=SaleLoot_Group_Tiers}Tiers", GroupOrder = 0)]
        [SettingPropertyBool("{=SaleLoot_Enabled_Name}Enable mod", Order = -1,
            RequireRestart = false,
            HintText = "{=SaleLoot_Enabled_Hint}Turn the mod on or off. When off, the 'Sell Loot' menu option is hidden and nothing else happens.")]
        public bool Enabled
        {
            get { try { return _enabled; } catch { return true; } }
            set { try { if (_enabled != value) { _enabled = value; OnPropertyChanged(nameof(Enabled)); } } catch { } }
        }

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

        // ============================================================
        //  GOODS (master switch + 22 individual toggles)
        // ============================================================

        private bool _enableGoods = true;

        [SettingPropertyGroup("{=SaleLoot_Group_Goods}Goods", GroupOrder = 1)]
        [SettingPropertyBool("{=SaleLoot_EnableGoods_Name}Sell goods", Order = -1,
            RequireRestart = false,
            HintText = "{=SaleLoot_EnableGoods_Hint}Turn on or off the whole Goods section. When off, no goods are ever sold, regardless of individual toggles.")]
        public bool EnableGoods
        {
            get { try { return _enableGoods; } catch { return true; } }
            set { try { if (_enableGoods != value) { _enableGoods = value; OnPropertyChanged(nameof(EnableGoods)); } } catch { } }
        }

        // --- wool
        private bool _goodWool = false;
        [SettingPropertyGroup("{=SaleLoot_Group_Goods}Goods", GroupOrder = 1)]
        [SettingPropertyBool("{=SaleLoot_Good_wool_Name}Wool", Order = 0, RequireRestart = false)]
        public bool GoodWool { get { try { return _goodWool; } catch { return false; } } set { try { if (_goodWool != value) { _goodWool = value; OnPropertyChanged(nameof(GoodWool)); } } catch { } } }

        // --- silver
        private bool _goodSilver = false;
        [SettingPropertyGroup("{=SaleLoot_Group_Goods}Goods", GroupOrder = 1)]
        [SettingPropertyBool("{=SaleLoot_Good_silver_Name}Silver Ore", Order = 1, RequireRestart = false)]
        public bool GoodSilver { get { try { return _goodSilver; } catch { return false; } } set { try { if (_goodSilver != value) { _goodSilver = value; OnPropertyChanged(nameof(GoodSilver)); } } catch { } } }

        // --- jewelry
        private bool _goodJewelry = false;
        [SettingPropertyGroup("{=SaleLoot_Group_Goods}Goods", GroupOrder = 1)]
        [SettingPropertyBool("{=SaleLoot_Good_jewelry_Name}Jewelry", Order = 2, RequireRestart = false)]
        public bool GoodJewelry { get { try { return _goodJewelry; } catch { return false; } } set { try { if (_goodJewelry != value) { _goodJewelry = value; OnPropertyChanged(nameof(GoodJewelry)); } } catch { } } }

        // --- salt
        private bool _goodSalt = false;
        [SettingPropertyGroup("{=SaleLoot_Group_Goods}Goods", GroupOrder = 1)]
        [SettingPropertyBool("{=SaleLoot_Good_salt_Name}Salt", Order = 3, RequireRestart = false)]
        public bool GoodSalt { get { try { return _goodSalt; } catch { return false; } } set { try { if (_goodSalt != value) { _goodSalt = value; OnPropertyChanged(nameof(GoodSalt)); } } catch { } } }

        // --- spice
        private bool _goodSpice = false;
        [SettingPropertyGroup("{=SaleLoot_Group_Goods}Goods", GroupOrder = 1)]
        [SettingPropertyBool("{=SaleLoot_Good_spice_Name}Spice", Order = 4, RequireRestart = false)]
        public bool GoodSpice { get { try { return _goodSpice; } catch { return false; } } set { try { if (_goodSpice != value) { _goodSpice = value; OnPropertyChanged(nameof(GoodSpice)); } } catch { } } }

        // --- cotton (Raw Silk)
        private bool _goodCotton = false;
        [SettingPropertyGroup("{=SaleLoot_Group_Goods}Goods", GroupOrder = 1)]
        [SettingPropertyBool("{=SaleLoot_Good_cotton_Name}Raw Silk", Order = 5, RequireRestart = false)]
        public bool GoodCotton { get { try { return _goodCotton; } catch { return false; } } set { try { if (_goodCotton != value) { _goodCotton = value; OnPropertyChanged(nameof(GoodCotton)); } } catch { } } }

        // --- flax
        private bool _goodFlax = false;
        [SettingPropertyGroup("{=SaleLoot_Group_Goods}Goods", GroupOrder = 1)]
        [SettingPropertyBool("{=SaleLoot_Good_flax_Name}Flax", Order = 6, RequireRestart = false)]
        public bool GoodFlax { get { try { return _goodFlax; } catch { return false; } } set { try { if (_goodFlax != value) { _goodFlax = value; OnPropertyChanged(nameof(GoodFlax)); } } catch { } } }

        // --- clay
        private bool _goodClay = false;
        [SettingPropertyGroup("{=SaleLoot_Group_Goods}Goods", GroupOrder = 1)]
        [SettingPropertyBool("{=SaleLoot_Good_clay_Name}Clay", Order = 7, RequireRestart = false)]
        public bool GoodClay { get { try { return _goodClay; } catch { return false; } } set { try { if (_goodClay != value) { _goodClay = value; OnPropertyChanged(nameof(GoodClay)); } } catch { } } }

        // --- pottery
        private bool _goodPottery = false;
        [SettingPropertyGroup("{=SaleLoot_Group_Goods}Goods", GroupOrder = 1)]
        [SettingPropertyBool("{=SaleLoot_Good_pottery_Name}Pottery", Order = 8, RequireRestart = false)]
        public bool GoodPottery { get { try { return _goodPottery; } catch { return false; } } set { try { if (_goodPottery != value) { _goodPottery = value; OnPropertyChanged(nameof(GoodPottery)); } } catch { } } }

        // --- linen
        private bool _goodLinen = false;
        [SettingPropertyGroup("{=SaleLoot_Group_Goods}Goods", GroupOrder = 1)]
        [SettingPropertyBool("{=SaleLoot_Good_linen_Name}Linen", Order = 9, RequireRestart = false)]
        public bool GoodLinen { get { try { return _goodLinen; } catch { return false; } } set { try { if (_goodLinen != value) { _goodLinen = value; OnPropertyChanged(nameof(GoodLinen)); } } catch { } } }

        // --- leather
        private bool _goodLeather = false;
        [SettingPropertyGroup("{=SaleLoot_Group_Goods}Goods", GroupOrder = 1)]
        [SettingPropertyBool("{=SaleLoot_Good_leather_Name}Leather", Order = 10, RequireRestart = false)]
        public bool GoodLeather { get { try { return _goodLeather; } catch { return false; } } set { try { if (_goodLeather != value) { _goodLeather = value; OnPropertyChanged(nameof(GoodLeather)); } } catch { } } }

        // --- velvet
        private bool _goodVelvet = false;
        [SettingPropertyGroup("{=SaleLoot_Group_Goods}Goods", GroupOrder = 1)]
        [SettingPropertyBool("{=SaleLoot_Good_velvet_Name}Velvet", Order = 11, RequireRestart = false)]
        public bool GoodVelvet { get { try { return _goodVelvet; } catch { return false; } } set { try { if (_goodVelvet != value) { _goodVelvet = value; OnPropertyChanged(nameof(GoodVelvet)); } } catch { } } }

        // --- wine
        private bool _goodWine = false;
        [SettingPropertyGroup("{=SaleLoot_Group_Goods}Goods", GroupOrder = 1)]
        [SettingPropertyBool("{=SaleLoot_Good_wine_Name}Wine", Order = 12, RequireRestart = false)]
        public bool GoodWine { get { try { return _goodWine; } catch { return false; } } set { try { if (_goodWine != value) { _goodWine = value; OnPropertyChanged(nameof(GoodWine)); } } catch { } } }

        // --- oil
        private bool _goodOil = false;
        [SettingPropertyGroup("{=SaleLoot_Group_Goods}Goods", GroupOrder = 1)]
        [SettingPropertyBool("{=SaleLoot_Good_oil_Name}Oil", Order = 13, RequireRestart = false)]
        public bool GoodOil { get { try { return _goodOil; } catch { return false; } } set { try { if (_goodOil != value) { _goodOil = value; OnPropertyChanged(nameof(GoodOil)); } } catch { } } }

        // --- fur
        private bool _goodFur = false;
        [SettingPropertyGroup("{=SaleLoot_Group_Goods}Goods", GroupOrder = 1)]
        [SettingPropertyBool("{=SaleLoot_Good_fur_Name}Fur", Order = 14, RequireRestart = false)]
        public bool GoodFur { get { try { return _goodFur; } catch { return false; } } set { try { if (_goodFur != value) { _goodFur = value; OnPropertyChanged(nameof(GoodFur)); } } catch { } } }

        // --- hardwood
        private bool _goodHardwood = false;
        [SettingPropertyGroup("{=SaleLoot_Group_Goods}Goods", GroupOrder = 1)]
        [SettingPropertyBool("{=SaleLoot_Good_hardwood_Name}Hardwood", Order = 15, RequireRestart = false)]
        public bool GoodHardwood { get { try { return _goodHardwood; } catch { return false; } } set { try { if (_goodHardwood != value) { _goodHardwood = value; OnPropertyChanged(nameof(GoodHardwood)); } } catch { } } }

        // --- charcoal
        private bool _goodCharcoal = false;
        [SettingPropertyGroup("{=SaleLoot_Group_Goods}Goods", GroupOrder = 1)]
        [SettingPropertyBool("{=SaleLoot_Good_charcoal_Name}Charcoal", Order = 16, RequireRestart = false)]
        public bool GoodCharcoal { get { try { return _goodCharcoal; } catch { return false; } } set { try { if (_goodCharcoal != value) { _goodCharcoal = value; OnPropertyChanged(nameof(GoodCharcoal)); } } catch { } } }

        // --- iron_ore
        private bool _goodIronOre = false;
        [SettingPropertyGroup("{=SaleLoot_Group_Goods}Goods", GroupOrder = 1)]
        [SettingPropertyBool("{=SaleLoot_Good_iron_ore_Name}Iron Ore", Order = 17, RequireRestart = false)]
        public bool GoodIronOre { get { try { return _goodIronOre; } catch { return false; } } set { try { if (_goodIronOre != value) { _goodIronOre = value; OnPropertyChanged(nameof(GoodIronOre)); } } catch { } } }

        // --- iron
        private bool _goodIron = false;
        [SettingPropertyGroup("{=SaleLoot_Group_Goods}Goods", GroupOrder = 1)]
        [SettingPropertyBool("{=SaleLoot_Good_iron_Name}Iron", Order = 18, RequireRestart = false)]
        public bool GoodIron { get { try { return _goodIron; } catch { return false; } } set { try { if (_goodIron != value) { _goodIron = value; OnPropertyChanged(nameof(GoodIron)); } } catch { } } }

        // --- steel
        private bool _goodSteel = false;
        [SettingPropertyGroup("{=SaleLoot_Group_Goods}Goods", GroupOrder = 1)]
        [SettingPropertyBool("{=SaleLoot_Good_steel_Name}Steel", Order = 19, RequireRestart = false)]
        public bool GoodSteel { get { try { return _goodSteel; } catch { return false; } } set { try { if (_goodSteel != value) { _goodSteel = value; OnPropertyChanged(nameof(GoodSteel)); } } catch { } } }

        // --- fine_steel
        private bool _goodFineSteel = false;
        [SettingPropertyGroup("{=SaleLoot_Group_Goods}Goods", GroupOrder = 1)]
        [SettingPropertyBool("{=SaleLoot_Good_fine_steel_Name}Fine Steel", Order = 20, RequireRestart = false)]
        public bool GoodFineSteel { get { try { return _goodFineSteel; } catch { return false; } } set { try { if (_goodFineSteel != value) { _goodFineSteel = value; OnPropertyChanged(nameof(GoodFineSteel)); } } catch { } } }

        // --- thamaskene_steel
        private bool _goodThamaskeneSteel = false;
        [SettingPropertyGroup("{=SaleLoot_Group_Goods}Goods", GroupOrder = 1)]
        [SettingPropertyBool("{=SaleLoot_Good_thamaskene_steel_Name}Thamaskene Steel", Order = 21, RequireRestart = false)]
        public bool GoodThamaskeneSteel { get { try { return _goodThamaskeneSteel; } catch { return false; } } set { try { if (_goodThamaskeneSteel != value) { _goodThamaskeneSteel = value; OnPropertyChanged(nameof(GoodThamaskeneSteel)); } } catch { } } }
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
                var settings = SaleLootSettings.Instance;
                if (settings == null || !settings.Enabled)
                {
                    SaleLootLog.Write("OnMenuCondition: mod disabled, hiding option.");
                    return false;
                }

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
                var settings = SaleLootSettings.Instance;
                if (settings == null || !settings.Enabled) return;

                SaleLootLog.Write(">>> OnMenuConsequence: user clicked 'Sell Loot'.");
                SaleLootLogic.OpenTradeAndStage(Settlement.CurrentSettlement);
            }
            catch (Exception ex) { SaleLootLog.Exc("OnMenuConsequence", ex); }
        }
    }

    // ============================================================
    //  CORE LOGIC
    // ============================================================
    internal enum LootCategory { None, Weapon, Ammo, Armor, HorseArmor, Goods }

    internal static class SaleLootLogic
    {
        private static bool _pendingStage = false;

        private static MethodInfo _processSellItemMethod = null;
        private static FieldInfo _invLogicField = null;

        public static void OpenTradeAndStage(Settlement town)
        {
            try
            {
                var settings = SaleLootSettings.Instance;
                if (settings == null || !settings.Enabled)
                {
                    SaleLootLog.Write("OpenTradeAndStage: mod disabled, abort.");
                    return;
                }

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

                var s = SaleLootSettings.Instance;
                if (s == null || !s.Enabled)
                {
                    _pendingStage = false;
                    SaleLootLog.Write("OnInventoryReady(" + caller + "): mod disabled, clearing pending and ignore.");
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
                if (!settings.Enabled) { SaleLootLog.Write("ApplyStaging: mod disabled, abort."); return; }

                SaleLootLog.Write("ApplyStaging: START. merchant gold=" + initialMerchantGold
                    + " tiers W/A/A/H=" + settings.MaxWeaponTier + "/" + settings.MaxAmmoTier + "/" + settings.MaxArmorTier + "/" + settings.MaxHorseArmorTier
                    + " EnableGoods=" + settings.EnableGoods);

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

                // Goods-specific counters
                int rejectedGoodsMaster = 0;
                int rejectedGoodsFood = 0;
                int rejectedGoodsToggle = 0;

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

                    // ---- GOODS branch ----
                    if (cat == LootCategory.Goods)
                    {
                        if (!settings.EnableGoods) { rejectedGoodsMaster++; continue; }
                        if (item.IsFood) { rejectedGoodsFood++; continue; }
                        if (IsLocked(lockedKeys, itemVM.ItemRosterElement)) { rejectedLockKey++; continue; }
                        if (!IsGoodsEnabled(item.StringId, settings)) { rejectedGoodsToggle++; continue; }

                        int goodsPrice = 0;
                        try { goodsPrice = itemVM.ItemCost; } catch { goodsPrice = 0; }
                        if (goodsPrice <= 0) { rejectedPrice++; continue; }

                        candidates.Add(new Candidate { VM = itemVM, Price = goodsPrice });
                        continue;
                    }

                    // ---- Tier-based branch (weapons / ammo / armor / horse armor) ----
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
                    + " price=0=" + rejectedPrice
                    + " | goods: master=" + rejectedGoodsMaster
                    + " food=" + rejectedGoodsFood
                    + " toggle=" + rejectedGoodsToggle);

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

                            // cameFromTradeData:false → vanilla calls ExecuteRemoveZeroCounts,
                            // which drops the 0-count row from the visual list.
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

                case ItemObject.ItemTypeEnum.Goods:
                    return LootCategory.Goods;
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
                case "Goods":
                    return LootCategory.Goods;
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
                case LootCategory.Goods: return int.MaxValue;
            }
            return -1;
        }

        private static bool IsGoodsEnabled(string id, SaleLootSettings s)
        {
            if (s == null || string.IsNullOrEmpty(id)) return false;
            switch (id)
            {
                case "wool": return s.GoodWool;
                case "silver": return s.GoodSilver;
                case "jewelry": return s.GoodJewelry;
                case "salt": return s.GoodSalt;
                case "spice": return s.GoodSpice;
                case "cotton": return s.GoodCotton;
                case "flax": return s.GoodFlax;
                case "clay": return s.GoodClay;
                case "pottery": return s.GoodPottery;
                case "linen": return s.GoodLinen;
                case "leather": return s.GoodLeather;
                case "velvet": return s.GoodVelvet;
                case "wine": return s.GoodWine;
                case "oil": return s.GoodOil;
                case "fur": return s.GoodFur;
                case "hardwood": return s.GoodHardwood;
                case "charcoal": return s.GoodCharcoal;
                case "iron_ore": return s.GoodIronOre;
                case "iron": return s.GoodIron;
                case "steel": return s.GoodSteel;
                case "fine_steel": return s.GoodFineSteel;
                case "thamaskene_steel": return s.GoodThamaskeneSteel;
                default: return false;
            }
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