using System;
using System.Collections.Generic;
using UnityEngine;
using static BuildPiecesCustomized.BuildPiecesCustomized;

namespace BuildPiecesCustomized
{
    internal static class PieceTableCategories
    {
        private static readonly HashSet<Piece.PieceCategory> registeredCategories = new HashSet<Piece.PieceCategory>();
        private static readonly HashSet<string> reportedCategories = new HashSet<string>(StringComparer.Ordinal);
        private static int lastRefreshFrame = -1;

        internal static void RefreshRegisteredCategories()
        {
            registeredCategories.Clear();
            lastRefreshFrame = Time.frameCount;

            // Category providers can extend the enum values or register categories directly on piece tables.
            foreach (Piece.PieceCategory category in Enum.GetValues(typeof(Piece.PieceCategory)))
                RegisterCategory(category);

            foreach (PieceTable table in Resources.FindObjectsOfTypeAll<PieceTable>())
            {
                if (table == null || table.m_categories == null)
                    continue;

                foreach (Piece.PieceCategory category in table.m_categories)
                    RegisterCategory(category);
            }
        }

        private static void RegisterCategory(Piece.PieceCategory category)
        {
            if ((int)category >= 0 && (int)category < int.MaxValue && category != Piece.PieceCategory.Max)
                registeredCategories.Add(category);
        }

        internal static void ApplyConfiguredCategory(Piece piece, Piece.PieceCategory category)
        {
            int index = (int)category;
            bool isNative = index >= 0 && index < (int)Piece.PieceCategory.Max;
            bool isCustom = index > (int)Piece.PieceCategory.Max && index < int.MaxValue && category != Piece.PieceCategory.All;

            if (isCustom && !registeredCategories.Contains(category) && lastRefreshFrame != Time.frameCount)
                RefreshRegisteredCategories();

            if (isNative || category == Piece.PieceCategory.All || (isCustom && registeredCategories.Contains(category)))
            {
                piece.m_category = category;
                return;
            }

            string pieceName = Utils.GetPrefabName(piece.gameObject);
            if (reportedCategories.Add(pieceName + ":" + index))
                LogWarning($"Ignoring unregistered or reserved category {index} for piece '{pieceName}'. " +
                    "Keep a built-in category, All (100), or a category registered by an installed mod. " +
                    $"The existing category {(int)piece.m_category} is unchanged.");
        }

        internal static void EnsureStorage(PieceTable table)
        {
            if (table == null)
                return;

            int required = (int)Piece.PieceCategory.Max;
            foreach (Piece.PieceCategory category in Enum.GetValues(typeof(Piece.PieceCategory)))
                IncludeCategory(category, ref required);

            if (table.m_categories != null)
                foreach (Piece.PieceCategory category in table.m_categories)
                    IncludeCategory(category, ref required);

            if (table.m_pieces != null)
                foreach (GameObject prefab in table.m_pieces)
                    if (prefab != null && prefab.TryGetComponent(out Piece piece))
                        IncludeCategory(piece.m_category, ref required);

            required = Math.Max(required, table.m_availablePiecesByCategory?.Count ?? 0);
            required = Math.Max(required, table.m_selectedPiece?.Length ?? 0);
            required = Math.Max(required, table.m_lastSelectedPiece?.Length ?? 0);

            if (table.m_availablePiecesByCategory == null)
                table.m_availablePiecesByCategory = new List<List<Piece>>();

            while (table.m_availablePiecesByCategory.Count < required)
                table.m_availablePiecesByCategory.Add(new List<Piece>());

            for (int i = 0; i < table.m_availablePiecesByCategory.Count; i++)
                if (table.m_availablePiecesByCategory[i] == null)
                    table.m_availablePiecesByCategory[i] = new List<Piece>();

            // Grow all three structures together without discarding existing selections or custom categories.
            if (table.m_selectedPiece == null || table.m_selectedPiece.Length < required)
                Array.Resize(ref table.m_selectedPiece, required);
            if (table.m_lastSelectedPiece == null || table.m_lastSelectedPiece.Length < required)
                Array.Resize(ref table.m_lastSelectedPiece, required);

            // Max and All are selection sentinels resolved by GetSelectedCategory; do not turn them into real tabs.
            if (table.m_selectedCategory != Piece.PieceCategory.Max && table.m_selectedCategory != Piece.PieceCategory.All &&
                ((int)table.m_selectedCategory < 0 || (int)table.m_selectedCategory >= required))
                table.m_selectedCategory = Piece.PieceCategory.Max;
        }

        private static void IncludeCategory(Piece.PieceCategory category, ref int required)
        {
            int index = (int)category;
            // All is a broadcast category, not a storage index. Keep space for the Max sentinel because category
            // providers may count it while expanding the game's loops, even though configs cannot select it as a tab.
            if (index >= 0 && index < int.MaxValue && category != Piece.PieceCategory.All)
                required = Math.Max(required, index + 1);
        }
    }
}
