using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using Fusion;

public enum WallMode { Static, Rising, Falling };

public class MazeWallController : NetworkBehaviour
{
    public WallMode modeX = WallMode.Static;
    public WallMode modeZ = WallMode.Static;

    [Networked] public int playersNearX { get; set; } = 0;
    [Networked] public int playersNearZ { get; set; } = 0;
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
        if (!this.Object.HasStateAuthority)
            return;

        float deltaTime = (float)this.Runner.DeltaTime;

        if (playersNearX > 0 && modeX == WallMode.Rising)
        {
            // rise wall by 4 with moveSpeed
            Vector3 posX = longX.transform.position;
            posX.y += moveSpeed * deltaTime;
            posX.y = Mathf.Clamp(posX.y, -2, 2);
            longX.transform.position = posX;
        }
        if (playersNearZ > 0 && modeZ == WallMode.Rising)
        {
            // rise wall by 4 with moveSpeed
            Vector3 posZ = longZ.transform.position;
            posZ.y += moveSpeed * deltaTime;
            posZ.y = Mathf.Clamp(posZ.y, -2, 2);
            longZ.transform.position = posZ;
        }
        if (playersNearX == 0 && modeX == WallMode.Rising)
        {
            // fall wall by 4 with moveSpeed
            Vector3 posX = longX.transform.position;
            posX.y -= moveSpeed * deltaTime;
            posX.y = Mathf.Clamp(posX.y, -2, 2);
            longX.transform.position = posX;
        }
        if (playersNearZ == 0 && modeZ == WallMode.Rising)
        {
            // fall wall by 4 with moveSpeed
            Vector3 posZ = longZ.transform.position;
            posZ.y -= moveSpeed * deltaTime;
            posZ.y = Mathf.Clamp(posZ.y, -2, 2);
            longZ.transform.position = posZ;
        }

        if (playersNearX > 0 && modeX == WallMode.Falling)
        {
            // fall wall by 4 with moveSpeed
            Vector3 posX = longX.transform.position;
            posX.y -= moveSpeed * deltaTime;
            posX.y = Mathf.Clamp(posX.y, -2, 2);
            longX.transform.position = posX;
        }
        if (playersNearZ > 0 && modeZ == WallMode.Falling)
        {
            // fall wall by 4 with moveSpeed
            Vector3 posZ = longZ.transform.position;
            posZ.y -= moveSpeed * deltaTime;
            posZ.y = Mathf.Clamp(posZ.y, -2, 2);
            longZ.transform.position = posZ;
        }
        if (playersNearX == 0 && modeX == WallMode.Falling)
        {
            // rise wall by 4 with moveSpeed
            Vector3 posX = longX.transform.position;
            posX.y += moveSpeed * deltaTime;
            posX.y = Mathf.Clamp(posX.y, -2, 2);
            longX.transform.position = posX;
        }
        if (playersNearZ == 0 && modeZ == WallMode.Falling)
        {
            // rise wall by 4 with moveSpeed
            Vector3 posZ = longZ.transform.position;
            posZ.y += moveSpeed * deltaTime;
            posZ.y = Mathf.Clamp(posZ.y, -2, 2);
            longZ.transform.position = posZ;
        }
    }
}