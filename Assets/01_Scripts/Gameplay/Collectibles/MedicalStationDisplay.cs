using TMPro;
using UnityEngine;

namespace GameJam.Collectibles
{
    [ExecuteAlways]
    [RequireComponent(typeof(HealthPickup))]
    public sealed class MedicalStationDisplay : MonoBehaviour
    {
        [SerializeField] private TMP_Text counter;
        [SerializeField] private string label = "CURACIONES";
        [SerializeField] private bool faceCamera = true;
        private HealthPickup station;

        private void LateUpdate()
        {
            if (station == null) station = GetComponent<HealthPickup>();
            if (counter == null || station == null) return;
            counter.text = label + "\n" + station.RemainingSlots + " / " + station.TotalSlots;
            counter.color = station.RemainingSlots > 0 ? new Color(.7f, 1f, .8f) : new Color(1f, .5f, .35f);
            if (Application.isPlaying && faceCamera && Camera.main != null)
                counter.transform.rotation = Quaternion.LookRotation(counter.transform.position - Camera.main.transform.position);
        }
    }
}
