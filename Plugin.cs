using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Extensions;
using Jotunn.Managers;
using Jotunn.Utils;
using UnityEngine;

namespace VALKEA
{
    [BepInDependency(Jotunn.Main.ModGuid, "2.30.2")]
    [BepInPlugin(ModGUID, ModName, ModVersion)]
    [NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod, VersionStrictness.Major)]
    public class  VALKEAplugin: BaseUnityPlugin
    {
        internal const string ModName = "VALKEA";
        internal const string ModVersion = "3.1.0";
        internal const string Author = "TheBeesDecree";
        private const string ModGUID = Author + "." + ModName;

        private readonly Harmony _harmony = new(ModGUID);

        public static readonly ManualLogSource VALKEALogger =
            BepInEx.Logging.Logger.CreateLogSource(ModName);

        private PieceTable _pieceTable;
        private CustomItem _hammer;
        private ConfigEntry<string> _recipe;
        private ConfigEntry<bool> _spiralStairsNeedSupport;

        public void Awake()
        {
            _recipe = Config.BindConfig("BJORKSNAS", "Recipe", "FineWood:1,QueenBee:1,Coins:999:99",
                "Items needed to craft BJORKSNAS at a workbench, separated by commas. Each is Item:Amount, " +
                "or Item:Amount:PerLevel where PerLevel is what each upgrade costs (BJORKSNAS upgrades to level 3)");
            _spiralStairsNeedSupport = Config.BindConfig("Pieces", "Spiral stairs need support", false,
                "Off: the v2 spiral staircases and the spiral columns float without support, as they always have. " +
                "On: they break without support like other pieces, which can bring down existing builds that rely on floating ones.");

            AddPieces();
            PieceFixes.Apply(_pieceTable.m_pieces);
            PieceFixes.SetSpiralStairsNeedSupport(_spiralStairsNeedSupport.Value);
            PieceManager.OnPiecesRegistered += TagPieces;
            _recipe.SettingChanged += (_, _) => UpdateRecipe();
            _spiralStairsNeedSupport.SettingChanged += (_, _) => PieceFixes.SetSpiralStairsNeedSupport(_spiralStairsNeedSupport.Value);

            Assembly assembly = Assembly.GetExecutingAssembly();
            _harmony.PatchAll(assembly);
        }

        private void AddPieces()
        {
            string assetBundleName = "bjorksnas";
            var loadedMaterials = new HashSet<Material>(Resources.FindObjectsOfTypeAll<Material>());
            AssetBundle bundle = AssetUtils.LoadAssetBundleFromResources(assetBundleName, Assembly.GetExecutingAssembly());
            var objects = bundle.LoadAllAssets<GameObject>();
            PieceFixes.RenameBundleMaterials(Resources.FindObjectsOfTypeAll<Material>().Where(m => !loadedMaterials.Contains(m)));

            // Add Custom hammer
            GameObject bjorksnas_go = bundle.LoadAsset<GameObject>("BJORKSNAS");
            ItemConfig bjorksnas_ic = new ItemConfig();
            bjorksnas_ic.Name = "BJORKSNAS";
            bjorksnas_ic.Description = "Legend has it that BJÖRKSNÄS is the holy key needed to assemble all goods from VALKEA";
            bjorksnas_ic.CraftingStation = CraftingStations.Workbench;
            bjorksnas_ic.Requirements = ParseRecipe(_recipe.Value);
            _hammer = new CustomItem(bjorksnas_go, false, bjorksnas_ic);
            ItemManager.Instance.AddItem(_hammer);

            // Fix asset bundle piece table by filling missing fields
            // These must have the same length and need to match the category integer in your asset bundle
            var piecetable = bjorksnas_go.GetComponent<ItemDrop>().m_itemData.m_shared.m_buildPieces;
            _pieceTable = piecetable;
            piecetable.m_categories = new List<Piece.PieceCategory>()
            {
                Piece.PieceCategory.Misc,
                Piece.PieceCategory.Crafting,
                Piece.PieceCategory.BuildingWorkbench,
                Piece.PieceCategory.BuildingStonecutter
            };
            piecetable.m_categoryLabels = new List<string>
                { "Food", "Misc.", "Building", "Furniture" };
            
            foreach (var piece in piecetable.m_pieces)
            {
                PieceManager.Instance.AddPiece(new CustomPiece(piece, piecetable.name, false));
            }
        }

        private static RequirementConfig[] ParseRecipe(string recipe)
        {
            return recipe.Split(',')
                .Select(part => part.Split(':').Select(s => s.Trim()).ToArray())
                .Where(p => (p.Length == 2 || p.Length == 3) && p.Skip(1).All(n => int.TryParse(n, out _)))
                .Select(p => new RequirementConfig(p[0], int.Parse(p[1]), p.Length == 3 ? int.Parse(p[2]) : 0))
                .ToArray();
        }

        // Applies a changed or server-synced recipe to the hammer that is already in the item database
        private void UpdateRecipe()
        {
            Recipe recipe = _hammer.Recipe?.Recipe;
            if (!ObjectDB.instance || !recipe)
            {
                return;
            }
            recipe.m_resources = ParseRecipe(_recipe.Value)
                .Select(r => new Piece.Requirement { m_resItem = ObjectDB.instance.GetItemPrefab(r.Item)?.GetComponent<ItemDrop>(), m_amount = r.Amount, m_amountPerLevel = r.AmountPerLevel })
                .Where(r => r.m_resItem)
                .ToArray();
        }

        // Valheim 1.0 groups the build menu by usage tags instead of category tabs. Jotunn skips tagging
        // pieces that already sit in the bundle's piece table, so tag them here after the old tabs,
        // plus Jotunn's guesses for walls, floors, roofs, stairs, doors and storage.
        private void TagPieces()
        {
            foreach (var prefab in _pieceTable.m_pieces)
            {
                Piece piece = prefab.GetComponent<Piece>();
                if (piece.m_usage != 0)
                {
                    continue;
                }
                var guessed = PieceManager.Instance.FindUsageTagFlags(piece, true) & ~PieceManager.Instance.FindUsageTagFlags(piece, false);
                piece.m_usage = TabUsage(piece) | (guessed & ~Piece.UsageTagFlags.Furniture);
            }
        }

        private static Piece.UsageTagFlags TabUsage(Piece piece)
        {
            switch (piece.m_category)
            {
                case Piece.PieceCategory.Misc:
                    return Piece.UsageTagFlags.Food;
                case Piece.PieceCategory.Crafting:
                    // The glass set only sat in "Misc." to fit the old tab size
                    return piece.name.Contains("Glass") ? Piece.UsageTagFlags.Building : Piece.UsageTagFlags.Misc;
                case Piece.PieceCategory.BuildingWorkbench:
                    return Piece.UsageTagFlags.Building;
                default:
                    return Piece.UsageTagFlags.Furniture;
            }
        }
    }
}