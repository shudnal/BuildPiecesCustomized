﻿using HarmonyLib;
using System;
using System.Collections.Generic;
using System.IO;
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
            File.WriteAllText(Path.Combine(pluginDirectory.FullName, filename), GetFileText());
        }

        private static string GetFileText()
        {
            sb.Clear();

            sb.AppendLine("This documentation generated automatically. It contains all available pieces and enumerations identifiers used to configure pieces.");
            sb.AppendLine();

            sb.AppendLine("# Properties and available values");
            sb.AppendLine();

            sb.AppendLine("## category");
            EnumToList(typeof(Piece.PieceCategory));

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
            EnumToList(typeof(HitData.DamageModifier), noID: true);

            sb.AppendLine();
            sb.AppendLine("# Piece prefab names");
            sb.AppendLine("Format \"Prefab name - Token - Localized name\"");
            sb.AppendLine("List given in order as it appears in the game");

            if ((bool)ObjectDB.instance)
            {
                foreach (ItemDrop tool in ObjectDB.instance.GetAllItems(ItemDrop.ItemData.ItemType.Tool, ""))
                {
                    if (tool.m_itemData.m_shared.m_buildPieces?.m_pieces?.Count == 0)
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

                DocGen.GenerateDocumentationFile();
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

                DocGen.GenerateDocumentationFile();
            }
        }
    }
}
