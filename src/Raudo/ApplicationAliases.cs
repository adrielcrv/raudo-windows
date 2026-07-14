using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Web.Script.Serialization;

namespace Raudo
{
    internal static class ApplicationAliasCatalog
    {
        private const string ResourceName = "Raudo.ApplicationAliases.es-MX.json";
        private const int MaximumAliasesPerApplication = 8;
        private static readonly Lazy<IList<ApplicationAliasDefinition>> Definitions =
            new Lazy<IList<ApplicationAliasDefinition>>(LoadDefinitions, true);

        public static IList<string> GetAliases(string name, string identifier)
        {
            List<string> aliases = new List<string>();
            if (string.IsNullOrWhiteSpace(identifier))
            {
                return aliases;
            }

            HashSet<string> unique = new HashSet<string>(StringComparer.Ordinal);
            string normalizedName = RaudoActionCatalog.Normalize(name);
            foreach (ApplicationAliasDefinition definition in Definitions.Value)
            {
                if (!MatchesIdentifier(identifier, definition.IdentifierContains))
                {
                    continue;
                }

                if (definition.Aliases == null)
                {
                    continue;
                }

                foreach (string rawAlias in definition.Aliases)
                {
                    string alias = (rawAlias ?? string.Empty).Trim();
                    string normalized = RaudoActionCatalog.Normalize(alias);
                    if (alias.Length == 0
                        || alias.Length > 64
                        || normalized.Length == 0
                        || string.Equals(normalized, normalizedName, StringComparison.Ordinal)
                        || !unique.Add(normalized))
                    {
                        continue;
                    }

                    aliases.Add(alias);
                    if (aliases.Count >= MaximumAliasesPerApplication)
                    {
                        return aliases;
                    }
                }
            }

            return aliases;
        }

        private static bool MatchesIdentifier(string identifier, string[] tokens)
        {
            if (tokens == null || tokens.Length == 0)
            {
                return false;
            }

            foreach (string rawToken in tokens)
            {
                string token = (rawToken ?? string.Empty).Trim();
                if (token.Length >= 4
                    && identifier.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static IList<ApplicationAliasDefinition> LoadDefinitions()
        {
            List<ApplicationAliasDefinition> empty = new List<ApplicationAliasDefinition>();
            try
            {
                using (Stream stream = Assembly.GetExecutingAssembly()
                    .GetManifestResourceStream(ResourceName))
                {
                    if (stream == null || stream.Length <= 0 || stream.Length > 128 * 1024)
                    {
                        return empty;
                    }

                    string json;
                    using (StreamReader reader = new StreamReader(
                        stream,
                        Encoding.UTF8,
                        true,
                        1024,
                        false))
                    {
                        json = reader.ReadToEnd();
                    }

                    JavaScriptSerializer serializer = new JavaScriptSerializer();
                    serializer.MaxJsonLength = 128 * 1024;
                    ApplicationAliasDocument document =
                        serializer.Deserialize<ApplicationAliasDocument>(json);
                    if (document == null
                        || document.SchemaVersion != 1
                        || !string.Equals(
                            document.LanguageTag,
                            "es-MX",
                            StringComparison.OrdinalIgnoreCase)
                        || document.Applications == null)
                    {
                        return empty;
                    }

                    return new List<ApplicationAliasDefinition>(document.Applications);
                }
            }
            catch (IOException)
            {
                return empty;
            }
            catch (InvalidOperationException)
            {
                return empty;
            }
            catch (ArgumentException)
            {
                return empty;
            }
        }
    }

    internal sealed class ApplicationAliasDocument
    {
        public int SchemaVersion { get; set; }
        public string LanguageTag { get; set; }
        public ApplicationAliasDefinition[] Applications { get; set; }
    }

    internal sealed class ApplicationAliasDefinition
    {
        public string[] IdentifierContains { get; set; }
        public string[] Aliases { get; set; }
    }
}
