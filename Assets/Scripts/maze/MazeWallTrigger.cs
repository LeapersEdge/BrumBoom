using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class MazeWallTrigger : MonoBehaviour
{
    [SerializeField] bool isLongX = false;
    [SerializeField] bool isLongZ = false;

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            MazeWallController controller = transform.parent.parent.GetComponent<MazeWallController>();
            if (controller != null)
            {
                if (isLongX)
                    controller.playersNearX++;
                if (isLongZ)
                    controller.playersNearZ++;
            }
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            MazeWallController controller = transform.parent.parent.GetComponent<MazeWallController>();
            if (controller != null)
            {
                if (isLongX)
                    controller.playersNearX--;
                if (isLongZ)
                    controller.playersNearZ--;

                if (controller.playersNearX < 0)
                    controller.playersNearX = 0;
                if (controller.playersNearZ < 0)
                    controller.playersNearZ = 0;
            }
        }
    }
}
