using HarmonyLib;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static BuildPiecesCustomized.BuildPiecesCustomized;

namespace BuildPiecesCustomized
{
    public static class PiecePatches
    {
        public static void UpdatePiecesProperties()
        {
            GlobalPatches.UpdateProperties();
            PieceTableCategories.RefreshRegisteredCategories();

            if (ZNetScene.instance)
                instance.StartCoroutine(PatchPieces());

            Piece.s_allPieces?.Do(piece => PatchPiece(piece));

            Player.m_localPlayer?.UpdateAvailablePiecesList();
        }

        private static void FillCraftingStations()
        {
            craftingStations.Clear();
            foreach (Recipe recipe in ObjectDB.instance.m_recipes)
            {
                if (recipe?.m_craftingStation == null)
                    continue;

                if (craftingStations.ContainsKey(recipe.m_craftingStation.name))
                    continue;

                craftingStations[recipe.m_craftingStation.name] = recipe.m_craftingStation;
                craftingStations[recipe.m_craftingStation.m_name] = recipe.m_craftingStation;
                craftingStations[recipe.m_craftingStation.m_name.Substring(1)] = recipe.m_craftingStation;
            }
        }

        private static void PatchPiece(Piece piece)
        {
            if (piece == null || !ZNetScene.instance)
                return;

            string name = Utils.GetPrefabName(piece.gameObject);
            if (!defaultPieceData.ContainsKey(name))
            {
                GameObject prefab = ZNetScene.instance.GetPrefab(name);

                Piece defaultPiece = prefab == null ? Resources.FindObjectsOfTypeAll<Piece>().FirstOrDefault(p => p.name == name) : prefab.GetComponent<Piece>();
                if (!(bool)defaultPiece)
                    return;

                defaultPieceData[name] = new CustomPieceData(defaultPiece);
            }

            defaultPieceData[name].PatchPiece(piece);

            if (pieceData.ContainsKey(name))
            {
                LogInfo($"Patching {piece.name}");
                pieceData[name].PatchPiece(piece, validateCategory: true);
            }

            GlobalPatches.PatchGlobalProperties(piece, name);

            if (piece.m_nview != null && piece.m_nview.IsValid())
                piece.m_nview.LoadFields();
        }

        private static IEnumerator PatchPieces()
        {
            yield return new WaitUntil(() => ObjectDB.instance != null);

            FillCraftingStations();

            yield return new WaitForFixedUpdate();

            if (!modEnabled.Value || !ObjectDB.instance || !ZNetScene.instance)
                yield break;

            PieceTableCategories.RefreshRegisteredCategories();

            foreach (GameObject go in CustomPieceData.GetBuildPieces())
                if (go != null && go.TryGetComponent(out Piece piece))
                    PatchPiece(piece);

            // Refresh after the prefab categories have changed, not only before this coroutine starts.
            Player.m_localPlayer?.UpdateAvailablePiecesList();
        }

        [HarmonyPatch(typeof(Piece), nameof(Piece.Awake))]
        private static class Piece_Awake_PatchPiece
        {
            private static void Postfix(Piece __instance)
            {
                if (!modEnabled.Value)
                    return;

                PatchPiece(__instance);
            }
        }

        [HarmonyPatch(typeof(PieceTable), nameof(PieceTable.IsPieceAvailable))]
        private static class PieceTable_IsPieceAvailable_PieceDisabled
        {
            private static void Postfix(PieceTable __instance, Piece piece, ref bool __result)
            {
                if (!modEnabled.Value)
                    return;

                if (__result && GlobalPatches.IsPieceForceDisabled(piece))
                    __result = false;
            }
        }

        [HarmonyPatch(typeof(PieceTable), nameof(PieceTable.UpdateAvailable))]
        private static class PieceTable_UpdateAvailable_PieceDisabled
        {
            [HarmonyPriority(Priority.Last)]
            private static void Prefix(PieceTable __instance)
            {
                if (modEnabled.Value)
                    PieceTableCategories.EnsureStorage(__instance);
            }

            [HarmonyPriority(Priority.Last)]
            private static void Postfix(PieceTable __instance)
            {
                if (!modEnabled.Value)
                    return;

                // Keep the shared prefab registry intact. Valheim 1.0.7 builds all three availability views.
                int removed = __instance.m_availablePieces.RemoveWhere(GlobalPatches.IsPieceForceDisabled);
                __instance.m_enabledPieces.RemoveWhere(GlobalPatches.IsPieceForceDisabled);
                foreach (List<Piece> category in __instance.m_availablePiecesByCategory)
                    category.RemoveAll(GlobalPatches.IsPieceForceDisabled);

                PieceTableCategories.EnsureStorage(__instance);

                if (removed > 0)
                    LogInfo($"Hidden pieces {__instance.name}: {removed}");
            }
        }

        public static class GlobalPatches
        {
            public const string allPiecesIdentifier = "AllPieces";
            private static readonly string allPiecesListIdentifier = allPiecesIdentifier.ToLowerInvariant();

            private static bool clipEverything;
            private static bool allowedInDungeons;
            private static bool repairPiece;
            private static bool canBeRemoved;
            private static bool isRoof;
            private static bool isLeaky;
            private static bool ashDamageImmune;
            private static bool noRoofWear;
            private static bool noSupportWear;

            private static HashSet<string> listClipEverything;
            private static HashSet<string> listAllowedInDungeons;
            private static HashSet<string> listRepairPiece;
            private static HashSet<string> listCanBeRemoved;
            private static HashSet<string> listIsRoof;
            private static HashSet<string> listIsLeaky;
            private static HashSet<string> listAshDamageImmune;
            private static HashSet<string> listNoRoofWear;
            private static HashSet<string> listNoSupportWear;
            private static HashSet<string> listDisabled;

            private static HashSet<string> ConfigToHashSet(string configString)
            {
                return new HashSet<string>(configString.Split(',').Select(p => p.Trim().ToLowerInvariant()).Where(p => !string.IsNullOrWhiteSpace(p)).ToList());
            }

            public static void UpdateProperties()
            {
                listClipEverything = ConfigToHashSet(prefabListClipEverything.Value);
                listAllowedInDungeons = ConfigToHashSet(prefabListAllowedInDungeons.Value);
                listRepairPiece = ConfigToHashSet(prefabListRepairPiece.Value);
                listCanBeRemoved = ConfigToHashSet(prefabListCanBeRemoved.Value);
                listIsRoof = ConfigToHashSet(prefabListIsRoof.Value);
                listIsLeaky = ConfigToHashSet(prefabListIsLeaky.Value);
                listAshDamageImmune = ConfigToHashSet(prefabListAshDamageImmune.Value);
                listNoRoofWear = ConfigToHashSet(prefabListNoRoofWear.Value);
                listNoSupportWear = ConfigToHashSet(prefabListNoSupportWear.Value);
                listDisabled = ConfigToHashSet(prefabListDisabled.Value);

                clipEverything = listClipEverything.Contains(allPiecesListIdentifier);
                allowedInDungeons = listAllowedInDungeons.Contains(allPiecesListIdentifier);
                repairPiece = listRepairPiece.Contains(allPiecesListIdentifier);
                canBeRemoved = listCanBeRemoved.Contains(allPiecesListIdentifier);
                isRoof = listIsRoof.Contains(allPiecesListIdentifier);
                isLeaky = listIsLeaky.Contains(allPiecesListIdentifier);
                ashDamageImmune = listAshDamageImmune.Contains(allPiecesListIdentifier);
                noRoofWear = listNoRoofWear.Contains(allPiecesListIdentifier);
                noSupportWear = listNoSupportWear.Contains(allPiecesListIdentifier);
            }

            public static bool IsPieceForceDisabled(Piece piece) => piece != null && listDisabled != null &&
                listDisabled.Contains(Utils.GetPrefabName(piece.gameObject).ToLowerInvariant());
            public static bool IsPieceForceDisabled(GameObject gameObject) => gameObject != null &&
                gameObject.TryGetComponent(out Piece piece) && IsPieceForceDisabled(piece);

            public static void PatchGlobalProperties(Piece piece, string pieceName)
            {
                string name = pieceName.ToLowerInvariant();

                if (clipEverything)
                    piece.m_clipEverything = true;
                else if (listClipEverything.Contains(name))
                {
                    LogInfo($"Patching {pieceName} clip everything");
                    piece.m_clipEverything = true;
                }

                if (allowedInDungeons)
                    piece.m_allowedInDungeons = true;
                else if (listAllowedInDungeons.Contains(name))
                {
                    LogInfo($"Patching {pieceName} allowed in dungeons");
                    piece.m_allowedInDungeons = true;
                }

                if (repairPiece)
                    piece.m_repairPiece = true;
                else if (listRepairPiece.Contains(name))
                {
                    LogInfo($"Patching {pieceName} can be repaired");
                    piece.m_repairPiece = true;
                }

                if (canBeRemoved)
                    piece.m_canBeRemoved = true;
                else if (listCanBeRemoved.Contains(name))
                {
                    LogInfo($"Patching {pieceName} can be removed");
                    piece.m_canBeRemoved = true;
                }

                if (listDisabled.Contains(name))
                {
                    piece.m_enabled = false;
                    piece.m_category = 0;
                }
                else if (pieceCategories.Value.TryGetValue(name, out int category))
                    PieceTableCategories.ApplyConfiguredCategory(piece, (Piece.PieceCategory)category);

                WearNTear wnt = GetWearNTearComponent(piece);
                if (wnt != null)
                {
                    if (ashDamageImmune)
                        wnt.m_ashDamageImmune = true;
                    else if (listAshDamageImmune.Contains(name))
                    {
                        LogInfo($"Patching {pieceName} ash and lava immune");
                        wnt.m_ashDamageImmune = true;
                    }

                    if (noRoofWear)
                        wnt.m_noRoofWear = false;
                    else if (listNoRoofWear.Contains(name))
                    {
                        LogInfo($"Patching {pieceName} no water damage");
                        wnt.m_noRoofWear = false;
                    }

                    if (noSupportWear)
                        wnt.m_noSupportWear = false;
                    else if (listNoSupportWear.Contains(name))
                    {
                        LogInfo($"Patching {pieceName} no structural integrity");
                        wnt.m_noSupportWear = false;
                    }

                    if (isRoof)
                        wnt.transform.root.GetComponentsInChildren<Collider>(includeInactive: true).Where(col => col.tag == "leaky").Do(col => col.tag = "roof");
                    else if (listIsRoof.Contains(name))
                    {
                        LogInfo($"Patching {pieceName} leaky -> roof");
                        wnt.transform.root.GetComponentsInChildren<Collider>(includeInactive: true).Where(col => col.tag == "leaky").Do(col => col.tag = "roof");
                    }

                    if (isLeaky)
                        wnt.transform.root.GetComponentsInChildren<Collider>(includeInactive: true).Where(col => col.tag == "roof").Do(col => col.tag = "leaky");
                    else if (listIsLeaky.Contains(name))
                    {
                        LogInfo($"Patching {pieceName} roof -> leaky");
                        wnt.transform.root.GetComponentsInChildren<Collider>(includeInactive: true).Where(col => col.tag == "roof").Do(col => col.tag = "leaky");
                    }
                }
            }
        }
    }
}
