using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using Jotunn.Configs;
using Jotunn.Entities;
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
        internal const string ModVersion = "3.0.1";
        internal const string Author = "TheBeesDecree";
        private const string ModGUID = Author + "." + ModName;

        private readonly Harmony _harmony = new(ModGUID);

        public static readonly ManualLogSource VALKEALogger =
            BepInEx.Logging.Logger.CreateLogSource(ModName);

        private PieceTable _pieceTable;

        public void Awake()
        {
            AddPieces();
            PieceManager.OnPiecesRegistered += TagPieces;

            Assembly assembly = Assembly.GetExecutingAssembly();
            _harmony.PatchAll(assembly);
        }

        private void AddPieces()
        {
            string assetBundleName = "bjorksnas";
            AssetBundle bundle = AssetUtils.LoadAssetBundleFromResources(assetBundleName, Assembly.GetExecutingAssembly());
            var objects = bundle.LoadAllAssets<GameObject>();

            // Add Custom hammer
            GameObject bjorksnas_go = bundle.LoadAsset<GameObject>("BJORKSNAS");
            ItemConfig bjorksnas_ic = new ItemConfig();
            bjorksnas_ic.Name = "BJORKSNAS";
            bjorksnas_ic.Description = "Legend has it that BJÖRKSNÄS is the holy key needed to assemble all goods from VALKEA";
            bjorksnas_ic.CraftingStation = CraftingStations.Workbench;
            bjorksnas_ic.Requirements = new[]
            {
                new RequirementConfig() { Item = "FineWood", Amount = 1 },
                new RequirementConfig() { Item = "QueenBee", Amount = 1 },
                new RequirementConfig() { Item = "Coins", Amount = 999, AmountPerLevel = 99 }
            };
            ItemManager.Instance.AddItem(new CustomItem(bjorksnas_go, false, bjorksnas_ic));

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