using HarmonyLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using static BuildPiecesCustomized.BuildPiecesCustomized;

namespace BuildPiecesCustomized
{
    internal static class DocGen
    {
        private static readonly StringBuilder sb = new StringBuilder();
        internal const string filename = "Pieces and properties.md";

        internal static void GenerateDocumentationFile()
        {
            string file = Path.Combine(configDirectory.FullName, filename);
            try
            {
                Directory.CreateDirectory(configDirectory.FullName);
                File.WriteAllText(file, GetFileText());
            }
            catch (Exception e)
            {
                LogWarning($"Error when writing file ({file})! Error: {e.Message}");
            }
        }

        private static void LogPieceCategories()
        {
            var categories = new Dictionary<Piece.PieceCategory, string>();

            foreach (Piece.PieceCategory category in Enum.GetValues(typeof(Piece.PieceCategory)))
                if ((int)category >= 0 && category != Piece.PieceCategory.Max)
                    categories[category] = category.ToString();

            // Documentation must not switch the player's build tool or execute HUD updates for inactive tables.
            foreach (PieceTable table in Resources.FindObjectsOfTypeAll<PieceTable>())
            {
                if (table == null || table.m_categories == null)
                    continue;

                for (int i = 0; i < table.m_categories.Count; i++)
                {
                    Piece.PieceCategory category = table.m_categories[i];
                    if ((int)category < 0 || category == Piece.PieceCategory.Max)
                        continue;

                    string label = table.m_categoryLabels != null && i < table.m_categoryLabels.Count
                        ? table.m_categoryLabels[i]
                        : null;

                    if (!string.IsNullOrWhiteSpace(label) && Localization.instance != null)
                        label = Localization.instance.Localize(label);

                    if (!string.IsNullOrWhiteSpace(label))
                        categories[category] = label;
                    else if (!categories.ContainsKey(category))
                        categories[category] = category.ToString();
                }
            }

            foreach (var pair in categories.OrderBy(p => (int)p.Key))
                sb.AppendLine($"* {(int)pair.Key} - {pair.Value}");
        }

        private static string GetFileText()
        {
            sb.Clear();

            sb.AppendLine("This documentation generated automatically. It contains all available pieces and enumerations identifiers used to configure pieces.");
            sb.AppendLine();

            sb.AppendLine("# Properties and available values");
            sb.AppendLine();

            sb.AppendLine("## category");
            LogPieceCategories();

            sb.AppendLine();
            sb.AppendLine("## comfortGroup");
            EnumToList(typeof(Piece.ComfortGroup));

            sb.AppendLine();
            sb.AppendLine("## onlyInBiome");
            EnumToList(typeof(Heightmap.Biome));

            sb.AppendLine();
            sb.AppendLine("## materialType");
            EnumToList(typeof(WearNTear.MaterialType));

            sb.AppendLine();
            sb.AppendLine("## damageModifiers");
            sb.AppendLine();
            sb.AppendLine("### type");
            EnumToList(typeof(HitData.DamageType), noID:true);

            sb.AppendLine();
            sb.AppendLine("### modifier");
            EnumToList(typeof(HitData.DamageModifier), noID:true);

            if ((bool)ObjectDB.instance)
            {
                sb.AppendLine();
                sb.AppendLine("## station");

                Dictionary<string, CraftingStation> stations = new Dictionary<string, CraftingStation>();
                foreach (Recipe recipe in ObjectDB.instance.m_recipes)
                {
                    if (recipe == null || recipe.m_craftingStation == null)
                        continue;

                    stations[recipe.m_craftingStation.name] = recipe.m_craftingStation;
                }

                foreach (KeyValuePair<string, CraftingStation> station in stations)
                    sb.AppendLine($"* {station.Key} - {Localization.instance.Localize(station.Value.m_name)}");

                sb.AppendLine();
                sb.AppendLine("# Piece prefab names");
                sb.AppendLine("Format \"Prefab name - Token - Localized name\"");
                sb.AppendLine("List given in order as it appears in the game");

                foreach (ItemDrop tool in ObjectDB.instance.GetAllItems(ItemDrop.ItemData.ItemType.Tool, ""))
                {
                    if (tool == null || tool.m_itemData.m_shared.m_buildPieces == null || tool.m_itemData.m_shared.m_buildPieces.m_pieces.Count == 0)
                        continue;

                    sb.AppendLine();
                    sb.AppendLine($"## {tool.name} - {tool.m_itemData.m_shared.m_name} - {Localization.instance.Localize(tool.m_itemData.m_shared.m_name)}");

                    foreach (GameObject item in tool.m_itemData.m_shared.m_buildPieces.m_pieces)
                    {
                        if (!item.TryGetComponent(out Piece piece))
                            continue;

                        sb.AppendLine($"* {piece.name} - {piece.m_name} - {Localization.instance.Localize(piece.m_name)}");
                    }
                }
            }

            return sb.ToString();
        }

        private static void EnumToList(Type enumType, bool noID = false)
        {
            foreach (var value in Enum.GetValues(enumType))
                sb.AppendLine(noID ? $"* {value}" : $"* {(int)value} - {value}");
        }

        [HarmonyPatch(typeof(ObjectDB), nameof(ObjectDB.Awake))]
        [HarmonyPriority(Priority.Last)]
        private static class ObjectDB_Awake_DocGen
        {
            private static void Postfix()
            {
                if (!modEnabled.Value)
                    return;

                GenerateDocumentationFile();
            }
        }

        [HarmonyPatch(typeof(ObjectDB), nameof(ObjectDB.CopyOtherDB))]
        [HarmonyPriority(Priority.Last)]
        private static class ObjectDB_CopyOtherDB_DocGen
        {
            private static void Postfix()
            {
                if (!modEnabled.Value)
                    return;

                GenerateDocumentationFile();
            }
        }

        [HarmonyPatch(typeof(ZoneSystem), nameof(ZoneSystem.Start))]
        [HarmonyPriority(Priority.Last)]
        private static class ZoneSystem_Start_DocGen
        {
            private static void Postfix()
            {
                if (!modEnabled.Value)
                    return;

                GenerateDocumentationFile();
            }
        }


    }
}
