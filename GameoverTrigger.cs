using UnityEngine;

public class GameoverTrigger : MonoBehaviour
{
    public GameObject player;
    public Vector3 playerFirstPosition;

    public void Start()
    {
        player = GameObject.FindGameObjectWithTag("Player");//"Player"のタグがついたオブジェクトを見つけて代入
        playerFirstPosition = player.transform.position;//操作キャラの初期位置


    }
    private void OnTriggerEnter(Collider other)
    {
        //string ColidedObject = other.name;
        //Debug.Log($"GameOver衝突:{ColidedObject}");

        //プレイヤーは初期位置に移動される
        if (other.name == player.name)
        {
            player.transform.position = playerFirstPosition;
        }
    }

}


