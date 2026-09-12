using HarmonyLib;
using System;
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

            if (ZNetScene.instance)
                instance.StartCoroutine(PatchPieces());

            Piece.s_allPieces?.Do(piece => PatchPiece(piece));

            RefreshBuildUi();
        }

        private static void RefreshBuildUi()
        {
            Player player = Player.m_localPlayer;
            if (player == null)
                return;

            player.UpdateAvailablePiecesList();

            BuildUi buildUi = Hud.instance?.m_buildUi;
            if (buildUi != null && buildUi.gameObject.activeSelf)
                buildUi.UpdateTagButtons(refreshOnly: true);
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

        private static ZNetScene fallbackScene;
        private static readonly Dictionary<string, Piece> fallbackPieces = new Dictionary<string, Piece>(StringComparer.Ordinal);

        private static Piece FindDefaultPiece(string name)
        {
            ZNetScene scene = ZNetScene.instance;
            if (fallbackScene != scene)
            {
                fallbackScene = scene;
                fallbackPieces.Clear();
            }

            GameObject prefab = scene.GetPrefab(name);
            if (prefab)
                return prefab.GetComponent<Piece>();
            if (fallbackPieces.TryGetValue(name, out Piece cached) && cached)
                return cached;

            // Keep positive results only. A late-registered prefab must not be hidden by a
            // cached miss. Warm other names encountered before the requested match, without
            // retaining an unbounded collection or walking past an already found prefab.
            Piece result = null;
            foreach (Piece candidate in Resources.FindObjectsOfTypeAll<Piece>())
            {
                if (!candidate)
                    continue;
                string candidateName = candidate.name;
                if (fallbackPieces.Count < 4096 && (!fallbackPieces.TryGetValue(candidateName, out Piece existing) || !existing))
                    fallbackPieces[candidateName] = candidate;
                if (candidateName == name)
                {
                    result = candidate;
                    break;
                }
            }
            return result;
        }

        private static void PatchPiece(Piece piece)
        {
            if (piece == null || !ZNetScene.instance)
                return;

            string name = Utils.GetPrefabName(piece.gameObject);
            if (!defaultPieceData.TryGetValue(name, out CustomPieceData defaults))
            {
                Piece defaultPiece = FindDefaultPiece(name);
                if (!defaultPiece)
                    return;
                defaults = new CustomPieceData(defaultPiece);
                defaultPieceData[name] = defaults;
            }

            pieceData.TryGetValue(name, out CustomPieceData configured);
            // The configured resource list replaces the full default list, so do not build
            // and immediately discard a second set of requirements on every Piece.Awake.
            defaults.PatchPiece(piece, skipResources: configured?.resources != null);

            if (configured != null)
            {
                if (loggingEnabled.Value)
                    LogInfo($"Patching {piece.name}");
                configured.PatchPiece(piece);
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

            foreach (GameObject go in CustomPieceData.GetBuildPieces())
                if (go != null && go.TryGetComponent(out Piece piece))
                    PatchPiece(piece);

            // Refresh after the prefab usage tags have changed, not only before this coroutine starts.
            RefreshBuildUi();
            DocGen.GenerateDocumentationFile();
        }

        [HarmonyPatch(typeof(ZNetScene), nameof(ZNetScene.OnDestroy))]
        private static class ZNetScene_OnDestroy_ClearFallbackPieces
        {
            private static void Postfix()
            {
                fallbackPieces.Clear();
                fallbackScene = null;
            }
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
            private static void Postfix(PieceTable __instance)
            {
                if (!modEnabled.Value)
                    return;

                // Keep the shared prefab registry intact while filtering all availability views.
                int removed = __instance.m_availablePieces.RemoveWhere(GlobalPatches.IsPieceForceDisabled);
                __instance.m_enabledPieces.RemoveWhere(GlobalPatches.IsPieceForceDisabled);
                foreach (List<Piece> category in __instance.m_availablePiecesByCategory)
                    category.RemoveAll(GlobalPatches.IsPieceForceDisabled);

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
                return new HashSet<string>(configString.Split(',').Select(p => p.Trim()).Where(p => !string.IsNullOrWhiteSpace(p)), StringComparer.OrdinalIgnoreCase);
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
                listDisabled.Count > 0 && listDisabled.Contains(Utils.GetPrefabName(piece.gameObject));
            public static bool IsPieceForceDisabled(GameObject gameObject) => gameObject != null &&
                gameObject.TryGetComponent(out Piece piece) && IsPieceForceDisabled(piece);

            public static void PatchGlobalProperties(Piece piece, string pieceName)
            {
                string name = pieceName;

                if (clipEverything)
                    piece.m_clipEverything = true;
                else if (listClipEverything.Contains(name))
                {
                    if (loggingEnabled.Value)
                        LogInfo($"Patching {pieceName} clip everything");
                    piece.m_clipEverything = true;
                }

                if (allowedInDungeons)
                    piece.m_allowedInDungeons = true;
                else if (listAllowedInDungeons.Contains(name))
                {
                    if (loggingEnabled.Value)
                        LogInfo($"Patching {pieceName} allowed in dungeons");
                    piece.m_allowedInDungeons = true;
                }

                if (repairPiece)
                    piece.m_repairPiece = true;
                else if (listRepairPiece.Contains(name))
                {
                    if (loggingEnabled.Value)
                        LogInfo($"Patching {pieceName} can be repaired");
                    piece.m_repairPiece = true;
                }

                if (canBeRemoved)
                    piece.m_canBeRemoved = true;
                else if (listCanBeRemoved.Contains(name))
                {
                    if (loggingEnabled.Value)
                        LogInfo($"Patching {pieceName} can be removed");
                    piece.m_canBeRemoved = true;
                }

                if (listDisabled.Contains(name))
                    piece.m_enabled = false;

                WearNTear wnt = GetWearNTearComponent(piece);
                if (wnt != null)
                {
                    if (ashDamageImmune)
                        wnt.m_ashDamageImmune = true;
                    else if (listAshDamageImmune.Contains(name))
                    {
                        if (loggingEnabled.Value)
                            LogInfo($"Patching {pieceName} ash and lava immune");
                        wnt.m_ashDamageImmune = true;
                    }

                    if (noRoofWear)
                        wnt.m_noRoofWear = false;
                    else if (listNoRoofWear.Contains(name))
                    {
                        if (loggingEnabled.Value)
                            LogInfo($"Patching {pieceName} no water damage");
                        wnt.m_noRoofWear = false;
                    }

                    if (noSupportWear)
                        wnt.m_noSupportWear = false;
                    else if (listNoSupportWear.Contains(name))
                    {
                        if (loggingEnabled.Value)
                            LogInfo($"Patching {pieceName} no structural integrity");
                        wnt.m_noSupportWear = false;
                    }

                    if (isRoof)
                        wnt.transform.root.GetComponentsInChildren<Collider>(includeInactive: true).Where(col => col.tag == "leaky").Do(col => col.tag = "roof");
                    else if (listIsRoof.Contains(name))
                    {
                        if (loggingEnabled.Value)
                            LogInfo($"Patching {pieceName} leaky -> roof");
                        wnt.transform.root.GetComponentsInChildren<Collider>(includeInactive: true).Where(col => col.tag == "leaky").Do(col => col.tag = "roof");
                    }

                    if (isLeaky)
                        wnt.transform.root.GetComponentsInChildren<Collider>(includeInactive: true).Where(col => col.tag == "roof").Do(col => col.tag = "leaky");
                    else if (listIsLeaky.Contains(name))
                    {
                        if (loggingEnabled.Value)
                            LogInfo($"Patching {pieceName} roof -> leaky");
                        wnt.transform.root.GetComponentsInChildren<Collider>(includeInactive: true).Where(col => col.tag == "roof").Do(col => col.tag = "leaky");
                    }
                }
            }
        }
    }
}
