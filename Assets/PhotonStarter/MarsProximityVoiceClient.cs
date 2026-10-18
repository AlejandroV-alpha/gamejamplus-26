using Photon.Voice.Fusion;
using UnityEngine;

namespace GameJam.Multiplayer
{
    // The SDK follows Fusion room/region changes. Credentials exist only in memory.
    public sealed class MarsProximityVoiceClient : FusionVoiceClient
    {
        protected override void Awake()
        {
            UseFusionAppSettings = false;
            Settings = new Photon.Realtime.AppSettings
            {
                AppIdVoice = PhotonLocalSettings.ReadVoiceAppId(),
                AppVersion = "MarsCoop-v1",
                FixedRegion = string.Empty
            };
            AutoConnectAndJoin = !string.IsNullOrEmpty(Settings.AppIdVoice);
            Debug.Log("[Proximity voice] " + PhotonLocalSettings.LastVoiceDiagnostic, this);
            base.Awake();
        }
    }
}
