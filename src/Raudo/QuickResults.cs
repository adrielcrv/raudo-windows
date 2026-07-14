using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;

namespace Raudo
{
    internal sealed class QuickResultProvider
    {
        private static readonly Regex ConversionPattern = new Regex(
            @"^\s*(?:(?:convertir|convierte)\s+)?(?<value>[+-]?(?:\d+(?:[\.,]\d*)?|[\.,]\d+))\s*(?<from>[^\s]+)\s+(?:a|en|to|->|→)\s+(?<to>[^\s]+)\s*$",
            RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

        private static readonly Dictionary<string, UnitDefinition> Units = CreateUnits();
        private readonly Func<string, string> copyText;

        public QuickResultProvider(Func<string, string> copyAction)
        {
            if (copyAction == null)
            {
                throw new ArgumentNullException("copyAction");
            }

            copyText = copyAction;
        }

        public IList<RaudoAction> CreateActions(string query)
        {
            List<RaudoAction> actions = new List<RaudoAction>(1);
            string value;
            string description;
            if (TryConvert(query, out value, out description))
            {
                string copyValue = value;
                actions.Add(new RaudoAction(
                    "quick.conversion",
                    "= " + value,
                    description,
                    string.Empty,
                    "Copiar",
                    RaudoActionGlyph.Conversion,
                    RaudoActionKind.Conversion,
                    false,
                    0,
                    delegate { return copyText(copyValue); }));
                return actions;
            }

            decimal result;
            if (ArithmeticParser.TryEvaluate(query, out result))
            {
                string formatted = FormatDecimal(result);
                actions.Add(new RaudoAction(
                    "quick.calculation",
                    "= " + formatted,
                    "Resultado local",
                    string.Empty,
                    "Copiar",
                    RaudoActionGlyph.Calculator,
                    RaudoActionKind.Calculation,
                    false,
                    0,
                    delegate { return copyText(formatted); }));
            }

            return actions;
        }

        internal static bool TryConvert(
            string query,
            out string displayValue,
            out string description)
        {
            displayValue = null;
            description = null;
            if (string.IsNullOrWhiteSpace(query) || query.Length > 128)
            {
                return false;
            }

            string normalized = RaudoActionCatalog.Normalize(query);
            Match match = ConversionPattern.Match(normalized);
            if (!match.Success)
            {
                return false;
            }

            decimal input;
            if (!TryParseDecimal(match.Groups["value"].Value, out input))
            {
                return false;
            }

            UnitDefinition source;
            UnitDefinition target;
            if (!Units.TryGetValue(NormalizeUnit(match.Groups["from"].Value), out source)
                || !Units.TryGetValue(NormalizeUnit(match.Groups["to"].Value), out target)
                || !string.Equals(source.Family, target.Family, StringComparison.Ordinal))
            {
                return false;
            }

            try
            {
                decimal converted;
                if (string.Equals(source.Family, "temperature", StringComparison.Ordinal))
                {
                    decimal kelvin = source.ToKelvin(input);
                    if (kelvin < 0M)
                    {
                        return false;
                    }

                    converted = target.FromKelvin(kelvin);
                }
                else
                {
                    converted = (input * source.ToBaseFactor) / target.ToBaseFactor;
                }

                string sourceValue = FormatDecimal(input) + " " + source.Symbol;
                displayValue = FormatDecimal(converted) + " " + target.Symbol;
                description = "Conversión local · " + sourceValue;
                return true;
            }
            catch (OverflowException)
            {
                return false;
            }
            catch (DivideByZeroException)
            {
                return false;
            }
        }

        internal static string FormatDecimal(decimal value)
        {
            return value.ToString("0.############", CultureInfo.CurrentCulture);
        }

        private static bool TryParseDecimal(string value, out decimal result)
        {
            result = 0M;
            if (string.IsNullOrWhiteSpace(value)
                || (value.IndexOf('.') >= 0 && value.IndexOf(',') >= 0))
            {
                return false;
            }

            return decimal.TryParse(
                value.Replace(',', '.'),
                NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture,
                out result);
        }

        private static string NormalizeUnit(string value)
        {
            return RaudoActionCatalog.Normalize(value).TrimEnd('.');
        }

        private static Dictionary<string, UnitDefinition> CreateUnits()
        {
            Dictionary<string, UnitDefinition> units =
                new Dictionary<string, UnitDefinition>(StringComparer.Ordinal);

            AddUnit(units, "length", "mm", 0.001M, "mm", "milimetro", "milimetros");
            AddUnit(units, "length", "cm", 0.01M, "cm", "centimetro", "centimetros");
            AddUnit(units, "length", "m", 1M, "m", "metro", "metros");
            AddUnit(units, "length", "km", 1000M, "km", "kilometro", "kilometros");
            AddUnit(units, "length", "in", 0.0254M, "in", "inch", "inches", "pulgada", "pulgadas");
            AddUnit(units, "length", "ft", 0.3048M, "ft", "foot", "feet", "pie", "pies");
            AddUnit(units, "length", "yd", 0.9144M, "yd", "yard", "yards", "yarda", "yardas");
            AddUnit(units, "length", "mi", 1609.344M, "mi", "mile", "miles", "milla", "millas");

            AddUnit(units, "mass", "mg", 0.001M, "mg", "miligramo", "miligramos");
            AddUnit(units, "mass", "g", 1M, "g", "gramo", "gramos");
            AddUnit(units, "mass", "kg", 1000M, "kg", "kilogramo", "kilogramos", "kilo", "kilos");
            AddUnit(units, "mass", "oz", 28.349523125M, "oz", "ounce", "ounces", "onza", "onzas");
            AddUnit(units, "mass", "lb", 453.59237M, "lb", "lbs", "pound", "pounds", "libra", "libras");

            AddTemperatureUnit(units, "°C", TemperatureScale.Celsius, "c", "°c", "celsius", "centigrado", "centigrados");
            AddTemperatureUnit(units, "°F", TemperatureScale.Fahrenheit, "f", "°f", "fahrenheit");
            AddTemperatureUnit(units, "K", TemperatureScale.Kelvin, "k", "kelvin");

            AddUnit(units, "time", "ms", 0.001M, "ms", "milisegundo", "milisegundos");
            AddUnit(units, "time", "s", 1M, "s", "seg", "second", "seconds", "segundo", "segundos");
            AddUnit(units, "time", "min", 60M, "min", "minute", "minutes", "minuto", "minutos");
            AddUnit(units, "time", "h", 3600M, "h", "hr", "hrs", "hour", "hours", "hora", "horas");
            AddUnit(units, "time", "d", 86400M, "d", "day", "days", "dia", "dias");

            AddUnit(units, "storage", "B", 1M, "b", "byte", "bytes");
            AddUnit(units, "storage", "KB", 1024M, "kb", "kib", "kilobyte", "kilobytes");
            AddUnit(units, "storage", "MB", 1048576M, "mb", "mib", "megabyte", "megabytes");
            AddUnit(units, "storage", "GB", 1073741824M, "gb", "gib", "gigabyte", "gigabytes");
            AddUnit(units, "storage", "TB", 1099511627776M, "tb", "tib", "terabyte", "terabytes");
            return units;
        }

        private static void AddUnit(
            IDictionary<string, UnitDefinition> units,
            string family,
            string symbol,
            decimal toBaseFactor,
            params string[] aliases)
        {
            UnitDefinition definition = new UnitDefinition(
                family,
                symbol,
                toBaseFactor,
                TemperatureScale.None);
            foreach (string alias in aliases)
            {
                units[NormalizeUnit(alias)] = definition;
            }
        }

        private static void AddTemperatureUnit(
            IDictionary<string, UnitDefinition> units,
            string symbol,
            TemperatureScale scale,
            params string[] aliases)
        {
            UnitDefinition definition = new UnitDefinition(
                "temperature",
                symbol,
                1M,
                scale);
            foreach (string alias in aliases)
            {
                units[NormalizeUnit(alias)] = definition;
            }
        }

        private enum TemperatureScale
        {
            None,
            Celsius,
            Fahrenheit,
            Kelvin
        }

        private sealed class UnitDefinition
        {
            public UnitDefinition(
                string family,
                string symbol,
                decimal toBaseFactor,
                TemperatureScale temperatureScale)
            {
                Family = family;
                Symbol = symbol;
                ToBaseFactor = toBaseFactor;
                Scale = temperatureScale;
            }

            public string Family { get; private set; }
            public string Symbol { get; private set; }
            public decimal ToBaseFactor { get; private set; }
            private TemperatureScale Scale { get; set; }

            public decimal ToKelvin(decimal value)
            {
                switch (Scale)
                {
                    case TemperatureScale.Celsius:
                        return value + 273.15M;
                    case TemperatureScale.Fahrenheit:
                        return ((value - 32M) * 5M / 9M) + 273.15M;
                    default:
                        return value;
                }
            }

            public decimal FromKelvin(decimal value)
            {
                switch (Scale)
                {
                    case TemperatureScale.Celsius:
                        return value - 273.15M;
                    case TemperatureScale.Fahrenheit:
                        return ((value - 273.15M) * 9M / 5M) + 32M;
                    default:
                        return value;
                }
            }
        }
    }

    internal sealed class ArithmeticParser
    {
        private const int MaximumExpressionLength = 128;
        private const int MaximumParenthesisDepth = 16;
        private const int MaximumOperatorCount = 64;

        private readonly string expression;
        private int position;
        private int parenthesisDepth;
        private int unaryDepth;
        private int operatorCount;

        private ArithmeticParser(string value)
        {
            expression = value;
        }

        public static bool TryEvaluate(string query, out decimal result)
        {
            result = 0M;
            if (string.IsNullOrWhiteSpace(query))
            {
                return false;
            }

            string expression = query.Trim();
            if (expression.StartsWith("=", StringComparison.Ordinal))
            {
                expression = expression.Substring(1).TrimStart();
            }

            if (expression.Length == 0 || expression.Length > MaximumExpressionLength)
            {
                return false;
            }

            try
            {
                ArithmeticParser parser = new ArithmeticParser(expression);
                decimal value = parser.ParseExpression();
                parser.SkipWhitespace();
                if (parser.position != expression.Length || parser.operatorCount == 0)
                {
                    return false;
                }

                result = value;
                return true;
            }
            catch (FormatException)
            {
                return false;
            }
            catch (OverflowException)
            {
                return false;
            }
            catch (DivideByZeroException)
            {
                return false;
            }
        }

        private decimal ParseExpression()
        {
            decimal value = ParseTerm();
            while (true)
            {
                if (Match('+'))
                {
                    CountOperator();
                    value += ParseTerm();
                }
                else if (Match('-'))
                {
                    CountOperator();
                    value -= ParseTerm();
                }
                else
                {
                    return value;
                }
            }
        }

        private decimal ParseTerm()
        {
            decimal value = ParseFactor();
            while (true)
            {
                if (Match('*') || Match('x') || Match('X') || Match('×'))
                {
                    CountOperator();
                    value *= ParseFactor();
                }
                else if (Match('/') || Match('÷'))
                {
                    CountOperator();
                    decimal divisor = ParseFactor();
                    if (divisor == 0M)
                    {
                        throw new DivideByZeroException();
                    }

                    value /= divisor;
                }
                else if (Match('%'))
                {
                    CountOperator();
                    decimal divisor = ParseFactor();
                    if (divisor == 0M)
                    {
                        throw new DivideByZeroException();
                    }

                    value %= divisor;
                }
                else
                {
                    return value;
                }
            }
        }

        private decimal ParseFactor()
        {
            SkipWhitespace();
            if (Match('+'))
            {
                return ParseUnary();
            }

            if (Match('-'))
            {
                return -ParseUnary();
            }

            if (Match('('))
            {
                parenthesisDepth++;
                if (parenthesisDepth > MaximumParenthesisDepth)
                {
                    throw new FormatException();
                }

                decimal value = ParseExpression();
                if (!Match(')'))
                {
                    throw new FormatException();
                }

                parenthesisDepth--;
                return value;
            }

            return ParseNumber();
        }

        private decimal ParseUnary()
        {
            unaryDepth++;
            if (unaryDepth > MaximumParenthesisDepth)
            {
                throw new FormatException();
            }

            try
            {
                return ParseFactor();
            }
            finally
            {
                unaryDepth--;
            }
        }

        private decimal ParseNumber()
        {
            SkipWhitespace();
            int start = position;
            bool hasDigit = false;
            bool hasSeparator = false;
            while (position < expression.Length)
            {
                char character = expression[position];
                if (char.IsDigit(character))
                {
                    hasDigit = true;
                    position++;
                }
                else if ((character == '.' || character == ',') && !hasSeparator)
                {
                    hasSeparator = true;
                    position++;
                }
                else
                {
                    break;
                }
            }

            if (!hasDigit)
            {
                throw new FormatException();
            }

            decimal value;
            string token = expression.Substring(start, position - start).Replace(',', '.');
            if (!decimal.TryParse(
                token,
                NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture,
                out value))
            {
                throw new FormatException();
            }

            return value;
        }

        private bool Match(char expected)
        {
            SkipWhitespace();
            if (position >= expression.Length || expression[position] != expected)
            {
                return false;
            }

            position++;
            return true;
        }

        private void SkipWhitespace()
        {
            while (position < expression.Length && char.IsWhiteSpace(expression[position]))
            {
                position++;
            }
        }

        private void CountOperator()
        {
            operatorCount++;
            if (operatorCount > MaximumOperatorCount)
            {
                throw new FormatException();
            }
        }
    }

    internal static class ClipboardWriter
    {
        public static string TryCopy(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "El resultado no se pudo copiar.";
            }

            try
            {
                Clipboard.SetDataObject(value, true, 5, 40);
                return null;
            }
            catch (ExternalException)
            {
                return "El Portapapeles está ocupado. Inténtalo de nuevo.";
            }
            catch (ThreadStateException)
            {
                return "El Portapapeles no está disponible.";
            }
        }
    }

    internal sealed class KnownFolderEntry
    {
        public KnownFolderEntry(string id, string title, string keywords, string path)
        {
            Id = id;
            Title = title;
            Keywords = keywords;
            Path = path;
        }

        public string Id { get; private set; }
        public string Title { get; private set; }
        public string Keywords { get; private set; }
        public string Path { get; private set; }
    }

    internal static class KnownFolderCatalog
    {
        private static readonly KnownFolderDefinition[] Definitions =
        {
            new KnownFolderDefinition(
                "desktop",
                "Escritorio",
                "desktop escritorio carpeta",
                new Guid("B4BFCC3A-DB2C-424C-B029-7FE99A87C641")),
            new KnownFolderDefinition(
                "downloads",
                "Descargas",
                "downloads descargas archivos carpeta",
                new Guid("374DE290-123F-4565-9164-39C4925E467B")),
            new KnownFolderDefinition(
                "documents",
                "Documentos",
                "documents documentos carpeta",
                new Guid("FDD39AD0-238F-46AF-ADB4-6C85480369C7")),
            new KnownFolderDefinition(
                "pictures",
                "Imágenes",
                "pictures imagenes fotos carpeta",
                new Guid("33E28130-4E1E-4676-835A-98395C3BC3BB")),
            new KnownFolderDefinition(
                "music",
                "Música",
                "music musica audio carpeta",
                new Guid("4BD8D571-6D19-48D3-BE97-422220080E43")),
            new KnownFolderDefinition(
                "videos",
                "Videos",
                "videos peliculas carpeta",
                new Guid("18989B1D-99B5-455B-841C-AB7C74E4DDFC"))
        };

        public static IList<RaudoAction> CreateActions()
        {
            List<RaudoAction> actions = new List<RaudoAction>();
            foreach (KnownFolderEntry rawFolder in GetFolders())
            {
                KnownFolderEntry folder = rawFolder;
                actions.Add(new RaudoAction(
                    "known-folder:" + folder.Id,
                    folder.Title,
                    "Carpeta local de Windows",
                    folder.Keywords,
                    "Abrir",
                    RaudoActionGlyph.Folder,
                    RaudoActionKind.Folder,
                    false,
                    5,
                    delegate { return FolderLauncher.TryOpen(folder.Path); }));
            }

            return actions;
        }

        internal static IList<KnownFolderEntry> GetFolders()
        {
            List<KnownFolderEntry> folders = new List<KnownFolderEntry>();
            HashSet<string> paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (KnownFolderDefinition definition in Definitions)
            {
                string path;
                if (!TryGetPath(definition.Identifier, out path)
                    || !paths.Add(path))
                {
                    continue;
                }

                folders.Add(new KnownFolderEntry(
                    definition.Id,
                    definition.Title,
                    definition.Keywords,
                    path));
            }

            return folders;
        }

        private static bool TryGetPath(Guid identifier, out string path)
        {
            path = null;
            IntPtr rawPath = IntPtr.Zero;
            try
            {
                int result = KnownFolderNativeMethods.SHGetKnownFolderPath(
                    ref identifier,
                    0,
                    IntPtr.Zero,
                    out rawPath);
                if (result != 0 || rawPath == IntPtr.Zero)
                {
                    return false;
                }

                string candidate;
                if (!LocalFolderPolicy.TryNormalizeExistingDirectory(
                    Marshal.PtrToStringUni(rawPath),
                    out candidate))
                {
                    return false;
                }

                path = candidate;
                return true;
            }
            catch (ArgumentException)
            {
                return false;
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
            finally
            {
                if (rawPath != IntPtr.Zero)
                {
                    Marshal.FreeCoTaskMem(rawPath);
                }
            }
        }

        private sealed class KnownFolderDefinition
        {
            public KnownFolderDefinition(string id, string title, string keywords, Guid identifier)
            {
                Id = id;
                Title = title;
                Keywords = keywords;
                Identifier = identifier;
            }

            public string Id { get; private set; }
            public string Title { get; private set; }
            public string Keywords { get; private set; }
            public Guid Identifier { get; private set; }
        }

        private static class KnownFolderNativeMethods
        {
            [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = true)]
            internal static extern int SHGetKnownFolderPath(
                ref Guid folderId,
                uint flags,
                IntPtr token,
                out IntPtr path);
        }
    }

    internal static class LocalFolderPolicy
    {
        private const uint DriveUnknown = 0;
        private const uint DriveNoRootDirectory = 1;
        private const uint DriveRemote = 4;

        public static bool TryNormalizeExistingDirectory(string path, out string normalizedPath)
        {
            normalizedPath = null;
            try
            {
                if (string.IsNullOrWhiteSpace(path) || !Path.IsPathRooted(path))
                {
                    return false;
                }

                string candidate = Path.GetFullPath(path);
                string root = Path.GetPathRoot(candidate);
                if (string.IsNullOrWhiteSpace(root)
                    || root.StartsWith("\\\\", StringComparison.Ordinal))
                {
                    return false;
                }

                uint driveType = LocalFolderNativeMethods.GetDriveType(root);
                if (driveType == DriveUnknown
                    || driveType == DriveNoRootDirectory
                    || driveType == DriveRemote
                    || !Directory.Exists(candidate))
                {
                    return false;
                }

                normalizedPath = candidate;
                return true;
            }
            catch (ArgumentException)
            {
                return false;
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
        }

        private static class LocalFolderNativeMethods
        {
            [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
            internal static extern uint GetDriveType(string rootPathName);
        }
    }

    internal static class FolderLauncher
    {
        public static string TryOpen(string path)
        {
            try
            {
                string localPath;
                if (!LocalFolderPolicy.TryNormalizeExistingDirectory(path, out localPath))
                {
                    return "La carpeta ya no está disponible.";
                }

                ProcessStartInfo info = new ProcessStartInfo();
                info.FileName = localPath;
                info.UseShellExecute = true;
                Process process = Process.Start(info);
                if (process != null)
                {
                    process.Dispose();
                }

                return null;
            }
            catch (Exception exception)
            {
                if (exception is ArgumentException
                    || exception is IOException
                    || exception is InvalidOperationException
                    || exception is System.ComponentModel.Win32Exception
                    || exception is UnauthorizedAccessException)
                {
                    return "Windows no pudo abrir la carpeta.";
                }

                throw;
            }
        }
    }
}
