using System;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Fusion;
using UnityEngine;

namespace GameJam.Multiplayer
{
    public static class MultiplayerConfiguration
    {
        // Increment when prefab layout or gameplay protocol changes without a Networked property change.
        public const string ContentProtocol = "expedition-2";
        static string protocol;
        public static string ProtocolVersion => protocol ??= ComputeProtocol();
        public static readonly string[] Regions = { "sa", "us", "usw", "eu", "asia", "jp", "au", "auto" };
        public static string SelectedRegion
        {
            get { string value = PlayerPrefs.GetString("GameJam.Region", "sa"); return Regions.Contains(value) ? value : "sa"; }
            set { if (Regions.Contains(value)) { PlayerPrefs.SetString("GameJam.Region", value); PlayerPrefs.Save(); } }
        }
        static string clientIdentity;
        static System.Threading.Mutex instanceIdentityLock;
        public static string ClientIdentity
        {
            get
            {
                if (clientIdentity != null) return clientIdentity;
                string identity = PlayerPrefs.GetString("GameJam.ClientIdentity", "");
                if (!Guid.TryParse(identity, out _)) { identity = Guid.NewGuid().ToString("N"); PlayerPrefs.SetString("GameJam.ClientIdentity", identity); PlayerPrefs.Save(); }
                // Editor/virtual players must not collide with the installed game on the same PC.
                if (Application.isEditor) return clientIdentity = identity + "-editor-" + System.Diagnostics.Process.GetCurrentProcess().Id;
                try
                {
                    instanceIdentityLock = new System.Threading.Mutex(false, "GameJamPlus26-" + identity, out bool firstInstance);
                    clientIdentity = firstInstance ? identity : identity + "-test-" + System.Diagnostics.Process.GetCurrentProcess().Id;
                    Application.quitting += () => { instanceIdentityLock?.Dispose(); instanceIdentityLock = null; };
                }
                catch (System.PlatformNotSupportedException) { clientIdentity = identity; }
                return clientIdentity;
            }
        }
        public static string FixedRegion => SelectedRegion == "auto" ? string.Empty : SelectedRegion;

        static string ComputeProtocol()
        {
            string schema = ContentProtocol + ";" + typeof(NetworkRunner).Assembly.GetName().Version + ";" +
                string.Join(";", typeof(MarsFusionAvatar).Assembly.GetTypes()
                    .Where(t => typeof(NetworkBehaviour).IsAssignableFrom(t)).OrderBy(t => t.FullName)
                    .SelectMany(t => t.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                        .Where(p => p.IsDefined(typeof(NetworkedAttribute), true)).OrderBy(p => p.Name)
                        .Select(p => t.FullName + "." + p.Name + ":" + p.PropertyType.FullName)));
            using (var hash = SHA256.Create())
                return ContentProtocol + "-" + BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(schema))).Replace("-", "").Substring(0, 16);
        }
    }
}
