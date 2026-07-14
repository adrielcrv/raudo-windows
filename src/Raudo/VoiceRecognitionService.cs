using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Globalization;
using Windows.Media.SpeechRecognition;
using Windows.Storage;

namespace Raudo
{
    internal enum VoiceRecognitionPhase
    {
        Preparing,
        Listening
    }

    internal enum VoiceRecognitionOutcomeKind
    {
        Success,
        NotUnderstood,
        Cancelled,
        Unavailable,
        Error
    }

    internal sealed class VoiceRecognitionPhaseEventArgs : EventArgs
    {
        public VoiceRecognitionPhaseEventArgs(VoiceRecognitionPhase phase)
        {
            Phase = phase;
        }

        public VoiceRecognitionPhase Phase { get; private set; }
    }

    internal sealed class VoiceRecognitionOutcome
    {
        public VoiceRecognitionOutcome(
            VoiceRecognitionOutcomeKind kind,
            string text,
            string message)
        {
            Kind = kind;
            Text = text ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public VoiceRecognitionOutcomeKind Kind { get; private set; }
        public string Text { get; private set; }
        public string Message { get; private set; }
    }

    internal sealed class VoiceAvailability
    {
        public VoiceAvailability(bool available, string languageTag, string message)
        {
            IsAvailable = available;
            LanguageTag = languageTag ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public bool IsAvailable { get; private set; }
        public string LanguageTag { get; private set; }
        public string Message { get; private set; }
    }

    internal sealed class VoiceRecognitionService : IDisposable
    {
        private readonly object sync = new object();
        private CancellationTokenSource cancellation;
        private SpeechRecognizer activeRecognizer;
        private bool disposed;
        private bool listening;

        public event EventHandler<VoiceRecognitionPhaseEventArgs> PhaseChanged;

        public bool IsListening
        {
            get
            {
                lock (sync)
                {
                    return listening;
                }
            }
        }

        public static VoiceAvailability GetAvailability()
        {
            try
            {
                Language language = FindSpanishLanguage();
                if (language == null)
                {
                    return new VoiceAvailability(
                        false,
                        string.Empty,
                        "Instala Voz para Español (México) en Configuración de Windows.");
                }

                return new VoiceAvailability(
                    true,
                    language.LanguageTag,
                    "Voz local de Windows lista");
            }
            catch (Exception)
            {
                return new VoiceAvailability(
                    false,
                    string.Empty,
                    "Windows no pudo consultar los idiomas de voz instalados.");
            }
        }

        public Task<VoiceRecognitionOutcome> ListenOnceAsync(
            IList<InstalledApplication> applications)
        {
            return ListenSessionAsync(applications, 1, null, null);
        }

        public async Task<VoiceRecognitionOutcome> ListenSessionAsync(
            IList<InstalledApplication> applications,
            int maximumAttempts,
            Func<VoiceRecognitionOutcome, bool> shouldRetry,
            Action<VoiceRecognitionOutcome, int> retrying)
        {
            if (maximumAttempts < 1 || maximumAttempts > 2)
            {
                throw new ArgumentOutOfRangeException("maximumAttempts");
            }

            CancellationTokenSource localCancellation;
            lock (sync)
            {
                if (disposed)
                {
                    return new VoiceRecognitionOutcome(
                        VoiceRecognitionOutcomeKind.Unavailable,
                        string.Empty,
                        "La entrada de voz ya no está disponible.");
                }

                if (listening)
                {
                    return new VoiceRecognitionOutcome(
                        VoiceRecognitionOutcomeKind.Cancelled,
                        string.Empty,
                        "La escucha anterior sigue activa.");
                }

                listening = true;
                cancellation = new CancellationTokenSource();
                localCancellation = cancellation;
            }

            SpeechRecognizer recognizer = null;
            try
            {
                RaisePhase(VoiceRecognitionPhase.Preparing);
                Language language = FindSpanishLanguage();
                if (language == null)
                {
                    return new VoiceRecognitionOutcome(
                        VoiceRecognitionOutcomeKind.Unavailable,
                        string.Empty,
                        "Español (México) no está disponible como idioma de voz en Windows.");
                }

                recognizer = new SpeechRecognizer(language);
                lock (sync)
                {
                    activeRecognizer = recognizer;
                }

                recognizer.Timeouts.InitialSilenceTimeout = TimeSpan.FromSeconds(6);
                recognizer.Timeouts.BabbleTimeout = TimeSpan.FromSeconds(4);
                recognizer.Timeouts.EndSilenceTimeout = TimeSpan.FromMilliseconds(900);

                string grammarPath = EnsureGrammarFile();
                StorageFile grammarFile = await StorageFile
                    .GetFileFromPathAsync(grammarPath)
                    .AsTask(localCancellation.Token);
                recognizer.Constraints.Add(
                    new SpeechRecognitionGrammarFileConstraint(
                        grammarFile,
                        "raudo.local.commands"));

                IList<string> applicationPhrases =
                    VoiceGrammarBuilder.BuildApplicationPhrases(applications);
                if (applicationPhrases.Count > 0)
                {
                    recognizer.Constraints.Add(
                        new SpeechRecognitionListConstraint(
                            applicationPhrases,
                            "raudo.local.applications"));
                }

                SpeechRecognitionCompilationResult compilation = await recognizer
                    .CompileConstraintsAsync()
                    .AsTask(localCancellation.Token);
                if (compilation.Status != SpeechRecognitionResultStatus.Success)
                {
                    return new VoiceRecognitionOutcome(
                        VoiceRecognitionOutcomeKind.Error,
                        string.Empty,
                        DescribeStatus(compilation.Status));
                }

                for (int attempt = 0; attempt < maximumAttempts; attempt++)
                {
                    RaisePhase(VoiceRecognitionPhase.Listening);
                    SpeechRecognitionResult result = await recognizer
                        .RecognizeAsync()
                        .AsTask(localCancellation.Token);
                    VoiceRecognitionOutcome outcome;
                    if (result.Status != SpeechRecognitionResultStatus.Success)
                    {
                        outcome = MapResultStatus(result.Status);
                    }
                    else if (result.Confidence == SpeechRecognitionConfidence.Rejected
                        || result.Confidence == SpeechRecognitionConfidence.Low
                        || string.IsNullOrWhiteSpace(result.Text))
                    {
                        outcome = new VoiceRecognitionOutcome(
                            VoiceRecognitionOutcomeKind.NotUnderstood,
                            result.Text,
                            "No reconocí la orden con suficiente claridad.");
                    }
                    else
                    {
                        outcome = new VoiceRecognitionOutcome(
                            VoiceRecognitionOutcomeKind.Success,
                            result.Text,
                            string.Empty);
                    }

                    bool hasAnotherAttempt = attempt + 1 < maximumAttempts;
                    if (hasAnotherAttempt
                        && shouldRetry != null
                        && shouldRetry(outcome))
                    {
                        if (retrying != null)
                        {
                            retrying(outcome, attempt + 2);
                        }

                        await Task.Delay(750, localCancellation.Token);
                        continue;
                    }

                    return outcome;
                }

                return new VoiceRecognitionOutcome(
                    VoiceRecognitionOutcomeKind.NotUnderstood,
                    string.Empty,
                    "Windows no reconoció una orden válida.");
            }
            catch (OperationCanceledException)
            {
                return new VoiceRecognitionOutcome(
                    VoiceRecognitionOutcomeKind.Cancelled,
                    string.Empty,
                    "Escucha cancelada");
            }
            catch (UnauthorizedAccessException)
            {
                return new VoiceRecognitionOutcome(
                    VoiceRecognitionOutcomeKind.Unavailable,
                    string.Empty,
                    "Permite el acceso al micrófono para aplicaciones de escritorio en Windows.");
            }
            catch (COMException exception)
            {
                return new VoiceRecognitionOutcome(
                    VoiceRecognitionOutcomeKind.Error,
                    string.Empty,
                    DescribeComError(exception));
            }
            catch (Exception)
            {
                return new VoiceRecognitionOutcome(
                    VoiceRecognitionOutcomeKind.Error,
                    string.Empty,
                    "Windows no pudo completar la escucha. El micrófono ya fue liberado.");
            }
            finally
            {
                lock (sync)
                {
                    activeRecognizer = null;
                    listening = false;
                    if (ReferenceEquals(cancellation, localCancellation))
                    {
                        cancellation = null;
                    }
                }

                if (recognizer != null)
                {
                    recognizer.Dispose();
                }

                localCancellation.Dispose();
            }
        }

        public void Cancel()
        {
            CancellationTokenSource current;
            lock (sync)
            {
                current = cancellation;
            }

            if (current != null)
            {
                try
                {
                    current.Cancel();
                }
                catch (ObjectDisposedException)
                {
                }
            }
        }

        public void Dispose()
        {
            lock (sync)
            {
                if (disposed)
                {
                    return;
                }

                disposed = true;
            }

            Cancel();
        }

        internal static async Task<SpeechRecognitionResultStatus> CompileForTestingAsync(
            IList<InstalledApplication> applications)
        {
            Language language = FindSpanishLanguage();
            if (language == null)
            {
                return SpeechRecognitionResultStatus.TopicLanguageNotSupported;
            }

            using (SpeechRecognizer recognizer = new SpeechRecognizer(language))
            {
                StorageFile grammarFile = await StorageFile
                    .GetFileFromPathAsync(EnsureGrammarFile())
                    .AsTask();
                recognizer.Constraints.Add(
                    new SpeechRecognitionGrammarFileConstraint(grammarFile));
                IList<string> phrases = VoiceGrammarBuilder.BuildApplicationPhrases(applications);
                if (phrases.Count > 0)
                {
                    recognizer.Constraints.Add(new SpeechRecognitionListConstraint(phrases));
                }

                SpeechRecognitionCompilationResult result = await recognizer
                    .CompileConstraintsAsync()
                    .AsTask();
                return result.Status;
            }
        }

        private void RaisePhase(VoiceRecognitionPhase phase)
        {
            EventHandler<VoiceRecognitionPhaseEventArgs> handler = PhaseChanged;
            if (handler != null)
            {
                handler(this, new VoiceRecognitionPhaseEventArgs(phase));
            }
        }

        private static Language FindSpanishLanguage()
        {
            Language fallback = null;
            foreach (Language language in SpeechRecognizer.SupportedGrammarLanguages)
            {
                if (string.Equals(
                    language.LanguageTag,
                    "es-MX",
                    StringComparison.OrdinalIgnoreCase))
                {
                    return language;
                }

                if (fallback == null
                    && language.LanguageTag.StartsWith("es-", StringComparison.OrdinalIgnoreCase))
                {
                    fallback = language;
                }
            }

            return fallback;
        }

        private static string EnsureGrammarFile()
        {
            string directory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Raudo",
                "voice");
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, "commands-es-MX.srgs.xml");
            string content = VoiceGrammarBuilder.BuildSrgs();
            bool write = true;
            try
            {
                write = !File.Exists(path)
                    || !string.Equals(
                        File.ReadAllText(path, Encoding.UTF8),
                        content,
                        StringComparison.Ordinal);
            }
            catch (IOException)
            {
                write = true;
            }

            if (write)
            {
                string temporary = path + ".tmp";
                File.WriteAllText(temporary, content, new UTF8Encoding(false));
                if (File.Exists(path))
                {
                    File.Replace(temporary, path, null, true);
                }
                else
                {
                    File.Move(temporary, path);
                }
            }

            return path;
        }

        private static VoiceRecognitionOutcome MapResultStatus(
            SpeechRecognitionResultStatus status)
        {
            switch (status)
            {
                case SpeechRecognitionResultStatus.UserCanceled:
                    return new VoiceRecognitionOutcome(
                        VoiceRecognitionOutcomeKind.Cancelled,
                        string.Empty,
                        "Escucha cancelada");
                case SpeechRecognitionResultStatus.AudioQualityFailure:
                    return new VoiceRecognitionOutcome(
                        VoiceRecognitionOutcomeKind.NotUnderstood,
                        string.Empty,
                        "El audio no fue suficientemente claro.");
                default:
                    return new VoiceRecognitionOutcome(
                        VoiceRecognitionOutcomeKind.NotUnderstood,
                        string.Empty,
                        DescribeStatus(status));
            }
        }

        private static string DescribeStatus(SpeechRecognitionResultStatus status)
        {
            switch (status)
            {
                case SpeechRecognitionResultStatus.TopicLanguageNotSupported:
                case SpeechRecognitionResultStatus.GrammarLanguageMismatch:
                    return "El idioma de voz de Windows no coincide con Español (México).";
                case SpeechRecognitionResultStatus.GrammarCompilationFailure:
                    return "Windows no pudo preparar las órdenes locales de Raudo.";
                case SpeechRecognitionResultStatus.AudioQualityFailure:
                    return "El audio no fue suficientemente claro.";
                case SpeechRecognitionResultStatus.UserCanceled:
                    return "Escucha cancelada";
                default:
                    return "Windows no reconoció una orden válida.";
            }
        }

        private static string DescribeComError(COMException exception)
        {
            const int AccessDenied = unchecked((int)0x80070005);
            return exception.ErrorCode == AccessDenied
                ? "Permite el acceso al micrófono para aplicaciones de escritorio en Windows."
                : "Windows no pudo iniciar el reconocimiento local de voz.";
        }
    }
}
