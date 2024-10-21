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

                if (prefab == null || !prefab.TryGetComponent(out Piece defaultPiece))
                    return;

                defaultPieceData[name] = new CustomPieceData(defaultPiece);
            }

            defaultPieceData[name].PatchPiece(piece);

            if (pieceData.ContainsKey(name))
            {
                LogInfo($"Patching {piece.name}");
                pieceData[name].PatchPiece(piece);
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

            foreach (GameObject go in CustomPieceData.GetBuildPieces())
                if (go != null && go.TryGetComponent(out Piece piece))
                    PatchPiece(piece);
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

        public static class GlobalPatches
        {
            public const string allPiecesIdentifier = "AllPieces";

            private static bool clipEverything;
            private static bool allowedInDungeons;
            private static bool repairPiece;
            private static bool canBeRemoved;
            private static bool isRoof;
            private static bool ashDamageImmune;
            private static bool noRoofWear;
            private static bool noSupportWear;

            private static HashSet<string> listClipEverything;
            private static HashSet<string> listAllowedInDungeons;
            private static HashSet<string> listRepairPiece;
            private static HashSet<string> listCanBeRemoved;
            private static HashSet<string> listIsRoof;
            private static HashSet<string> listAshDamageImmune;
            private static HashSet<string> listNoRoofWear;
            private static HashSet<string> listNoSupportWear;

            private static HashSet<string> ConfigToHashSet(string configString)
            {
                return new HashSet<string>(configString.Split(',').Select(p => p.Trim().ToLower()).Where(p => !string.IsNullOrWhiteSpace(p)).ToList());
            }

            public static void UpdateProperties()
            {
                listClipEverything = ConfigToHashSet(prefabListClipEverything.Value);
                listAllowedInDungeons = ConfigToHashSet(prefabListAllowedInDungeons.Value);
                listRepairPiece = ConfigToHashSet(prefabListRepairPiece.Value);
                listCanBeRemoved = ConfigToHashSet(prefabListCanBeRemoved.Value);
                listIsRoof = ConfigToHashSet(prefabListIsRoof.Value);
                listAshDamageImmune = ConfigToHashSet(prefabListAshDamageImmune.Value);
                listNoRoofWear = ConfigToHashSet(prefabListNoRoofWear.Value);
                listNoSupportWear = ConfigToHashSet(prefabListNoSupportWear.Value);

                clipEverything = listClipEverything.Contains(allPiecesIdentifier);
                allowedInDungeons = listAllowedInDungeons.Contains(allPiecesIdentifier);
                repairPiece = listRepairPiece.Contains(allPiecesIdentifier);
                canBeRemoved = listCanBeRemoved.Contains(allPiecesIdentifier);
                isRoof = listIsRoof.Contains(allPiecesIdentifier);
                ashDamageImmune = listAshDamageImmune.Contains(allPiecesIdentifier);
                noRoofWear = listNoRoofWear.Contains(allPiecesIdentifier);
                noSupportWear = listNoSupportWear.Contains(allPiecesIdentifier);
            }

            public static void PatchGlobalProperties(Piece piece, string pieceName)
            {
                string name = pieceName.ToLower();

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
                }
            }
        }
    }
}
