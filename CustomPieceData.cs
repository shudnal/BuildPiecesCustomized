using HarmonyLib;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using static BuildPiecesCustomized.BuildPiecesCustomized;

#nullable enable

namespace BuildPiecesCustomized
{
    [Serializable]
    internal class CustomPieceData
    {
        public string? prefabName;

        public string? name;
        public string? description;

        public bool? enabled;
        public Piece.PieceCategory? category;
        public Piece.ComfortGroup? comfortGroup;
        public int? comfort;
        public bool? groundOnly;
        public bool? cultivatedGroundOnly;
        public bool? waterPiece;
        public bool? clipGround;
        public bool? clipEverything;
        public bool? noInWater;
        public bool? notOnWood;
        public bool? notOnTiltingSurface;
        public bool? inCeilingOnly;
        public bool? notOnFloor;
        public bool? noClipping;
        public bool? onlyInTeleportArea;
        public bool? allowedInDungeons;
        public float? spaceRequirement;
        public bool? repairPiece;
        public bool? canBeRemoved;
        public bool? allowRotatedOverlap;
        public bool? vegetationGroundOnly;
        public float? blockRadius;
        public int? extraPlacementDistance;
        public string? station;
        public Heightmap.Biome? onlyInBiome;

        public bool? targetNonPlayerBuilt;
        public bool? primaryTarget;
        public bool? randomTarget;

        public float? health;
        public bool? noRoofWear;
        public bool? noSupportWear;
        public WearNTear.MaterialType? materialType;
        public bool? supports;
        public float? hitNoise;
        public float? destroyNoise;
        public bool? autoCreateFragments;
        public List<string>? damageModifiers;

        public bool? ashDamageImmune;
        public bool? ashDamageResist;
        public bool? burnable;
        public int? minToolTier;
        public bool? triggerPrivateArea;

        public List<string>? resources;

        internal void PatchPiece(Piece piece)
        {
            if (enabled.HasValue)
                piece.m_enabled = enabled.Value;

            if (name != null)
                piece.m_name = name;

            if (description != null)
                piece.m_description = description;

            if (category.HasValue)
                piece.m_category = category.Value;

            if (comfort.HasValue)
                piece.m_comfort = comfort.Value;

            if (comfortGroup.HasValue)
                piece.m_comfortGroup = comfortGroup.Value;

            if (groundOnly.HasValue)
                piece.m_groundOnly = groundOnly.Value;

            if (cultivatedGroundOnly.HasValue)
                piece.m_cultivatedGroundOnly = cultivatedGroundOnly.Value;

            if (waterPiece.HasValue)
                piece.m_waterPiece = waterPiece.Value;

            if (clipGround.HasValue)
                piece.m_clipGround = clipGround.Value;

            if (clipEverything.HasValue)
                piece.m_clipEverything = clipEverything.Value;

            if (noInWater.HasValue)
                piece.m_noInWater = noInWater.Value;

            if (notOnWood.HasValue)
                piece.m_notOnWood = notOnWood.Value;

            if (notOnTiltingSurface.HasValue)
                piece.m_notOnTiltingSurface = notOnTiltingSurface.Value;

            if (inCeilingOnly.HasValue)
                piece.m_inCeilingOnly = inCeilingOnly.Value;

            if (notOnFloor.HasValue)
                piece.m_notOnFloor = notOnFloor.Value;

            if (noClipping.HasValue)
                piece.m_noClipping = noClipping.Value;

            if (onlyInTeleportArea.HasValue)
                piece.m_onlyInTeleportArea = onlyInTeleportArea.Value;

            if (allowedInDungeons.HasValue)
                piece.m_allowedInDungeons = allowedInDungeons.Value;

            if (spaceRequirement.HasValue)
                piece.m_spaceRequirement = spaceRequirement.Value;

            if (repairPiece.HasValue)
                piece.m_repairPiece = repairPiece.Value;

            if (canBeRemoved.HasValue)
                piece.m_canBeRemoved = canBeRemoved.Value;

            if (allowRotatedOverlap.HasValue)
                piece.m_allowRotatedOverlap = allowRotatedOverlap.Value;

            if (vegetationGroundOnly.HasValue)
                piece.m_vegetationGroundOnly = vegetationGroundOnly.Value;

            if (blockRadius.HasValue)
                piece.m_blockRadius = blockRadius.Value;

            if (extraPlacementDistance.HasValue)
                piece.m_extraPlacementDistance = extraPlacementDistance.Value;

            if (!string.IsNullOrEmpty(station))
                piece.m_craftingStation = craftingStations.GetValueSafe(station);

            if (onlyInBiome.HasValue)
                piece.m_onlyInBiome = onlyInBiome.Value;

            if (targetNonPlayerBuilt.HasValue)
                piece.m_targetNonPlayerBuilt = targetNonPlayerBuilt.Value;

            if (primaryTarget.HasValue)
                piece.m_primaryTarget = primaryTarget.Value;

            if (randomTarget.HasValue)
                piece.m_randomTarget = randomTarget.Value;

            if (resources != null)
            {
                var reqs = new List<Piece.Requirement>();

                foreach (string modString in resources)
                {
                    string[] parts = modString.Split(':');
                    if (parts.Length < 1)
                        continue;

                    var item = ObjectDB.instance?.GetItemPrefab(parts[0])?.GetComponent<ItemDrop>();

                    if (item == null)
                        continue;

                    reqs.Add(new Piece.Requirement
                    {
                        m_resItem = item,
                        m_amount = parts.Length > 1 ? int.Parse(parts[1]) : 1,
                        m_recover = parts.Length < 3 || bool.Parse(parts[2])
                    });
                }

                piece.m_resources = reqs.ToArray();
            }

            if (GetWearNTearComponent(piece) is WearNTear wnt)
            {
                if (health.HasValue)
                    wnt.m_health = health.Value;

                if (noRoofWear.HasValue)
                    wnt.m_noRoofWear = noRoofWear.Value;

                if (noSupportWear.HasValue)
                    wnt.m_noSupportWear = noSupportWear.Value;

                if (supports.HasValue)
                    wnt.m_supports = supports.Value;

                if (hitNoise.HasValue)
                    wnt.m_hitNoise = hitNoise.Value;

                if (destroyNoise.HasValue)
                    wnt.m_destroyNoise = destroyNoise.Value;

                if (autoCreateFragments.HasValue)
                    wnt.m_autoCreateFragments = autoCreateFragments.Value;

                if (ashDamageImmune.HasValue)
                    wnt.m_ashDamageImmune = ashDamageImmune.Value;

                if (ashDamageResist.HasValue)
                    wnt.m_ashDamageResist = ashDamageResist.Value;

                if (burnable.HasValue)
                    wnt.m_burnable = burnable.Value;

                if (minToolTier.HasValue)
                    wnt.m_minToolTier = minToolTier.Value;

                if (triggerPrivateArea.HasValue)
                    wnt.m_triggerPrivateArea = triggerPrivateArea.Value;

                if (materialType.HasValue)
                    wnt.m_materialType = materialType.Value;

                if (damageModifiers != null)
                {
                    foreach (string modString in damageModifiers)
                    {
                        var parts = modString.Split(':');
                        if (parts.Length != 2)
                            continue;

                        if (!Enum.TryParse(parts[0], out HitData.DamageType type))
                            continue;

                        if (!Enum.TryParse(parts[1], out HitData.DamageModifier mod))
                            continue;

                        ref var dmg = ref wnt.m_damages;

                        switch (type)
                        {
                            case HitData.DamageType.Blunt: dmg.m_blunt = mod; break;
                            case HitData.DamageType.Slash: dmg.m_slash = mod; break;
                            case HitData.DamageType.Pierce: dmg.m_pierce = mod; break;
                            case HitData.DamageType.Chop: dmg.m_chop = mod; break;
                            case HitData.DamageType.Pickaxe: dmg.m_pickaxe = mod; break;
                            case HitData.DamageType.Fire: dmg.m_fire = mod; break;
                            case HitData.DamageType.Frost: dmg.m_frost = mod; break;
                            case HitData.DamageType.Lightning: dmg.m_lightning = mod; break;
                            case HitData.DamageType.Poison: dmg.m_poison = mod; break;
                            case HitData.DamageType.Spirit: dmg.m_spirit = mod; break;
                        }
                    }
                }
            }
        }

        internal void SaveToDirectory(string directory)
        {
            Directory.CreateDirectory(directory);

            string filename = Path.Combine(directory, $"{prefabName}.{(saveAsYAML.Value ? "yaml" : "json")}");

            File.WriteAllText(filename, saveAsYAML.Value ? YamlSerializer.Serialize(this) : JsonConvert.SerializeObject(this, Formatting.Indented));
        }

        internal CustomPieceData()
        {

        }

        internal CustomPieceData(Piece piece)
        {
            prefabName = piece.gameObject.name;
            enabled = piece.m_enabled;
            name = piece.m_name;
            description = piece.m_description;
            category = piece.m_category;
            comfortGroup = piece.m_comfortGroup;
            comfort = piece.m_comfort;
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

            resources = new List<string>();
            resources.AddRange(piece.m_resources.Select(req => { return $"{req.m_resItem.name}:{req.m_amount}:{req.m_recover}"; }));

            WearNTear wnt = GetWearNTearComponent(piece);
            if (wnt != null)
            {
                health = wnt.m_health;
                noRoofWear = wnt.m_noRoofWear;
                noSupportWear = wnt.m_noSupportWear;
                materialType = wnt.m_materialType;
                supports = wnt.m_supports;
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

        internal static CustomPieceData? GetByPieceName(string pieceName)
        {
            if (!(bool)ObjectDB.instance)
                return null;

            GameObject prefab = GetBuildPieces().FirstOrDefault(buildPiece => buildPiece.name == pieceName); 
            if (prefab == null) 
                return null;

            if (!prefab.TryGetComponent(out Piece piece))
                return null;

            return new CustomPieceData(piece);
        }

        internal static List<GameObject> GetBuildPieces()
        {
            List<GameObject> pieces = new List<GameObject>();

            HashSet<string> toolList = new HashSet<string>(toolsToPatchPieces.Value.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(c => c.Trim()));

            foreach (string toolName in toolList)
            {
                var tool = ObjectDB.instance.GetItemPrefab(toolName)?.GetComponent<ItemDrop>();

                if (tool == null)
                    continue;

                pieces.AddRange(tool.m_itemData.m_shared.m_buildPieces.m_pieces);
            }

            return pieces;
        }

    }
}