using UnityEngine;

public class PlayerPosition : MonoBehaviour
{
    public bool IsOnSideWalk = false;

    private void Update()
    {
        RaycastHit hit;
       //Physics.Raycast（どこから、どの方向に飛ばす）真下にraycastを飛ばす
        if (Physics.Raycast(transform.position, Vector3.down, out hit, 1.0f))
        {
            if (hit.collider.gameObject.layer == LayerMask.NameToLayer("SideWalk"))
            {
                IsOnSideWalk = true;
            }
            else
            {
                IsOnSideWalk= false;
            }
        }
    }
}
