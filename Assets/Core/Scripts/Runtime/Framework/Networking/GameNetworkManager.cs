using UnityEngine;
using Unity.Netcode;
using System.Net;
using System.Net.Sockets;

namespace Blocks.Gameplay.Core
{
    /// <summary>
    /// Manages network connections for the game, extending Unity's <see cref="NetworkManager"/>.
    /// Provides singleton access, connection state management, player name registry, and performance settings.
    /// Supports Host, Client, and Server modes with callbacks for connection events.
    /// </summary>
    public class GameNetworkManager : NetworkManager
    {
        #region Enums

        /// <summary>
        /// Defines the possible connection states for the network manager.
        /// </summary>
        public enum ConnectionStates
        {
            /// <summary>
            /// Not connected to any network session.
            /// </summary>
            None,

            /// <summary>
            /// Currently attempting to establish a connection.
            /// </summary>
            Connecting,

            /// <summary>
            /// Successfully connected to a network session.
            /// </summary>
            Connected,

            /// <summary>
            /// Connection attempt failed or connection was lost.
            /// </summary>
            Failed
        }

        #endregion

        #region Fields & Properties

        /// <summary>
        /// Gets the singleton instance of the GameNetworkManager.
        /// </summary>
        public static GameNetworkManager Instance => Singleton as GameNetworkManager;

        [Header("Performance Settings")]
        [Tooltip("Target frame rate for the application.")]
        public int targetFrameRate = 60;

        [Tooltip("Enable VSync to synchronize rendering with the display's refresh rate.")]
        public bool enableVSync = false;

        [Header("Player Settings")]
        [Tooltip("The player's display name.")]
        public string PlayerName { get; set; } = "Player";

        /// <summary>
        /// Gets the network state view model used for UI updates.
        /// </summary>
        private NetworkStateViewModel m_NetworkState;
        public NetworkStateViewModel NetworkState => m_NetworkState ??= new NetworkStateViewModel();

        private readonly System.Collections.Generic.Dictionary<ulong, string> m_PlayerNames = new System.Collections.Generic.Dictionary<ulong, string>();

        #endregion

        #region Unity Methods

        // Leave Awake/OnDestroy to NetworkManager. Hiding its private Unity messages
        // skips transport cleanup and can leave the UDP socket bound after Play stops.
        private void Start()
        {
            if (Singleton != this)
            {
                Debug.LogWarning("Duplicate GameNetworkManager instance detected. Destroying self.");
                Destroy(gameObject);
                return;
            }
            SetFrameRate(targetFrameRate, enableVSync);
            OnClientConnectedCallback += HandleClientConnected;
            OnClientDisconnectCallback += HandleClientDisconnect;
            OnServerStarted += HandleServerStarted;
            Application.quitting += HandleApplicationQuitting;
        }

        private void OnDisable()
        {
            if (IsServer || IsClient || IsHost)
            {
                Shutdown();
            }
        }

        private void HandleApplicationQuitting()
        {
            if (IsServer || IsClient || IsHost)
            {
                Shutdown();
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Starts a network session as a host (both server and client).
        /// Only starts if the current connection state is None or Failed.
        /// </summary>
        public void StartHostConnection()
        {
            if (NetworkState.ConnectionState != ConnectionStates.None &&
                NetworkState.ConnectionState != ConnectionStates.Failed)
            {
                return;
            }

            // Ensure transport port is free before attempting bind
            EnsureAvailableHostPort();

            NetworkState.ConnectionState = ConnectionStates.Connecting;
            UpdateViewModel();

            if (!StartHost())
            {
                NetworkState.ConnectionState = ConnectionStates.Failed;
                UpdateViewModel();
            }
        }

        /// <summary>
        /// Checks if the configured transport port is already in use by another process
        /// or an orphaned socket. If in use, automatically selects the next available UDP port.
        /// </summary>
        public void EnsureAvailableHostPort()
        {
            if (NetworkConfig?.NetworkTransport is Unity.Netcode.Transports.UTP.UnityTransport utp)
            {
                ushort initialPort = utp.ConnectionData.Port;
                string listenAddress = string.IsNullOrEmpty(utp.ConnectionData.ServerListenAddress) ? "127.0.0.1" : utp.ConnectionData.ServerListenAddress;

                if (!IsUdpPortAvailable(initialPort, listenAddress))
                {
                    ushort availablePort = GetNextAvailableUdpPort(initialPort, listenAddress);
                    Debug.LogWarning($"[GameNetworkManager] UDP Port {initialPort} is already in use. Auto-switching host to port {availablePort}.");
                    utp.SetConnectionData(utp.ConnectionData.Address, availablePort, utp.ConnectionData.ServerListenAddress);
                }
            }
        }

        private static ushort GetNextAvailableUdpPort(ushort startingPort, string listenAddress, int maxScan = 100)
        {
            for (ushort port = startingPort; port < startingPort + maxScan; port++)
            {
                if (IsUdpPortAvailable(port, listenAddress))
                {
                    return port;
                }
            }
            return startingPort;
        }

        private static bool IsUdpPortAvailable(ushort port, string listenAddress)
        {
            try
            {
                IPAddress ip = IPAddress.Any;
                if (!string.IsNullOrEmpty(listenAddress) && IPAddress.TryParse(listenAddress, out var parsed))
                {
                    ip = parsed;
                }

                using (var client = new UdpClient(new IPEndPoint(ip, port)))
                {
                    return true;
                }
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Starts a network session as a client.
        /// Only starts if the current connection state is None or Failed.
        /// </summary>
        public void StartClientConnection()
        {
            if (NetworkState.ConnectionState != ConnectionStates.None &&
                NetworkState.ConnectionState != ConnectionStates.Failed)
            {
                return;
            }

            NetworkState.ConnectionState = ConnectionStates.Connecting;
            UpdateViewModel();

            if (!StartClient())
            {
                NetworkState.ConnectionState = ConnectionStates.Failed;
                UpdateViewModel();
            }
        }

        /// <summary>
        /// Disconnects from the current network session.
        /// Only disconnects if currently connected or connecting.
        /// </summary>
        public void Disconnect()
        {
            if (NetworkState.ConnectionState == ConnectionStates.None ||
                NetworkState.ConnectionState == ConnectionStates.Failed)
            {
                return;
            }

            Shutdown();
            NetworkState.ConnectionState = ConnectionStates.None;
            UpdateViewModel();
        }

        /// <summary>
        /// Registers a player name for the specified client ID.
        /// If the client ID already exists, the name will be updated.
        /// </summary>
        /// <param name="clientId">The unique client ID.</param>
        /// <param name="playerName">The player's display name.</param>
        public void RegisterPlayerName(ulong clientId, string playerName)
        {
            if (m_PlayerNames.ContainsKey(clientId))
            {
                m_PlayerNames[clientId] = playerName;
            }
            else
            {
                m_PlayerNames.Add(clientId, playerName);
            }
        }

        /// <summary>
        /// Removes a player name from the registry for the specified client ID.
        /// </summary>
        /// <param name="clientId">The unique client ID.</param>
        public void UnregisterPlayerName(ulong clientId)
        {
            m_PlayerNames.Remove(clientId);
        }

        /// <summary>
        /// Attempts to retrieve the player name for the specified client ID.
        /// </summary>
        /// <param name="clientId">The unique client ID.</param>
        /// <param name="playerName">The retrieved player name, if found.</param>
        /// <returns>True if the player name was found; otherwise, false.</returns>
        public bool TryGetPlayerName(ulong clientId, out string playerName)
        {
            return m_PlayerNames.TryGetValue(clientId, out playerName);
        }

        /// <summary>
        /// Gets the player name for the specified client ID.
        /// If no name is registered, returns a default name in the format "Player-{clientId}".
        /// </summary>
        /// <param name="clientId">The unique client ID.</param>
        /// <returns>The player's display name or a default name.</returns>
        public string GetPlayerName(ulong clientId)
        {
            return m_PlayerNames.TryGetValue(clientId, out string name) ? name : $"Player-{clientId}";
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Sets the application frame rate and VSync settings.
        /// </summary>
        /// <param name="targetRate">The target frame rate.</param>
        /// <param name="vsync">Whether to enable VSync.</param>
        private void SetFrameRate(int targetRate, bool vsync)
        {
            Application.targetFrameRate = targetRate;
            QualitySettings.vSyncCount = vsync ? 1 : 0;
        }

        /// <summary>
        /// Handles the server started callback.
        /// Updates connection state to Connected if running as a dedicated server.
        /// </summary>
        private void HandleServerStarted()
        {
            // Only set connected state for dedicated server mode
            if (IsServer && !IsHost && !IsClient)
            {
                NetworkState.ConnectionState = ConnectionStates.Connected;
            }
            UpdateViewModel();
        }

        /// <summary>
        /// Handles the client connected callback.
        /// Updates connection state to Connected when the local client connects.
        /// </summary>
        /// <param name="clientId">The ID of the connected client.</param>
        private void HandleClientConnected(ulong clientId)
        {
            if (clientId == LocalClientId)
            {
                NetworkState.ConnectionState = ConnectionStates.Connected;
            }
            UpdateViewModel();
        }

        /// <summary>
        /// Handles the client disconnect callback.
        /// Updates connection state and cleans up player name registry.
        /// </summary>
        /// <param name="clientId">The ID of the disconnected client.</param>
        private void HandleClientDisconnect(ulong clientId)
        {
            if (clientId == LocalClientId)
            {
                if (NetworkState.ConnectionState != ConnectionStates.None)
                {
                    NetworkState.ConnectionState = ConnectionStates.None;
                    if (!string.IsNullOrEmpty(DisconnectReason))
                    {
                        Debug.Log($"Disconnect Reason: {DisconnectReason}");
                    }
                }
            }

            UnregisterPlayerName(clientId);
            UpdateViewModel();
        }

        /// <summary>
        /// Updates the network state view model with current connection information.
        /// Determines the netcode status based on the current role (Host, Server, or Client).
        /// </summary>
        private void UpdateViewModel()
        {
            if (NetworkState == null) return;

            string status = "Offline";
            if (IsHost) status = "Host";
            else if (IsServer) status = "Server";
            else if (IsClient) status = "Client";

            NetworkState.UpdateState(
                connectionState: NetworkState.ConnectionState,
                topology: NetworkConfig.NetworkTopology,
                netcodeStatus: status,
                isConnectedOrHost: IsConnectedClient || IsHost
            );
        }

        #endregion
    }
}
