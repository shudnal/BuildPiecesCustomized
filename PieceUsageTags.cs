using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using YamlDotNet.Serialization;

namespace BuildPiecesCustomized
{
    internal static class PieceUsageTags
    {
        internal const string ConfigFileName = "Piece usage tags";

        internal sealed class Definition
        {
            internal string Name { get; }
            internal Piece.UsageTagFlags Value { get; }
            internal string DisplayNameToken { get; }

            internal Definition(string name, Piece.UsageTagFlags value, string displayNameToken)
            {
                Name = name;
                Value = value;
                DisplayNameToken = displayNameToken;
            }
        }

        internal sealed class ConfigFile
        {
            [JsonProperty("pieces"), YamlMember(Alias = "pieces")]
            public Dictionary<string, List<string>> Pieces { get; set; }

            [JsonProperty("tags"), YamlMember(Alias = "tags")]
            public Dictionary<string, List<string>> Tags { get; set; }
        }

        private static readonly List<Definition> definitions = typeof(Piece.UsageTagFlags)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .OrderBy(field => field.MetadataToken)
            .Select(field =>
            {
                DisplayNameAttribute displayName = (DisplayNameAttribute)Attribute.GetCustomAttribute(field, typeof(DisplayNameAttribute));
                return new Definition(field.Name, (Piece.UsageTagFlags)field.GetValue(null), displayName?.DisplayName);
            })
            .ToList();

        private static readonly Dictionary<string, Definition> definitionsByName = definitions
            .ToDictionary(definition => definition.Name, StringComparer.OrdinalIgnoreCase);

        private static readonly int knownMask = definitions.Aggregate(0, (mask, definition) => mask | (int)definition.Value);

        internal static IReadOnlyList<Definition> Definitions => definitions;

        internal static bool TryParseTag(string value, out Piece.UsageTagFlags tag, out string canonicalName)
        {
            tag = 0;
            canonicalName = null;
            if (string.IsNullOrWhiteSpace(value) || !definitionsByName.TryGetValue(value.Trim(), out Definition definition))
                return false;

            tag = definition.Value;
            canonicalName = definition.Name;
            return true;
        }

        internal static bool TryParseTags(IEnumerable<string> values, out Piece.UsageTagFlags result, out List<string> canonicalNames, out string invalidValue)
        {
            result = 0;
            canonicalNames = new List<string>();
            invalidValue = null;

            if (values == null)
                return true;

            var selected = new HashSet<Piece.UsageTagFlags>();
            foreach (string value in values)
            {
                if (!TryParseTag(value, out Piece.UsageTagFlags tag, out _))
                {
                    invalidValue = value ?? "<null>";
                    return false;
                }

                selected.Add(tag);
            }

            foreach (Definition definition in definitions)
            {
                if (!selected.Contains(definition.Value))
                    continue;

                result |= definition.Value;
                canonicalNames.Add(definition.Name);
            }

            return true;
        }

        internal static List<string> GetNames(Piece.UsageTagFlags value)
        {
            var result = new List<string>();
            foreach (Definition definition in definitions)
                if ((value & definition.Value) == definition.Value)
                    result.Add(definition.Name);
            return result;
        }

        internal static bool HasUnsupportedBits(Piece.UsageTagFlags value) => ((int)value & ~knownMask) != 0;

        internal static string GetLocalizedDisplayName(Definition definition)
        {
            if (string.IsNullOrWhiteSpace(definition.DisplayNameToken))
                return definition.Name;

            return Localization.instance != null
                ? Localization.instance.Localize(definition.DisplayNameToken)
                : definition.DisplayNameToken;
        }

        internal static string FormatForDocumentation(Piece.UsageTagFlags value)
        {
            string names = string.Join(", ", GetNames(value));
            if (!HasUnsupportedBits(value))
                return names.Length == 0 ? "None" : names;

            return names.Length == 0 ? "Unsupported/custom tags" : names + ", Unsupported/custom tags";
        }
    }
}
