// =============================================================================
//  Internal · Plugin Loader
// -----------------------------------------------------------------------------
//  Scans  <GTA V>\scripts\SentiencePlugins\*.dll  on startup, reflects the
//  assemblies, instantiates every public concrete type that implements
//  ISentiencePlugin, and wires it up with a PluginContext + Logger.
//
//  Failure modes are aggressively caught:
//    - DLL fails to load                 → skip, log, continue
//    - Plugin OnLoad throws              → quarantine the plugin (Disabled)
//    - Plugin event handler throws       → quarantine on Nth fault
//
//  A quarantined plugin remains visible in the F5 menu so users see "why
//  did MyMod stop working", but its delegates are dropped.
// =============================================================================
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using GTA5MOD2026.SDK;

namespace GTA5MOD2026.Plugins
{
    public enum PluginStatus
    {
        Loaded = 0,
        Disabled = 1,
        Faulted = 2,
        IncompatibleSdk = 3
    }

    public sealed class LoadedPlugin
    {
        public string Name { get; internal set; }
        public string Author { get; internal set; }
        public string Version { get; internal set; }
        public string SourceFile { get; internal set; }
        public PluginStatus Status { get; internal set; }
        public string StatusMessage { get; internal set; }
        public int FaultCount { get; internal set; }
        internal ISentiencePlugin Instance { get; set; }
        internal Assembly Assembly { get; set; }
    }

    public sealed class PluginLoader
    {
        private const int FaultQuarantineThreshold = 3;
        private const string SdkVersion = SentienceSdk.Version;

        private readonly List<LoadedPlugin> _plugins
            = new List<LoadedPlugin>();
        private readonly Dictionary<Assembly, LoadedPlugin> _byAssembly
            = new Dictionary<Assembly, LoadedPlugin>();

        // Internal: external plugins subscribe via IPluginContext.Events.
        // Within the host assembly NPCManager / SentienceMenu can fire / read.
        internal SentienceEventHub Hub { get; }
        public string HostModVersion { get; }

        public IReadOnlyList<LoadedPlugin> Plugins => _plugins;

        public PluginLoader(string hostModVersion)
        {
            HostModVersion = hostModVersion ?? "";
            Hub = new SentienceEventHub(OnHandlerFault);
        }

        public string PluginDirectory => Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory ?? "",
            SentienceSdk.PluginFolderName);

        /// <summary>
        /// Scan the plugin folder, load every DLL, instantiate every plugin
        /// type.  Safe to call multiple times: already-loaded assemblies are
        /// skipped (.NET caches them).
        /// </summary>
        public void DiscoverAndLoad(
            IReadOnlyDictionary<string, string> configSnapshot)
        {
            string dir = PluginDirectory;
            if (!Directory.Exists(dir))
            {
                try { Directory.CreateDirectory(dir); }
                catch { /* not fatal */ }
                return;
            }

            string[] dlls;
            try { dlls = Directory.GetFiles(dir, "*.dll"); }
            catch { return; }

            foreach (string dllPath in dlls)
            {
                TryLoadAssembly(dllPath, configSnapshot);
            }
        }

        private void TryLoadAssembly(
            string dllPath,
            IReadOnlyDictionary<string, string> configSnapshot)
        {
            Assembly asm;
            try
            {
                // LoadFrom keeps cross-assembly references resolvable from
                // the same folder, which is critical so plugins can ship
                // their own Newtonsoft etc.
                asm = Assembly.LoadFrom(dllPath);
            }
            catch (Exception ex)
            {
                AddSkeleton(dllPath, PluginStatus.Faulted,
                    "Failed to load: " + ex.Message);
                return;
            }

            Type[] types;
            try { types = asm.GetTypes(); }
            catch (ReflectionTypeLoadException rex)
            {
                types = rex.Types?.Where(t => t != null).ToArray()
                    ?? Type.EmptyTypes;
            }
            catch (Exception ex)
            {
                AddSkeleton(dllPath, PluginStatus.Faulted,
                    "Type scan failed: " + ex.Message);
                return;
            }

            foreach (var t in types)
            {
                if (t == null) continue;
                if (t.IsAbstract || t.IsInterface) continue;
                if (!typeof(ISentiencePlugin).IsAssignableFrom(t)) continue;
                if (t.GetConstructor(Type.EmptyTypes) == null) continue;

                TryInstantiate(t, asm, dllPath, configSnapshot);
            }
        }

        private void TryInstantiate(
            Type pluginType, Assembly asm, string dllPath,
            IReadOnlyDictionary<string, string> configSnapshot)
        {
            ISentiencePlugin instance;
            try { instance = (ISentiencePlugin)Activator.CreateInstance(pluginType); }
            catch (Exception ex)
            {
                AddSkeleton(dllPath, PluginStatus.Faulted,
                    $"{pluginType.FullName} ctor threw: {ex.Message}");
                return;
            }

            string name    = instance.Name    ?? pluginType.Name;
            string author  = instance.Author  ?? "(unknown)";
            string version = instance.Version ?? "0.0.0";

            // SDK compatibility check (string comparison is fine for SemVer
            // when we constrain ourselves to "X.Y.Z" without prerelease tags).
            if (!IsSdkCompatible(instance.MinSdkVersion))
            {
                _plugins.Add(new LoadedPlugin
                {
                    Name = name,
                    Author = author,
                    Version = version,
                    SourceFile = dllPath,
                    Status = PluginStatus.IncompatibleSdk,
                    StatusMessage =
                        $"Requires SDK >= {instance.MinSdkVersion}, " +
                        $"host is {SdkVersion}",
                    Instance = instance,
                    Assembly = asm
                });
                return;
            }

            var record = new LoadedPlugin
            {
                Name = name,
                Author = author,
                Version = version,
                SourceFile = dllPath,
                Status = PluginStatus.Loaded,
                StatusMessage = "",
                Instance = instance,
                Assembly = asm
            };
            _plugins.Add(record);
            _byAssembly[asm] = record;

            string pluginDataDir = Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.MyDocuments),
                "GTA5MOD2026", "PluginData",
                SanitizeFolderName(name));

            var ctx = new PluginContext(
                HostModVersion,
                Hub,
                new PluginLogger(name),
                configSnapshot,
                pluginDataDir);

            try
            {
                instance.OnLoad(ctx);
                ctx.Logger.Info(
                    $"Loaded v{version} by {author} from {Path.GetFileName(dllPath)}");
            }
            catch (Exception ex)
            {
                record.Status = PluginStatus.Faulted;
                record.StatusMessage = "OnLoad threw: " + ex.Message;
                Hub.DropHandlersForAssembly(asm);
                ctx.Logger.Error(record.StatusMessage);
            }
        }

        public void UnloadAll()
        {
            foreach (var p in _plugins)
            {
                if (p.Instance == null) continue;
                try { p.Instance.OnUnload(); }
                catch { /* never let unload throw */ }
                if (p.Assembly != null)
                    Hub.DropHandlersForAssembly(p.Assembly);
            }
            _plugins.Clear();
            _byAssembly.Clear();
        }

        public bool DisablePlugin(string nameOrFile)
        {
            var p = FindByNameOrFile(nameOrFile);
            if (p == null || p.Status == PluginStatus.Disabled) return false;
            try { p.Instance?.OnUnload(); } catch { }
            Hub.DropHandlersForAssembly(p.Assembly);
            p.Status = PluginStatus.Disabled;
            p.StatusMessage = "Disabled by user";
            return true;
        }

        public bool EnablePlugin(string nameOrFile,
            IReadOnlyDictionary<string, string> configSnapshot)
        {
            var p = FindByNameOrFile(nameOrFile);
            if (p == null) return false;
            if (p.Status == PluginStatus.Loaded) return true;
            if (p.Status == PluginStatus.IncompatibleSdk) return false;
            if (p.Instance == null) return false;

            string pluginDataDir = Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.MyDocuments),
                "GTA5MOD2026", "PluginData",
                SanitizeFolderName(p.Name));
            var ctx = new PluginContext(HostModVersion, Hub,
                new PluginLogger(p.Name), configSnapshot, pluginDataDir);
            try
            {
                p.Instance.OnLoad(ctx);
                p.Status = PluginStatus.Loaded;
                p.StatusMessage = "";
                p.FaultCount = 0;
                return true;
            }
            catch (Exception ex)
            {
                p.Status = PluginStatus.Faulted;
                p.StatusMessage = "OnLoad threw: " + ex.Message;
                Hub.DropHandlersForAssembly(p.Assembly);
                return false;
            }
        }

        private LoadedPlugin FindByNameOrFile(string nameOrFile)
        {
            if (string.IsNullOrWhiteSpace(nameOrFile)) return null;
            return _plugins.FirstOrDefault(p =>
                string.Equals(p.Name, nameOrFile,
                    StringComparison.OrdinalIgnoreCase)
                || string.Equals(
                    Path.GetFileName(p.SourceFile ?? ""), nameOrFile,
                    StringComparison.OrdinalIgnoreCase));
        }

        private void OnHandlerFault(Delegate handler, Exception ex)
        {
            var asm = handler?.Method?.DeclaringType?.Assembly;
            if (asm == null) return;
            if (!_byAssembly.TryGetValue(asm, out var record)) return;
            record.FaultCount++;
            if (record.FaultCount >= FaultQuarantineThreshold)
            {
                record.Status = PluginStatus.Faulted;
                record.StatusMessage =
                    $"Quarantined after {record.FaultCount} faults. " +
                    $"Last: {ex.Message}";
                Hub.DropHandlersForAssembly(asm);
            }
        }

        private void AddSkeleton(string dllPath,
            PluginStatus status, string msg)
        {
            _plugins.Add(new LoadedPlugin
            {
                Name = Path.GetFileNameWithoutExtension(dllPath),
                Author = "(unknown)",
                Version = "?",
                SourceFile = dllPath,
                Status = status,
                StatusMessage = msg
            });
        }

        private static bool IsSdkCompatible(string requiredMin)
        {
            if (string.IsNullOrWhiteSpace(requiredMin)) return true;
            return CompareSemVer(requiredMin, SdkVersion) <= 0;
        }

        /// <summary>
        /// Lightweight SemVer comparison without prerelease tag support.
        /// Returns &lt;0 if a&lt;b, 0 if equal, &gt;0 if a&gt;b.
        /// </summary>
        private static int CompareSemVer(string a, string b)
        {
            if (string.Equals(a, b, StringComparison.Ordinal)) return 0;
            int[] pa = ParseSemVer(a);
            int[] pb = ParseSemVer(b);
            for (int i = 0; i < 3; i++)
            {
                if (pa[i] != pb[i]) return pa[i].CompareTo(pb[i]);
            }
            return 0;
        }

        private static int[] ParseSemVer(string s)
        {
            int[] r = new int[3];
            if (string.IsNullOrWhiteSpace(s)) return r;
            string[] parts = s.Split(new[] { '.' }, 4);
            for (int i = 0; i < 3 && i < parts.Length; i++)
                int.TryParse(parts[i], out r[i]);
            return r;
        }

        private static string SanitizeFolderName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "_";
            var chars = name.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                if (Path.GetInvalidFileNameChars().Contains(chars[i]))
                    chars[i] = '_';
            }
            return new string(chars);
        }
    }
}
