using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security;
using System.Text;

namespace Raudo
{
    internal enum VoiceCommandKind
    {
        Unknown,
        OpenApplication,
        OpenYouTube,
        OpenWeather,
        OpenSalto,
        OpenRaudo,
        StartPulse,
        StopPulse,
        DesktopLeft,
        DesktopRight,
        DesktopAdjacent,
        DesktopCreate,
        DesktopOverview,
        ScreenCapture,
        MediaPlayPause,
        MediaPrevious,
        MediaNext,
        VolumeMute,
        VolumeDown,
        VolumeUp,
        Calculation,
        Conversion,
        DictationUnavailable
    }

    internal sealed class VoiceCommand
    {
        private VoiceCommand(
            VoiceCommandKind kind,
            string title,
            string detail,
            string argument,
            string applicationIdentifier)
        {
            Kind = kind;
            Title = title ?? string.Empty;
            Detail = detail ?? string.Empty;
            Argument = argument ?? string.Empty;
            ApplicationIdentifier = applicationIdentifier ?? string.Empty;
        }

        public VoiceCommandKind Kind { get; private set; }
        public string Title { get; private set; }
        public string Detail { get; private set; }
        public string Argument { get; private set; }
        public string ApplicationIdentifier { get; private set; }

        public bool IsRecognized
        {
            get { return Kind != VoiceCommandKind.Unknown; }
        }

        public static VoiceCommand Create(
            VoiceCommandKind kind,
            string title,
            string detail)
        {
            return new VoiceCommand(kind, title, detail, string.Empty, string.Empty);
        }

        public static VoiceCommand Application(InstalledApplication application)
        {
            return new VoiceCommand(
                VoiceCommandKind.OpenApplication,
                "Abriendo " + application.Name,
                "Aplicación instalada",
                application.Name,
                application.Identifier);
        }

        public static VoiceCommand Calculation(string expression, string result)
        {
            return new VoiceCommand(
                VoiceCommandKind.Calculation,
                result,
                expression,
                result,
                string.Empty);
        }

        public static VoiceCommand Conversion(string description, string result)
        {
            return new VoiceCommand(
                VoiceCommandKind.Conversion,
                result,
                description,
                result,
                string.Empty);
        }

        public static VoiceCommand Unknown(string detail)
        {
            return new VoiceCommand(
                VoiceCommandKind.Unknown,
                "No entendí esa orden",
                detail,
                string.Empty,
                string.Empty);
        }
    }

    internal static class VoiceCommandParser
    {
        public static VoiceCommand Parse(
            string phrase,
            IList<InstalledApplication> applications)
        {
            string normalized = RaudoActionCatalog.Normalize(phrase);
            if (normalized.StartsWith("raudo ", StringComparison.Ordinal))
            {
                normalized = normalized.Substring(6).Trim();
            }

            if (normalized.Length == 0)
            {
                return VoiceCommand.Unknown("Prueba con “abre Excel” o “cuánto es 12 por 8”.");
            }

            VoiceCommand fixedCommand = ParseFixed(normalized);
            if (fixedCommand.IsRecognized)
            {
                return fixedCommand;
            }

            VoiceCommand calculation = ParseCalculation(normalized);
            if (calculation.IsRecognized)
            {
                return calculation;
            }

            VoiceCommand conversion = ParseConversion(normalized);
            if (conversion.IsRecognized)
            {
                return conversion;
            }

            string applicationName;
            if (TryRemovePrefix(normalized, "abre ", out applicationName)
                || TryRemovePrefix(normalized, "abrir ", out applicationName)
                || TryRemovePrefix(normalized, "open ", out applicationName))
            {
                InstalledApplication match = FindApplication(applicationName, applications);
                return match == null
                    ? VoiceCommand.Unknown("No encontré una aplicación con ese nombre.")
                    : VoiceCommand.Application(match);
            }

            return VoiceCommand.Unknown(
                "La orden no coincide con una acción local y segura de Raudo.");
        }

        private static VoiceCommand ParseFixed(string command)
        {
            switch (command)
            {
                case "abre youtube":
                case "abrir youtube":
                case "open youtube":
                    return VoiceCommand.Create(
                        VoiceCommandKind.OpenYouTube,
                        "Abriendo YouTube",
                        "Navegador predeterminado");
                case "muestrame el clima":
                case "muestra el clima":
                case "abre el clima":
                case "como esta el clima":
                case "show me the weather":
                case "show the weather":
                case "open weather":
                case "what is the weather":
                    return VoiceCommand.Create(
                        VoiceCommandKind.OpenWeather,
                        "Mostrando el clima",
                        "Búsqueda en el navegador predeterminado");
                case "abre salto":
                case "muestra salto":
                case "open salto":
                case "show salto":
                    return VoiceCommand.Create(
                        VoiceCommandKind.OpenSalto,
                        "Abriendo Salto",
                        "Comandos y búsqueda local");
                case "abre raudo":
                case "muestra raudo":
                case "open raudo":
                case "show raudo":
                    return VoiceCommand.Create(
                        VoiceCommandKind.OpenRaudo,
                        "Abriendo Raudo",
                        "Controles y preferencias");
                case "inicia pulso":
                case "activa pulso":
                case "enciende pulso":
                case "start pulse":
                case "activate pulse":
                case "turn on pulse":
                    return VoiceCommand.Create(
                        VoiceCommandKind.StartPulse,
                        "Pulso encendido",
                        "Usará la duración configurada");
                case "deten pulso":
                case "detiene pulso":
                case "apaga pulso":
                case "stop pulse":
                case "deactivate pulse":
                case "turn off pulse":
                    return VoiceCommand.Create(
                        VoiceCommandKind.StopPulse,
                        "Pulso apagado",
                        "El equipo vuelve a su comportamiento normal");
                case "escritorio izquierdo":
                case "escritorio de la izquierda":
                case "escritorio anterior":
                case "cambia al escritorio izquierdo":
                case "left desktop":
                case "previous desktop":
                case "switch to the left desktop":
                    return VoiceCommand.Create(
                        VoiceCommandKind.DesktopLeft,
                        "Escritorio anterior",
                        "Cambiando a la izquierda");
                case "escritorio derecho":
                case "escritorio de la derecha":
                case "escritorio siguiente":
                case "cambia al escritorio derecho":
                case "right desktop":
                case "next desktop":
                case "switch to the right desktop":
                    return VoiceCommand.Create(
                        VoiceCommandKind.DesktopRight,
                        "Escritorio siguiente",
                        "Cambiando a la derecha");
                case "cambia de escritorio":
                case "switch desktop":
                case "change desktop":
                    return VoiceCommand.Create(
                        VoiceCommandKind.DesktopAdjacent,
                        "Cambiando de escritorio",
                        "Escritorio virtual adyacente");
                case "crea un escritorio":
                case "crea un nuevo escritorio":
                case "nuevo escritorio":
                case "create a desktop":
                case "create a new desktop":
                case "new desktop":
                    return VoiceCommand.Create(
                        VoiceCommandKind.DesktopCreate,
                        "Escritorio creado",
                        "Nuevo espacio de trabajo");
                case "muestrame los escritorios":
                case "muestra los escritorios":
                case "vista de escritorios":
                case "show desktops":
                case "show my desktops":
                case "open task view":
                    return VoiceCommand.Create(
                        VoiceCommandKind.DesktopOverview,
                        "Vista de escritorios",
                        "Ventanas y espacios abiertos");
                case "recorta pantalla":
                case "toma una captura":
                case "captura pantalla":
                case "crop screen":
                case "take a screenshot":
                case "capture screen":
                    return VoiceCommand.Create(
                        VoiceCommandKind.ScreenCapture,
                        "Recortar pantalla",
                        "Herramienta Recortes de Windows");
                case "reproduce":
                case "pausa":
                case "reproduce o pausa":
                case "play":
                case "pause":
                case "play or pause":
                    return VoiceCommand.Create(
                        VoiceCommandKind.MediaPlayPause,
                        "Control multimedia",
                        "Reproducir o pausar");
                case "cancion anterior":
                case "pista anterior":
                case "previous song":
                case "previous track":
                    return VoiceCommand.Create(
                        VoiceCommandKind.MediaPrevious,
                        "Control multimedia",
                        "Pista anterior");
                case "siguiente cancion":
                case "siguiente pista":
                case "next song":
                case "next track":
                    return VoiceCommand.Create(
                        VoiceCommandKind.MediaNext,
                        "Control multimedia",
                        "Pista siguiente");
                case "silencia":
                case "silencia el volumen":
                case "mute":
                case "mute volume":
                    return VoiceCommand.Create(
                        VoiceCommandKind.VolumeMute,
                        "Control multimedia",
                        "Silenciar o restaurar volumen");
                case "baja el volumen":
                case "lower volume":
                case "volume down":
                    return VoiceCommand.Create(
                        VoiceCommandKind.VolumeDown,
                        "Control multimedia",
                        "Bajar volumen");
                case "sube el volumen":
                case "raise volume":
                case "volume up":
                    return VoiceCommand.Create(
                        VoiceCommandKind.VolumeUp,
                        "Control multimedia",
                        "Subir volumen");
                case "transcribe":
                case "inicia dictado":
                case "transcribe audio":
                case "start dictation":
                    return VoiceCommand.Create(
                        VoiceCommandKind.DictationUnavailable,
                        "Dictado no disponible",
                        "Esta prueba sólo usa órdenes locales; no envía voz a servicios remotos.");
                default:
                    return VoiceCommand.Unknown(string.Empty);
            }
        }

        private static VoiceCommand ParseCalculation(string command)
        {
            string expression;
            if (!TryRemovePrefix(command, "cuanto es ", out expression)
                && !TryRemovePrefix(command, "calcula ", out expression)
                && !TryRemovePrefix(command, "what is ", out expression)
                && !TryRemovePrefix(command, "calculate ", out expression))
            {
                return VoiceCommand.Unknown(string.Empty);
            }

            string[] spokenOperators =
            {
                " dividido entre ",
                " dividido por ",
                " divided by ",
                " multiplied by ",
                " menos ",
                " mas ",
                " minus ",
                " plus ",
                " times ",
                " por "
            };
            string[] symbols = { "/", "/", "/", "*", "-", "+", "-", "+", "*", "*" };
            int selected = -1;
            int position = -1;
            for (int index = 0; index < spokenOperators.Length; index++)
            {
                position = expression.IndexOf(
                    spokenOperators[index],
                    StringComparison.Ordinal);
                if (position >= 0)
                {
                    selected = index;
                    break;
                }
            }

            if (selected < 0)
            {
                return VoiceCommand.Unknown("La operación no tiene un operador permitido.");
            }

            string leftText = expression.Substring(0, position).Trim();
            string rightText = expression.Substring(
                position + spokenOperators[selected].Length).Trim();
            int left;
            int right;
            if (!VoiceNumberWords.TryParse(leftText, out left)
                || !VoiceNumberWords.TryParse(rightText, out right))
            {
                return VoiceCommand.Unknown("Los operandos deben estar entre cero y novecientos noventa y nueve.");
            }

            decimal result;
            switch (symbols[selected])
            {
                case "+":
                    result = left + right;
                    break;
                case "-":
                    result = left - right;
                    break;
                case "*":
                    result = left * right;
                    break;
                default:
                    if (right == 0)
                    {
                        return VoiceCommand.Unknown("No se puede dividir entre cero.");
                    }

                    result = left / (decimal)right;
                    break;
            }

            string resultText = result.ToString("0.####", CultureInfo.CurrentCulture);
            string displayExpression = string.Format(
                CultureInfo.CurrentCulture,
                "{0} {1} {2}",
                left,
                symbols[selected],
                right);
            return VoiceCommand.Calculation(displayExpression, resultText);
        }

        private static VoiceCommand ParseConversion(string command)
        {
            string expression;
            bool english = TryRemovePrefix(command, "convert ", out expression);
            if (!english && !TryRemovePrefix(command, "convierte ", out expression))
            {
                return VoiceCommand.Unknown(string.Empty);
            }

            string separatorText = english ? " to " : " a ";
            int separator = expression.LastIndexOf(separatorText, StringComparison.Ordinal);
            if (separator <= 0)
            {
                return VoiceCommand.Unknown("La conversión debe indicar una unidad de destino.");
            }

            string left = expression.Substring(0, separator).Trim();
            string targetUnit = expression.Substring(separator + separatorText.Length).Trim();
            int unitSeparator = left.LastIndexOf(' ');
            if (unitSeparator <= 0 || targetUnit.Length == 0)
            {
                return VoiceCommand.Unknown("La conversión no contiene unidades válidas.");
            }

            string numberText = left.Substring(0, unitSeparator).Trim();
            string sourceUnit = left.Substring(unitSeparator + 1).Trim();
            int value;
            if (!VoiceNumberWords.TryParse(numberText, out value))
            {
                return VoiceCommand.Unknown("El valor debe estar entre cero y novecientos noventa y nueve.");
            }

            string displayValue;
            string description;
            string query = string.Format(
                CultureInfo.InvariantCulture,
                "{0} {1} a {2}",
                value,
                sourceUnit,
                targetUnit);
            return QuickResultProvider.TryConvert(query, out displayValue, out description)
                ? VoiceCommand.Conversion(description, displayValue)
                : VoiceCommand.Unknown("Las unidades no son compatibles o no están disponibles.");
        }

        private static InstalledApplication FindApplication(
            string requestedName,
            IList<InstalledApplication> applications)
        {
            if (applications == null)
            {
                return null;
            }

            string normalized = RaudoActionCatalog.Normalize(requestedName);
            InstalledApplication exactMatch = null;
            int exactCount = 0;
            InstalledApplication prefixMatch = null;
            int prefixCount = 0;
            foreach (InstalledApplication application in applications)
            {
                if (application == null)
                {
                    continue;
                }

                bool exact = false;
                bool prefix = false;
                EvaluateApplicationName(application.Name, normalized, ref exact, ref prefix);
                if (application.Aliases != null)
                {
                    foreach (string alias in application.Aliases)
                    {
                        EvaluateApplicationName(alias, normalized, ref exact, ref prefix);
                    }
                }

                if (exact)
                {
                    exactMatch = application;
                    exactCount++;
                }
                else if (prefix)
                {
                    prefixMatch = application;
                    prefixCount++;
                }
            }

            return exactCount == 1
                ? exactMatch
                : exactCount == 0 && prefixCount == 1
                    ? prefixMatch
                    : null;
        }

        private static void EvaluateApplicationName(
            string value,
            string requested,
            ref bool exact,
            ref bool prefix)
        {
            string candidate = RaudoActionCatalog.Normalize(value);
            if (candidate.Length == 0)
            {
                return;
            }

            if (string.Equals(candidate, requested, StringComparison.Ordinal))
            {
                exact = true;
                return;
            }

            if (candidate.StartsWith(requested, StringComparison.Ordinal)
                || requested.StartsWith(candidate, StringComparison.Ordinal))
            {
                prefix = true;
            }
        }

        private static bool TryRemovePrefix(
            string value,
            string prefix,
            out string remainder)
        {
            if (value.StartsWith(prefix, StringComparison.Ordinal))
            {
                remainder = value.Substring(prefix.Length).Trim();
                return remainder.Length > 0;
            }

            remainder = string.Empty;
            return false;
        }
    }

    internal static class VoiceGrammarBuilder
    {
        public const int MaximumApplications = 384;
        public const int MaximumApplicationPhrases = MaximumApplications * 4;

        private static readonly string[] SpanishFixedCommands =
        {
            "abre youtube",
            "abrir youtube",
            "muéstrame el clima",
            "muestra el clima",
            "abre el clima",
            "cómo está el clima",
            "abre salto",
            "muestra salto",
            "abre raudo",
            "muestra raudo",
            "inicia pulso",
            "activa pulso",
            "enciende pulso",
            "detén pulso",
            "detiene pulso",
            "apaga pulso",
            "escritorio izquierdo",
            "escritorio de la izquierda",
            "escritorio anterior",
            "cambia al escritorio izquierdo",
            "escritorio derecho",
            "escritorio de la derecha",
            "escritorio siguiente",
            "cambia al escritorio derecho",
            "cambia de escritorio",
            "crea un escritorio",
            "crea un nuevo escritorio",
            "nuevo escritorio",
            "muéstrame los escritorios",
            "muestra los escritorios",
            "vista de escritorios",
            "recorta pantalla",
            "toma una captura",
            "captura pantalla",
            "reproduce",
            "pausa",
            "reproduce o pausa",
            "canción anterior",
            "pista anterior",
            "siguiente canción",
            "siguiente pista",
            "silencia",
            "silencia el volumen",
            "baja el volumen",
            "sube el volumen",
            "transcribe",
            "inicia dictado"
        };

        private static readonly string[] EnglishFixedCommands =
        {
            "open youtube",
            "show me the weather",
            "show the weather",
            "open weather",
            "what is the weather",
            "open salto",
            "show salto",
            "open raudo",
            "show raudo",
            "start pulse",
            "activate pulse",
            "turn on pulse",
            "stop pulse",
            "deactivate pulse",
            "turn off pulse",
            "left desktop",
            "previous desktop",
            "switch to the left desktop",
            "right desktop",
            "next desktop",
            "switch to the right desktop",
            "switch desktop",
            "change desktop",
            "create a desktop",
            "create a new desktop",
            "new desktop",
            "show desktops",
            "show my desktops",
            "open task view",
            "crop screen",
            "take a screenshot",
            "capture screen",
            "play",
            "pause",
            "play or pause",
            "previous song",
            "previous track",
            "next song",
            "next track",
            "mute",
            "mute volume",
            "lower volume",
            "volume down",
            "raise volume",
            "volume up",
            "transcribe",
            "transcribe audio",
            "start dictation"
        };

        private static readonly string[] SpanishConversionUnits =
        {
            "milímetros", "centímetros", "metros", "kilómetros", "pulgadas", "pies",
            "yardas", "millas", "miligramos", "gramos", "kilogramos", "kilos", "onzas",
            "libras", "celsius", "fahrenheit", "kelvin", "milisegundos", "segundos",
            "minutos", "horas", "días", "bytes", "kilobytes", "megabytes", "gigabytes",
            "terabytes"
        };

        private static readonly string[] EnglishConversionUnits =
        {
            "millimeters", "centimeters", "meters", "kilometers", "inches", "feet",
            "yards", "miles", "milligrams", "grams", "kilograms", "ounces", "pounds",
            "celsius", "fahrenheit", "kelvin", "milliseconds", "seconds", "minutes",
            "hours", "days", "bytes", "kilobytes", "megabytes", "gigabytes", "terabytes"
        };

        public static string BuildSrgs(string languageTag)
        {
            if (string.IsNullOrWhiteSpace(languageTag))
            {
                throw new ArgumentException("Se requiere un idioma para la gramática.", "languageTag");
            }

            bool english = VoiceLanguagePolicy.IsEnglish(languageTag);
            string[] fixedCommands = english
                ? EnglishFixedCommands
                : SpanishFixedCommands;
            string[] conversionUnits = english
                ? EnglishConversionUnits
                : SpanishConversionUnits;

            StringBuilder xml = new StringBuilder(50000);
            xml.Append("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
            xml.Append("<grammar version=\"1.0\" xml:lang=\"");
            xml.Append(SecurityElement.Escape(languageTag));
            xml.Append("\" root=\"command\"");
            xml.Append(" xmlns=\"http://www.w3.org/2001/06/grammar\">");
            xml.Append("<rule id=\"command\" scope=\"public\"><item>");
            xml.Append("<item repeat=\"0-1\">raudo</item><one-of>");
            foreach (string command in fixedCommands)
            {
                xml.Append("<item>");
                xml.Append(SecurityElement.Escape(command));
                xml.Append("</item>");
            }

            if (english)
            {
                xml.Append("<item><one-of><item>what is</item><item>calculate</item>");
                xml.Append("</one-of><ruleref uri=\"#number\"/><one-of>");
                xml.Append("<item>times</item><item>multiplied by</item><item>plus</item>");
                xml.Append("<item>minus</item><item>divided by</item></one-of>");
                xml.Append("<ruleref uri=\"#number\"/></item>");
                xml.Append("<item>convert<ruleref uri=\"#number\"/><one-of>");
            }
            else
            {
                xml.Append("<item><one-of><item>cuánto es</item><item>cuanto es</item>");
                xml.Append("<item>calcula</item></one-of><ruleref uri=\"#number\"/>");
                xml.Append("<one-of><item>por</item><item>más</item><item>mas</item>");
                xml.Append("<item>menos</item><item>dividido entre</item>");
                xml.Append("<item>dividido por</item></one-of><ruleref uri=\"#number\"/></item>");
                xml.Append("<item>convierte<ruleref uri=\"#number\"/><one-of>");
            }

            foreach (string unit in conversionUnits)
            {
                xml.Append("<item>");
                xml.Append(SecurityElement.Escape(unit));
                xml.Append("</item>");
            }

            xml.Append(english ? "</one-of>to<one-of>" : "</one-of>a<one-of>");
            foreach (string unit in conversionUnits)
            {
                xml.Append("<item>");
                xml.Append(SecurityElement.Escape(unit));
                xml.Append("</item>");
            }

            xml.Append("</one-of></item>");
            xml.Append("</one-of></item></rule>");
            xml.Append("<rule id=\"number\"><one-of>");
            for (int value = 0; value <= 999; value++)
            {
                xml.Append("<item>");
                xml.Append(SecurityElement.Escape(
                    english
                        ? VoiceNumberWords.ToEnglish(value)
                        : VoiceNumberWords.ToSpanish(value)));
                xml.Append("</item>");
            }

            xml.Append("</one-of></rule></grammar>");
            return xml.ToString();
        }

        public static IList<string> BuildApplicationPhrases(
            IList<InstalledApplication> applications)
        {
            return BuildApplicationPhrases(applications, "es-MX");
        }

        public static IList<string> BuildApplicationPhrases(
            IList<InstalledApplication> applications,
            string languageTag)
        {
            List<string> phrases = new List<string>();
            HashSet<string> unique = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (applications == null)
            {
                return phrases;
            }

            foreach (InstalledApplication application in applications)
            {
                if (application == null || phrases.Count >= MaximumApplicationPhrases)
                {
                    break;
                }

                AddApplicationPhrases(
                    application.Name,
                    languageTag,
                    phrases,
                    unique);
                if (application.Aliases == null)
                {
                    continue;
                }

                foreach (string alias in application.Aliases)
                {
                    AddApplicationPhrases(alias, languageTag, phrases, unique);
                    if (phrases.Count >= MaximumApplicationPhrases)
                    {
                        break;
                    }
                }
            }

            return phrases;
        }

        private static void AddApplicationPhrases(
            string value,
            string languageTag,
            IList<string> phrases,
            ISet<string> unique)
        {
            string name = NormalizeGrammarPhrase(value);
            if (name.Length == 0)
            {
                return;
            }

            string verb = VoiceLanguagePolicy.IsEnglish(languageTag)
                ? "open "
                : "abre ";
            string direct = verb + name;
            if (phrases.Count < MaximumApplicationPhrases && unique.Add(direct))
            {
                phrases.Add(direct);
            }

            string addressed = "raudo " + verb + name;
            if (phrases.Count < MaximumApplicationPhrases && unique.Add(addressed))
            {
                phrases.Add(addressed);
            }
        }

        private static string NormalizeGrammarPhrase(string value)
        {
            string normalized = RaudoActionCatalog.Normalize(value);
            StringBuilder result = new StringBuilder(normalized.Length);
            bool previousSpace = false;
            foreach (char character in normalized)
            {
                bool allowed = char.IsLetterOrDigit(character);
                if (allowed)
                {
                    result.Append(character);
                    previousSpace = false;
                }
                else if (!previousSpace && result.Length > 0)
                {
                    result.Append(' ');
                    previousSpace = true;
                }
            }

            return result.ToString().Trim();
        }
    }

    internal static class VoiceSessionPolicy
    {
        public static bool ShouldRetry(
            VoiceRecognitionOutcome outcome,
            IList<InstalledApplication> applications)
        {
            if (outcome == null)
            {
                return false;
            }

            if (outcome.Kind == VoiceRecognitionOutcomeKind.NotUnderstood)
            {
                return true;
            }

            return outcome.Kind == VoiceRecognitionOutcomeKind.Success
                && !VoiceCommandParser.Parse(outcome.Text, applications).IsRecognized;
        }

        public static string DescribeRetry(
            VoiceRecognitionOutcome outcome,
            IList<InstalledApplication> applications)
        {
            if (outcome == null)
            {
                return "Vuelvo a escuchar una vez más.";
            }

            if (!string.IsNullOrWhiteSpace(outcome.Text))
            {
                return "Escuché “" + outcome.Text + "”. Vuelvo a escuchar una vez más.";
            }

            string message = outcome.Message;
            if (outcome.Kind == VoiceRecognitionOutcomeKind.Success)
            {
                VoiceCommand command = VoiceCommandParser.Parse(outcome.Text, applications);
                message = command.Detail;
            }

            return string.IsNullOrWhiteSpace(message)
                ? "Vuelvo a escuchar una vez más."
                : message + " Vuelvo a escuchar una vez más.";
        }
    }

    internal static class VoiceNumberWords
    {
        private static readonly IDictionary<string, int> Values = CreateValues();

        public static bool TryParse(string value, out int result)
        {
            string normalized = RaudoActionCatalog.Normalize(value);
            int numeric;
            if (int.TryParse(normalized, NumberStyles.Integer, CultureInfo.InvariantCulture, out numeric)
                && numeric >= 0
                && numeric <= 999)
            {
                result = numeric;
                return true;
            }

            return Values.TryGetValue(normalized, out result);
        }

        public static string ToSpanish(int value)
        {
            if (value < 0 || value > 999)
            {
                throw new ArgumentOutOfRangeException("value");
            }

            if (value < 30)
            {
                return Small(value);
            }

            if (value < 100)
            {
                int tens = value / 10;
                int units = value % 10;
                string tensWord = new[]
                {
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    "treinta",
                    "cuarenta",
                    "cincuenta",
                    "sesenta",
                    "setenta",
                    "ochenta",
                    "noventa"
                }[tens];
                return units == 0 ? tensWord : tensWord + " y " + Small(units);
            }

            if (value == 100)
            {
                return "cien";
            }

            int hundreds = value / 100;
            int remainder = value % 100;
            string prefix;
            switch (hundreds)
            {
                case 1: prefix = "ciento"; break;
                case 2: prefix = "doscientos"; break;
                case 3: prefix = "trescientos"; break;
                case 4: prefix = "cuatrocientos"; break;
                case 5: prefix = "quinientos"; break;
                case 6: prefix = "seiscientos"; break;
                case 7: prefix = "setecientos"; break;
                case 8: prefix = "ochocientos"; break;
                default: prefix = "novecientos"; break;
            }

            return remainder == 0 ? prefix : prefix + " " + ToSpanish(remainder);
        }

        public static string ToEnglish(int value)
        {
            if (value < 0 || value > 999)
            {
                throw new ArgumentOutOfRangeException("value");
            }

            if (value < 20)
            {
                string[] small =
                {
                    "zero", "one", "two", "three", "four", "five", "six", "seven",
                    "eight", "nine", "ten", "eleven", "twelve", "thirteen", "fourteen",
                    "fifteen", "sixteen", "seventeen", "eighteen", "nineteen"
                };
                return small[value];
            }

            if (value < 100)
            {
                string[] tensWords =
                {
                    string.Empty,
                    string.Empty,
                    "twenty",
                    "thirty",
                    "forty",
                    "fifty",
                    "sixty",
                    "seventy",
                    "eighty",
                    "ninety"
                };
                int tens = value / 10;
                int units = value % 10;
                return units == 0
                    ? tensWords[tens]
                    : tensWords[tens] + " " + ToEnglish(units);
            }

            int hundreds = value / 100;
            int remainder = value % 100;
            string prefix = ToEnglish(hundreds) + " hundred";
            return remainder == 0 ? prefix : prefix + " " + ToEnglish(remainder);
        }

        private static string Small(int value)
        {
            string[] words =
            {
                "cero", "uno", "dos", "tres", "cuatro", "cinco", "seis", "siete",
                "ocho", "nueve", "diez", "once", "doce", "trece", "catorce", "quince",
                "dieciséis", "diecisiete", "dieciocho", "diecinueve", "veinte", "veintiuno",
                "veintidós", "veintitrés", "veinticuatro", "veinticinco", "veintiséis",
                "veintisiete", "veintiocho", "veintinueve"
            };
            return words[value];
        }

        private static IDictionary<string, int> CreateValues()
        {
            Dictionary<string, int> values = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int value = 0; value <= 999; value++)
            {
                values[RaudoActionCatalog.Normalize(ToSpanish(value))] = value;
                values[RaudoActionCatalog.Normalize(ToEnglish(value))] = value;
            }

            return values;
        }
    }
}
