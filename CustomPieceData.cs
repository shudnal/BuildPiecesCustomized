using HarmonyLib;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using YamlDotNet.Serialization;
using static BuildPiecesCustomized.BuildPiecesCustomized;
using static Version;

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
        public List<string>? usageTags;
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
        public bool? allowedInDeepSnow;
        public bool? requireDeepSnow;
        public float? spaceRequirement;
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
        public float? roofCheckOffset;
        public bool? noSupportWear;
        public bool? snowDamageImmune;
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

        public string? requiredPersistentEvent;
        public bool? takeDamageIfInsideEvent;
        public float? eventDamage;
        public float? eventDamageDeviation;
        public Heightmap.Biome? requiredBiome;
        public float? outsideRequiredBiomeDamage;

        public List<string>? resources;

        [JsonProperty("__usageTagsAdditions", NullValueHandling = NullValueHandling.Ignore), YamlIgnore]
        internal List<string>? usageTagsAdditions;

        [NonSerialized, JsonIgnore, YamlIgnore]
        private Piece.UsageTagFlags? capturedUsageTags;
        [NonSerialized, JsonIgnore, YamlIgnore]
        private Piece.UsageTagFlags? parsedUsageTags;
        [NonSerialized, JsonIgnore, YamlIgnore]
        private Piece.UsageTagFlags parsedUsageTagAdditions;

        // Parsed configuration is immutable until the source strings change. Keep only names
        // here; resolve ItemDrop against the current ObjectDB so prefab replacement stays live.
        [NonSerialized, JsonIgnore]
        private string[]? parsedResourceSource;
        [NonSerialized, JsonIgnore]
        private ResourceSpec[] parsedResources = Array.Empty<ResourceSpec>();
        [NonSerialized, JsonIgnore]
        private string[]? parsedDamageSource;
        [NonSerialized, JsonIgnore]
        private HitData.DamageModPair[] parsedDamageModifiers = Array.Empty<HitData.DamageModPair>();

        private readonly struct ResourceSpec
        {
            internal readonly string Name;
            internal readonly int Amount;
            internal readonly bool Recover;

            internal ResourceSpec(string name, int amount, bool recover)
            {
                Name = name;
                Amount = amount;
                Recover = recover;
            }
        }

        private static bool Matches(List<string> source, string[]? cached)
        {
            if (cached == null || source.Count != cached.Length)
                return false;
            for (int i = 0; i < cached.Length; i++)
                if (!string.Equals(source[i], cached[i], StringComparison.Ordinal))
                    return false;
            return true;
        }

        private ResourceSpec[] GetResourceSpecs()
        {
            if (resources == null)
                return Array.Empty<ResourceSpec>();
            if (Matches(resources, parsedResourceSource))
                return parsedResources;

            var result = new List<ResourceSpec>(resources.Count);
            foreach (string entry in resources)
            {
                string[] parts = entry?.Split(':') ?? Array.Empty<string>();
                int amount = 1;
                bool recover = true;
                if (parts.Length == 0 || string.IsNullOrWhiteSpace(parts[0])
                    || (parts.Length > 1 && (!int.TryParse(parts[1], out amount) || amount < 0))
                    || (parts.Length > 2 && !bool.TryParse(parts[2], out recover)))
                {
                    LogWarning($"Invalid resource entry for {prefabName}: {entry}");
                    continue;
                }
                result.Add(new ResourceSpec(parts[0], amount, recover));
            }
            parsedResources = result.ToArray();
            parsedResourceSource = resources.ToArray();
            return parsedResources;
        }

        private HitData.DamageModPair[] GetDamageModifierSpecs()
        {
            if (damageModifiers == null)
                return Array.Empty<HitData.DamageModPair>();
            if (Matches(damageModifiers, parsedDamageSource))
                return parsedDamageModifiers;

            var result = new List<HitData.DamageModPair>(damageModifiers.Count);
            foreach (string entry in damageModifiers)
            {
                string[] parts = entry?.Split(':') ?? Array.Empty<string>();
                if (parts.Length != 2 || !Enum.TryParse(parts[0], out HitData.DamageType type)
                    || !Enum.TryParse(parts[1], out HitData.DamageModifier modifier))
                    continue;
                result.Add(new HitData.DamageModPair { m_type = type, m_modifier = modifier });
            }
            parsedDamageModifiers = result.ToArray();
            parsedDamageSource = damageModifiers.ToArray();
            return parsedDamageModifiers;
        }

        internal void PatchPiece(Piece piece, bool skipResources = false)
        {
            if (enabled.HasValue)
                piece.m_enabled = enabled.Value;

            if (name != null)
                piece.m_name = name;

            if (description != null)
                piece.m_description = description;

            if (capturedUsageTags.HasValue)
                piece.m_usage = capturedUsageTags.Value;
            else if (parsedUsageTags.HasValue)
                piece.m_usage = parsedUsageTags.Value;

            if (parsedUsageTagAdditions != 0)
                piece.m_usage |= parsedUsageTagAdditions;

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

            if (allowedInDeepSnow.HasValue)
                piece.m_allowedInDeepSnow = allowedInDeepSnow.Value;

            if (requireDeepSnow.HasValue)
                piece.m_requireDeepSnow = requireDeepSnow.Value;

            if (spaceRequirement.HasValue)
                piece.m_spaceRequirement = spaceRequirement.Value;

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

            if (station != null)
                piece.m_craftingStation = craftingStations.GetValueSafe(station);

            if (onlyInBiome.HasValue)
                piece.m_onlyInBiome = onlyInBiome.Value;

            if (targetNonPlayerBuilt.HasValue)
                piece.m_targetNonPlayerBuilt = targetNonPlayerBuilt.Value;

            if (primaryTarget.HasValue)
                piece.m_primaryTarget = primaryTarget.Value;

            if (randomTarget.HasValue)
                piece.m_randomTarget = randomTarget.Value;

            if (resources != null && !skipResources)
            {
                ResourceSpec[] specs = GetResourceSpecs();
                var requirements = new Piece.Requirement[specs.Length];
                int count = 0;
                for (int i = 0; i < specs.Length; i++)
                {
                    ResourceSpec spec = specs[i];
                    var item = ObjectDB.instance?.GetItemPrefab(spec.Name)?.GetComponent<ItemDrop>();
                    if (item == null)
                        continue;

                    // Requirements remain private to each piece; never share mutable objects
                    // between instances or with a prefab modified by another plugin.
                    requirements[count++] = new Piece.Requirement
                    {
                        m_resItem = item,
                        m_amount = spec.Amount,
                        m_recover = spec.Recover
                    };
                }
                if (count != requirements.Length)
                    Array.Resize(ref requirements, count);
                piece.m_resources = requirements;
            }

            if (GetWearNTearComponent(piece) is WearNTear wnt)
            {
                if (health.HasValue)
                    wnt.m_health = health.Value;

                if (noRoofWear.HasValue)
                    wnt.m_noRoofWear = noRoofWear.Value;

                if (roofCheckOffset.HasValue)
                    wnt.m_roofCheckOffset = roofCheckOffset.Value;

                if (noSupportWear.HasValue)
                    wnt.m_noSupportWear = noSupportWear.Value;

                if (snowDamageImmune.HasValue)
                    wnt.m_snowDamageImmune = snowDamageImmune.Value;

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

                if (requiredPersistentEvent != null)
                    wnt.m_requiredPersistentEvent = requiredPersistentEvent;

                if (takeDamageIfInsideEvent.HasValue)
                    wnt.m_takeDamageIfInsideEvent = takeDamageIfInsideEvent.Value;

                if (eventDamage.HasValue)
                    wnt.m_eventDamage = eventDamage.Value;

                if (eventDamageDeviation.HasValue)
                    wnt.m_eventDamageDeviation = eventDamageDeviation.Value;

                if (requiredBiome.HasValue)
                    wnt.m_requiredBiome = requiredBiome.Value;

                if (outsideRequiredBiomeDamage.HasValue)
                    wnt.m_outsideRequiredBiomeDamage = outsideRequiredBiomeDamage.Value;

                if (materialType.HasValue)
                    wnt.m_materialType = materialType.Value;

                if (damageModifiers != null)
                {
                    foreach (HitData.DamageModPair pair in GetDamageModifierSpecs())
                    {
                        HitData.DamageType type = pair.m_type;
                        HitData.DamageModifier mod = pair.m_modifier;

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

        internal void PrepareUsageTags(string sourceName)
        {
            parsedUsageTags = null;
            parsedUsageTagAdditions = 0;

            if (usageTags != null)
            {
                if (PieceUsageTags.TryParseTags(usageTags, out Piece.UsageTagFlags usage, out List<string> canonicalNames, out string invalidValue))
                {
                    usageTags = canonicalNames;
                    parsedUsageTags = usage;
                }
                else
                {
                    LogWarning($"Invalid usage tag '{invalidValue}' in '{sourceName}'. The configured usageTags value will be ignored.");
                    usageTags = null;
                }
            }

            if (usageTagsAdditions != null)
            {
                if (PieceUsageTags.TryParseTags(usageTagsAdditions, out Piece.UsageTagFlags additions, out List<string> canonicalNames, out string invalidValue))
                {
                    usageTagsAdditions = canonicalNames;
                    parsedUsageTagAdditions = additions;
                }
                else
                {
                    LogWarning($"Invalid usage tag '{invalidValue}' in the bulk usage tag override for '{sourceName}'. The tag additions will be ignored.");
                    usageTagsAdditions = null;
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
            capturedUsageTags = piece.m_usage;
            usageTags = PieceUsageTags.HasUnsupportedBits(piece.m_usage) ? null : PieceUsageTags.GetNames(piece.m_usage);
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
            allowedInDeepSnow = piece.m_allowedInDeepSnow;
            requireDeepSnow = piece.m_requireDeepSnow;
            spaceRequirement = piece.m_spaceRequirement;
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
                roofCheckOffset = wnt.m_roofCheckOffset;
                noSupportWear = wnt.m_noSupportWear;
                snowDamageImmune = wnt.m_snowDamageImmune;
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

                requiredPersistentEvent = wnt.m_requiredPersistentEvent;
                takeDamageIfInsideEvent = wnt.m_takeDamageIfInsideEvent;
                eventDamage = wnt.m_eventDamage;
                eventDamageDeviation = wnt.m_eventDamageDeviation;
                requiredBiome = wnt.m_requiredBiome;
                outsideRequiredBiomeDamage = wnt.m_outsideRequiredBiomeDamage;

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