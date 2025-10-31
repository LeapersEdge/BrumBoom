using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class GunControler : MonoBehaviour
{
    [Header("References")]
    [SerializeField] Camera camera;
    [SerializeField] GameObject gun;

    [Header("Properties")]
    [SerializeField] float rotationSpeed = 100.0f;

    Vector3 camRotation;
    float yaw = 0.0f;

    Vector3 baseCamRotation = Vector3.zero;

    void Start()
    {
        baseCamRotation = camera.gameObject.transform.localRotation.eulerAngles;
        camRotation = baseCamRotation;
    }

    void Update()
    {
        float horizontal = Input.GetAxis("HorizontalCam");
        float vertical = Input.GetAxis("VerticalCam");
        
        yaw += horizontal * rotationSpeed * Time.deltaTime;
        camRotation.x -= vertical * rotationSpeed * Time.deltaTime;
        camRotation.x = Mathf.Clamp(camRotation.x, -80.0f, 80.0f);

        camera.gameObject.transform.localRotation = Quaternion.Euler(camRotation);
        gun.transform.localRotation = Quaternion.Euler(0.0f, yaw, 0.0f);
    }
}
