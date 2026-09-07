using UnityEngine;

public class WalkSideSensor : MonoBehaviour
{
    [SerializeField] private PlayerPosition isOnSideWalk;

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            isOnSideWalk.IsOnSideWalk = true;
            Debug.Log("ç°ï‡ìπÇ…Ç¢ÇÈ");

        }
        
    }
    private void OnTriggerExit(Collider other) 
    {
        if (other.CompareTag("Player"))
        {
            isOnSideWalk.IsOnSideWalk = false;
            Debug.Log("ç°ï‡ìπÇèoÇΩ");
        }
    }

}
