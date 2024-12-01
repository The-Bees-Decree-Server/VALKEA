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
    [BepInDependency(Jotunn.Main.ModGuid)]
    [BepInPlugin(ModGUID, ModName, ModVersion)]
    [NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod, VersionStrictness.Major)]
    public class  VALKEAplugin: BaseUnityPlugin
    {
        internal const string ModName = "VALKEA";
        internal const string ModVersion = "3.0.0";
        internal const string Author = "TheBeesDecree";
        private const string ModGUID = Author + "." + ModName;

        private readonly Harmony _harmony = new(ModGUID);

        public static readonly ManualLogSource VALKEALogger =
            BepInEx.Logging.Logger.CreateLogSource(ModName);

        public void Awake()
        {
            AddPieces();

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
    }
}