using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public enum WallMode { Static, Rising, Falling };

public class MazeWallController : NetworkBehaviour
{
    public WallMode modeX = WallMode.Static;
    public WallMode modeZ = WallMode.Static;

    [Networked] public int playersNearX = 0;
    public int playersNearZ = 0;
    [SerializeField] GameObject longX;
    [SerializeField] GameObject longZ;
    public float moveSpeed = 10;

    void Start()
    {
        if (modeX == WallMode.Rising)
        {
            Vector3 posX = longX.transform.position;
            posX.y = -2f;
            longX.transform.position = posX;
        }

        if (modeZ == WallMode.Rising)
        {
            Vector3 posZ = longZ.transform.position;
            posZ.y = -2f;
            longZ.transform.position = posZ;
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (!Object.HasStateAuthority)
            return;

        float deltaTime = (float)Runner.DeltaTime;

        if (playersNearX > 0 && modeX == WallMode.Rising)
        {
            // rise wall by 4 with moveSpeed
            Vector3 posX = longX.transform.position;
            posX.y += moveSpeed * Time.deltaTime;
            posX.y = Mathf.Clamp(posX.y, -2, 2);
            longX.transform.position = posX;
        }
        if (playersNearZ > 0 && modeZ == WallMode.Rising)
        {
            // rise wall by 4 with moveSpeed
            Vector3 posZ = longZ.transform.position;
            posZ.y += moveSpeed * Time.deltaTime;
            posZ.y = Mathf.Clamp(posZ.y, -2, 2);
            longZ.transform.position = posZ;
        }
        if (playersNearX == 0 && modeX == WallMode.Rising)
        {
            // fall wall by 4 with moveSpeed
            Vector3 posX = longX.transform.position;
            posX.y -= moveSpeed * Time.deltaTime;
            posX.y = Mathf.Clamp(posX.y, -2, 2);
            longX.transform.position = posX;
        }
        if (playersNearZ == 0 && modeZ == WallMode.Rising)
        {
            // fall wall by 4 with moveSpeed
            Vector3 posZ = longZ.transform.position;
            posZ.y -= moveSpeed * Time.deltaTime;
            posZ.y = Mathf.Clamp(posZ.y, -2, 2);
            longZ.transform.position = posZ;
        }

        if (playersNearX > 0 && modeX == WallMode.Falling)
        {
            // fall wall by 4 with moveSpeed
            Vector3 posX = longX.transform.position;
            posX.y -= moveSpeed * Time.deltaTime;
            posX.y = Mathf.Clamp(posX.y, -2, 2);
            longX.transform.position = posX;
        }
        if (playersNearZ > 0 && modeZ == WallMode.Falling)
        {
            // fall wall by 4 with moveSpeed
            Vector3 posZ = longZ.transform.position;
            posZ.y -= moveSpeed * Time.deltaTime;
            posZ.y = Mathf.Clamp(posZ.y, -2, 2);
            longZ.transform.position = posZ;
        }
        if (playersNearX == 0 && modeX == WallMode.Falling)
        {
            // rise wall by 4 with moveSpeed
            Vector3 posX = longX.transform.position;
            posX.y += moveSpeed * Time.deltaTime;
            posX.y = Mathf.Clamp(posX.y, -2, 2);
            longX.transform.position = posX;
        }
        if (playersNearZ == 0 && modeZ == WallMode.Falling)
        {
            // rise wall by 4 with moveSpeed
            Vector3 posZ = longZ.transform.position;
            posZ.y += moveSpeed * Time.deltaTime;
            posZ.y = Mathf.Clamp(posZ.y, -2, 2);
            longZ.transform.position = posZ;
        }
    }
}