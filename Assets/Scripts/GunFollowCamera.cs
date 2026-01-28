using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GunFollowCamera : MonoBehaviour
{

    [Header("References")]
    public Transform cameraTransform;
    public Transform playerTransform;
    public Transform gunTransform;

    public float rotationSpeed = 5f;

    // Update is called once per frame
    void Update()
    {
        Vector3 viewDir = new Vector3(cameraTransform.position.x, playerTransform.position.y, cameraTransform.position.z) - playerTransform.position;
        gunTransform.forward = Vector3.Slerp(gunTransform.forward, viewDir.normalized, rotationSpeed * Time.deltaTime);
    }
}
