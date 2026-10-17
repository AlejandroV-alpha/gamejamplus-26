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
  if(editorPreview!=null)Destroy(editorPreview);
  if(enemyPrefab==null)yield break;
  while(NetworkManager.Singleton==null)yield return null;
  var manager=NetworkManager.Singleton;
  if(!manager.NetworkConfig.Prefabs.Contains(enemyPrefab))manager.AddNetworkPrefab(enemyPrefab);
  while(!manager.IsListening)yield return null;
  if(!manager.IsServer)yield break;
  spawnedEnemy=Instantiate(enemyPrefab,transform.position,transform.rotation);
  spawnedEnemy.GetComponent<NetworkObject>().Spawn(true);
 }
 void OnDrawGizmosSelected(){Gizmos.color=new Color(1,.45f,.05f,.8f);Gizmos.DrawWireSphere(transform.position,3);}
}
