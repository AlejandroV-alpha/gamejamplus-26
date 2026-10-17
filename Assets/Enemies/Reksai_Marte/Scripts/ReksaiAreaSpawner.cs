using System.Collections;
using UnityEngine;
using Unity.Netcode;

public sealed class ReksaiAreaSpawner : MonoBehaviour
{
    [SerializeField] GameObject enemyPrefab;
    [SerializeField] GameObject editorPreview;
    GameObject spawnedEnemy;

    IEnumerator Start()
    {
        if (editorPreview != null) Destroy(editorPreview);
        if (enemyPrefab == null) yield break;

        // If Unity Netcode is active and running in this scene
        if (NetworkManager.Singleton != null)
        {
            var manager = NetworkManager.Singleton;
            if (!manager.NetworkConfig.Prefabs.Contains(enemyPrefab))
                manager.AddNetworkPrefab(enemyPrefab);

            float wait = 0f;
            while (!manager.IsListening && wait < 2f)
            {
                wait += Time.deltaTime;
                yield return null;
            }

            if (manager.IsListening && manager.IsServer)
            {
                spawnedEnemy = Instantiate(enemyPrefab, transform.position, transform.rotation);
                spawnedEnemy.GetComponent<NetworkObject>()?.Spawn(true);
                yield break;
            }
        }

        // Standalone / Photon Fusion fallback (e.g. j_MarsFusion scene):
        // Spawn immediately in the scene
        yield return null;
        if (spawnedEnemy == null)
        {
            spawnedEnemy = Instantiate(enemyPrefab, transform.position, transform.rotation);
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1, .45f, .05f, .8f);
        Gizmos.DrawWireSphere(transform.position, 3);
    }
}

