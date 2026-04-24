using UnityEngine;

public class TerrainTrigger : MonoBehaviour
{
    public TerrainGenerator terrain;

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            Debug.Log(transform.position);
            terrain.StartTerrainTransition(transform.position);
        }
    }
}