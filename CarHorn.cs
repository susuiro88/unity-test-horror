using UnityEngine;

public class CarHorn : MonoBehaviour
{
    //設定したコライダーが反応あると効果音を出す。
    [SerializeField] private AudioSource hornAudio;
    [SerializeField] private PlayerPosition playerPosition;
    private void OnTriggerEnter(Collider other)
    {
        Debug.Log("触れた：" + other.name);

        if (other.CompareTag("Player"))
        {
            //Debug.Log("Playerに反応！");
            //hornAudio.Play();

            
            if (!playerPosition.IsOnSideWalk)
            {
                Debug.Log("Playerに反応！");
                hornAudio.Play();
            }
            
        }
    }
}
