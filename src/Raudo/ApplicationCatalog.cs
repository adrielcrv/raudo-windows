using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;

namespace Raudo
{
    internal sealed class InstalledApplication
    {
        public InstalledApplication(string name, string identifier)
        {
            Name = name ?? string.Empty;
            Identifier = identifier ?? string.Empty;
        }

        public string Name { get; private set; }
        public string Identifier { get; private set; }
    }

    internal sealed class InstalledApplicationCatalog
    {
        private const int MaximumApplications = 1024;
        private readonly object sync = new object();
        private readonly Func<IList<InstalledApplication>> source;
        private List<InstalledApplication> snapshot = new List<InstalledApplication>();
        private bool loading;
        private bool loaded;
        private string loadError;

        public InstalledApplicationCatalog()
            : this(ReadAppsFolder)
        {
        }

        internal InstalledApplicationCatalog(Func<IList<InstalledApplication>> catalogSource)
        {
            if (catalogSource == null)
            {
                throw new ArgumentNullException("catalogSource");
            }

            source = catalogSource;
        }

        public event EventHandler LoadCompleted;

        public bool IsLoading
        {
            get
            {
                lock (sync)
                {
                    return loading;
                }
            }
        }

        public bool IsLoaded
        {
            get
            {
                lock (sync)
                {
                    return loaded;
                }
            }
        }

        public string LoadError
        {
            get
            {
                lock (sync)
                {
                    return loadError;
                }
            }
        }

        public void EnsureLoading()
        {
            lock (sync)
            {
                if (loaded || loading)
                {
                    return;
                }

                loading = true;
            }

            Thread worker = new Thread(LoadWorker);
            worker.Name = "Raudo application catalog";
            worker.IsBackground = true;
            worker.SetApartmentState(ApartmentState.STA);
            worker.Start();
        }

        public IList<InstalledApplication> GetSnapshot()
        {
            lock (sync)
            {
                return new List<InstalledApplication>(snapshot);
            }
        }

        internal void LoadNowForTesting()
        {
            lock (sync)
            {
                if (loaded || loading)
                {
                    return;
                }

                loading = true;
            }

            LoadWorker();
        }

        private void LoadWorker()
        {
            List<InstalledApplication> applications;
            string error = null;
            try
            {
                applications = Normalize(source());
            }
            catch (Exception)
            {
                applications = new List<InstalledApplication>();
                error = "Windows no pudo preparar el catálogo de aplicaciones.";
            }

            lock (sync)
            {
                snapshot = applications;
                loadError = error;
                loading = false;
                loaded = true;
            }

            EventHandler completed = LoadCompleted;
            if (completed != null)
            {
                try
                {
                    completed(this, EventArgs.Empty);
                }
                catch (ObjectDisposedException)
                {
                }
            }
        }

        private static List<InstalledApplication> Normalize(
            IList<InstalledApplication> applications)
        {
            Dictionary<string, InstalledApplication> unique =
                new Dictionary<string, InstalledApplication>(StringComparer.Ordinal);
            if (applications != null)
            {
                foreach (InstalledApplication application in applications)
                {
                    if (application == null
                        || string.IsNullOrWhiteSpace(application.Name)
                        || string.IsNullOrWhiteSpace(application.Identifier))
                    {
                        continue;
                    }

                    string key = RaudoActionCatalog.Normalize(application.Name);
                    if (key.Length == 0 || unique.ContainsKey(key))
                    {
                        continue;
                    }

                    unique.Add(
                        key,
                        new InstalledApplication(
                            application.Name.Trim(),
                            application.Identifier.Trim()));
                    if (unique.Count >= MaximumApplications)
                    {
                        break;
                    }
                }
            }

            List<InstalledApplication> result =
                new List<InstalledApplication>(unique.Values);
            result.Sort(delegate(InstalledApplication left, InstalledApplication right)
            {
                return string.Compare(
                    left.Name,
                    right.Name,
                    StringComparison.CurrentCultureIgnoreCase);
            });
            return result;
        }

        private static IList<InstalledApplication> ReadAppsFolder()
        {
            List<InstalledApplication> result = new List<InstalledApplication>();
            object shell = null;
            object folder = null;
            object items = null;
            try
            {
                Type shellType = Type.GetTypeFromProgID("Shell.Application", false);
                if (shellType == null)
                {
                    return result;
                }

                shell = Activator.CreateInstance(shellType);
                folder = Invoke(
                    shell,
                    "NameSpace",
                    BindingFlags.InvokeMethod,
                    new object[] { "shell:AppsFolder" });
                if (folder == null)
                {
                    return result;
                }

                items = Invoke(folder, "Items", BindingFlags.InvokeMethod, null);
                if (items == null)
                {
                    return result;
                }

                int count = Convert.ToInt32(
                    Invoke(items, "Count", BindingFlags.GetProperty, null));
                count = Math.Min(count, MaximumApplications * 2);
                for (int index = 0; index < count; index++)
                {
                    object item = null;
                    try
                    {
                        item = Invoke(
                            items,
                            "Item",
                            BindingFlags.InvokeMethod,
                            new object[] { index });
                        string name = Convert.ToString(
                            Invoke(item, "Name", BindingFlags.GetProperty, null));
                        string identifier = Convert.ToString(
                            Invoke(item, "Path", BindingFlags.GetProperty, null));
                        if (!string.IsNullOrWhiteSpace(name)
                            && !string.IsNullOrWhiteSpace(identifier))
                        {
                            result.Add(new InstalledApplication(name, identifier));
                        }
                    }
                    catch (COMException)
                    {
                    }
                    catch (TargetInvocationException)
                    {
                    }
                    finally
                    {
                        ReleaseComObject(item);
                    }
                }
            }
            finally
            {
                ReleaseComObject(items);
                ReleaseComObject(folder);
                ReleaseComObject(shell);
            }

            return result;
        }

        private static object Invoke(
            object target,
            string member,
            BindingFlags flags,
            object[] arguments)
        {
            if (target == null)
            {
                return null;
            }

            return target.GetType().InvokeMember(
                member,
                flags,
                null,
                target,
                arguments);
        }

        private static void ReleaseComObject(object value)
        {
            if (value != null && Marshal.IsComObject(value))
            {
                Marshal.FinalReleaseComObject(value);
            }
        }
    }

    internal static class InstalledApplicationLauncher
    {
        public static string TryLaunch(string identifier)
        {
            if (string.IsNullOrWhiteSpace(identifier)
                || identifier.Length > 2048
                || identifier.IndexOf('\0') >= 0)
            {
                return "La aplicación seleccionada ya no está disponible.";
            }

            try
            {
                ProcessStartInfo info = new ProcessStartInfo();
                info.FileName = "shell:AppsFolder\\" + identifier;
                info.UseShellExecute = true;
                Process.Start(info);
                return null;
            }
            catch (Exception)
            {
                return "Windows no pudo abrir la aplicación seleccionada.";
            }
        }
    }
}
