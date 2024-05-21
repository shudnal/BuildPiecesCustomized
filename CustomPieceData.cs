using HarmonyLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using static BuildPiecesCustomized.BuildPiecesCustomized;

namespace BuildPiecesCustomized
{
    [Serializable]
    internal class CustomPieceData
    {
        public string name;

        public Piece.PieceCategory category;
        public Piece.ComfortGroup comfortGroup;
        public int comfort;
        public bool groundPiece;
        public bool allowAltGroundPlacement;
        public bool groundOnly;
        public bool cultivatedGroundOnly;
        public bool waterPiece;
        public bool clipGround;
        public bool clipEverything;
        public bool noInWater;
        public bool notOnWood;
        public bool notOnTiltingSurface;
        public bool inCeilingOnly;
        public bool notOnFloor;
        public bool noClipping;
        public bool onlyInTeleportArea;
        public bool allowedInDungeons;
        public float spaceRequirement;
        public bool repairPiece;
        public bool canBeRemoved = true;
        public bool allowRotatedOverlap;
        public bool vegetationGroundOnly;
        public float blockRadius;
        public int extraPlacementDistance;
        public string station = "";
        public Heightmap.Biome onlyInBiome;

        public bool targetNonPlayerBuilt = true;
        public bool primaryTarget;
        public bool randomTarget = true;

        public float health = 100f;
        public bool noRoofWear = true;
        public bool noSupportWear = true;
        public WearNTear.MaterialType materialType;
        public bool supports;
        public Vector3 comOffset = Vector3.zero;
        public float hitNoise;
        public float destroyNoise;
        public bool autoCreateFragments = true;
        public List<string> damageModifiers = new List<string>();

        public bool ashDamageImmune;
        public bool ashDamageResist;
        public bool burnable = true;
        public int minToolTier;
        public bool triggerPrivateArea = true;

        public List<string> resources = new List<string>();

        internal void PatchPiece(Piece piece)
        {
            piece.m_category = category;
            piece.m_comfort = comfort;
            piece.m_comfortGroup = comfortGroup;
            piece.m_groundPiece = groundPiece;
            piece.m_allowAltGroundPlacement = allowAltGroundPlacement;
            piece.m_groundOnly = groundOnly;
            piece.m_cultivatedGroundOnly = cultivatedGroundOnly;
            piece.m_waterPiece = waterPiece;
            piece.m_clipGround = clipGround;
            piece.m_clipEverything = clipEverything;
            piece.m_noInWater = noInWater;
            piece.m_notOnWood = notOnWood;
            piece.m_notOnTiltingSurface = notOnTiltingSurface;
            piece.m_inCeilingOnly = inCeilingOnly;
            piece.m_notOnFloor = notOnFloor;
            piece.m_noClipping = noClipping;
            piece.m_onlyInTeleportArea = onlyInTeleportArea;
            piece.m_allowedInDungeons = allowedInDungeons;
            piece.m_spaceRequirement = spaceRequirement;
            piece.m_repairPiece = repairPiece;
            piece.m_canBeRemoved = canBeRemoved;

            piece.m_allowRotatedOverlap = allowRotatedOverlap;
            piece.m_vegetationGroundOnly = vegetationGroundOnly;
            piece.m_blockRadius = blockRadius;
            piece.m_extraPlacementDistance = extraPlacementDistance;

            piece.m_craftingStation = craftingStations.GetValueSafe(station);
            piece.m_onlyInBiome = onlyInBiome;

            piece.m_targetNonPlayerBuilt = targetNonPlayerBuilt;
            piece.m_primaryTarget = primaryTarget;
            piece.m_randomTarget = randomTarget;

            List<Piece.Requirement> m_resources = new List<Piece.Requirement>();
            foreach (string modString in resources)
            {
                string[] parts = modString.Split(':');

                ItemDrop item = ObjectDB.instance?.GetItemPrefab(parts[0])?.GetComponent<ItemDrop>();
                if (item == null)
                    continue;

                m_resources.Add(new Piece.Requirement()
                {
                    m_resItem = item,
                    m_amount = parts.Length < 2 ? 1 : int.Parse(parts[1]),
                    m_recover = parts.Length < 3 || bool.Parse(parts[2]),
                });
            }

            piece.m_resources = m_resources.ToArray();

            if (piece.TryGetComponent(out WearNTear wnt))
            {
                wnt.m_health = health;
                wnt.m_noRoofWear = noRoofWear;
                wnt.m_noSupportWear = noSupportWear;
                wnt.m_materialType = materialType;
                wnt.m_supports = supports;
                wnt.m_comOffset = comOffset;
                wnt.m_hitNoise = hitNoise;
                wnt.m_destroyNoise = destroyNoise;
                wnt.m_autoCreateFragments = autoCreateFragments;

                wnt.m_ashDamageImmune = ashDamageImmune;
                wnt.m_ashDamageResist = ashDamageResist;
                wnt.m_burnable = burnable;
                wnt.m_minToolTier = minToolTier;
                wnt.m_triggerPrivateArea = triggerPrivateArea;

                foreach (string modString in damageModifiers)
                {
                    string[] parts = modString.Split(':');
                    if (parts.Length != 2)
                        continue;

                    HitData.DamageType type = (HitData.DamageType)Enum.Parse(typeof(HitData.DamageType), parts[0]);
                    HitData.DamageModifier mod = (HitData.DamageModifier)Enum.Parse(typeof(HitData.DamageModifier), parts[1]);
                    switch (type)
                    {
                        case HitData.DamageType.Blunt:
                            wnt.m_damages.m_blunt = mod;
                            break;
                        case HitData.DamageType.Slash:
                            wnt.m_damages.m_slash = mod;
                            break;
                        case HitData.DamageType.Pierce:
                            wnt.m_damages.m_pierce = mod;
                            break;
                        case HitData.DamageType.Chop:
                            wnt.m_damages.m_chop = mod;
                            break;
                        case HitData.DamageType.Pickaxe:
                            wnt.m_damages.m_pickaxe = mod;
                            break;
                        case HitData.DamageType.Fire:
                            wnt.m_damages.m_fire = mod;
                            break;
                        case HitData.DamageType.Frost:
                            wnt.m_damages.m_frost = mod;
                            break;
                        case HitData.DamageType.Lightning:
                            wnt.m_damages.m_lightning = mod;
                            break;
                        case HitData.DamageType.Poison:
                            wnt.m_damages.m_poison = mod;
                            break;
                        case HitData.DamageType.Spirit:
                            wnt.m_damages.m_spirit = mod;
                            break;
                    }
                }

            }
        }

        internal void SaveToDirectory(string directory)
        {
            string filename = Path.Combine(directory, $"{name}.json");

            File.WriteAllText(filename, JsonUtility.ToJson(this, true));
        }

        internal CustomPieceData()
        {

        }

        internal CustomPieceData(Piece piece)
        {
            name = piece.gameObject.name;
            category = piece.m_category;
            comfortGroup = piece.m_comfortGroup;
            comfort = piece.m_comfort;
            groundPiece = piece.m_groundPiece;
            allowAltGroundPlacement = piece.m_allowAltGroundPlacement;
            groundOnly = piece.m_groundOnly;
            cultivatedGroundOnly = piece.m_cultivatedGroundOnly;
            waterPiece = piece.m_waterPiece;
            clipGround = piece.m_clipGround;
            clipEverything = piece.m_clipEverything;
            noInWater = piece.m_noInWater;
            notOnWood = piece.m_notOnWood;
            notOnTiltingSurface = piece.m_notOnTiltingSurface;
            inCeilingOnly = piece.m_inCeilingOnly;
            notOnFloor = piece.m_notOnFloor;
            noClipping = piece.m_noClipping;
            onlyInTeleportArea = piece.m_onlyInTeleportArea;
            allowedInDungeons = piece.m_allowedInDungeons;
            spaceRequirement = piece.m_spaceRequirement;
            repairPiece = piece.m_repairPiece;
            canBeRemoved = piece.m_canBeRemoved;
            station = piece.m_craftingStation ? piece.m_craftingStation.m_name : "";
            onlyInBiome = piece.m_onlyInBiome;

            allowRotatedOverlap = piece.m_allowRotatedOverlap;
            vegetationGroundOnly = piece.m_vegetationGroundOnly;
            blockRadius = piece.m_blockRadius;
            extraPlacementDistance = piece.m_extraPlacementDistance;

            targetNonPlayerBuilt = piece.m_targetNonPlayerBuilt;
            primaryTarget = piece.m_primaryTarget;
            randomTarget = piece.m_randomTarget;

            resources.AddRange(piece.m_resources.Select(req => { return $"{req.m_resItem.name}:{req.m_amount}:{req.m_recover}"; }));

            if (piece.TryGetComponent(out WearNTear wnt))
            {
                health = wnt.m_health;
                noRoofWear = wnt.m_noRoofWear;
                noSupportWear = wnt.m_noSupportWear;
                materialType = wnt.m_materialType;
                supports = wnt.m_supports;
                comOffset = wnt.m_comOffset;
                hitNoise = wnt.m_hitNoise;
                destroyNoise = wnt.m_destroyNoise;
                autoCreateFragments = wnt.m_autoCreateFragments;

                ashDamageImmune = wnt.m_ashDamageImmune;
                ashDamageResist = wnt.m_ashDamageResist;
                burnable = wnt.m_burnable;
                minToolTier = wnt.m_minToolTier;
                triggerPrivateArea = wnt.m_triggerPrivateArea;

                damageModifiers = new List<string>()
                {
                    Enum.GetName(typeof(HitData.DamageType), HitData.DamageType.Blunt) + ":" + Enum.GetName(typeof(HitData.DamageModifier), wnt.m_damages.m_blunt),
                    Enum.GetName(typeof(HitData.DamageType), HitData.DamageType.Slash) + ":" + Enum.GetName(typeof(HitData.DamageModifier), wnt.m_damages.m_slash),
                    Enum.GetName(typeof(HitData.DamageType), HitData.DamageType.Pierce) + ":" + Enum.GetName(typeof(HitData.DamageModifier), wnt.m_damages.m_pierce),
                    Enum.GetName(typeof(HitData.DamageType), HitData.DamageType.Chop) + ":" + Enum.GetName(typeof(HitData.DamageModifier), wnt.m_damages.m_chop),
                    Enum.GetName(typeof(HitData.DamageType), HitData.DamageType.Pickaxe) + ":" + Enum.GetName(typeof(HitData.DamageModifier), wnt.m_damages.m_pickaxe),
                    Enum.GetName(typeof(HitData.DamageType), HitData.DamageType.Fire) + ":" + Enum.GetName(typeof(HitData.DamageModifier), wnt.m_damages.m_fire),
                    Enum.GetName(typeof(HitData.DamageType), HitData.DamageType.Frost) + ":" + Enum.GetName(typeof(HitData.DamageModifier), wnt.m_damages.m_frost),
                    Enum.GetName(typeof(HitData.DamageType), HitData.DamageType.Lightning) + ":" + Enum.GetName(typeof(HitData.DamageModifier), wnt.m_damages.m_lightning),
                    Enum.GetName(typeof(HitData.DamageType), HitData.DamageType.Poison) + ":" + Enum.GetName(typeof(HitData.DamageModifier), wnt.m_damages.m_poison),
                    Enum.GetName(typeof(HitData.DamageType), HitData.DamageType.Spirit) + ":" + Enum.GetName(typeof(HitData.DamageModifier), wnt.m_damages.m_spirit)
                };

            }
        }

        internal static CustomPieceData GetByPieceName(string pieceName)
        {
            if (!(bool)ObjectDB.instance)
                return null;

            GameObject prefab = GetBuildPieces().First(buildPiece => buildPiece.name == pieceName);
            if (prefab == null) 
                return null;

            if (!prefab.TryGetComponent(out Piece piece))
                return null;

            return new CustomPieceData(piece);
        }

        internal static List<GameObject> GetBuildPieces()
        {
            List<GameObject> pieces = new List<GameObject>();

            HashSet<string> toolList = new HashSet<string>(toolsToPatchPieces.Value.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(c => { return c.Trim(); }));

            foreach (string toolName in toolList)
            {
                ItemDrop tool = ObjectDB.instance.GetItemPrefab(toolName)?.GetComponent<ItemDrop>();

                if (tool == null)
                    continue;

                pieces.AddRange(tool.m_itemData.m_shared.m_buildPieces.m_pieces);
            }

            return pieces;
        }

    }
}