using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WheelController : MonoBehaviour
{
    [SerializeField] WheelCollider frontRightCollieder;
    [SerializeField] WheelCollider frontLeftCollider;
    [SerializeField] WheelCollider backRightCollider;
    [SerializeField] WheelCollider backLeftCollider;

    [SerializeField] Transform frontRightTransform;
    [SerializeField] Transform frontLeftTransform;
    [SerializeField] Transform backRightTransform;
    [SerializeField] Transform backLeftTransform;

    public float acceleration = 500f;
    public float steering = 30f;
    public float brakeForce = 300f;

    private float currentAcceleration = 0f;
    private float currentBrakeForce = 0f;
    private float currentSteeringAngle = 0f;


    // Start is called before the first frame update
    private void FixedUpdate()
    {
        float moveInput = Input.GetAxis("Vertical");
        float steerInput = Input.GetAxis("Horizontal");
        bool isBraking = Input.GetKey(KeyCode.Space);

        currentAcceleration = moveInput * acceleration;
        currentSteeringAngle = steerInput * steering;

        if (isBraking)
        {
            currentBrakeForce = brakeForce;
        }
        else
        {
            currentBrakeForce = 0f;
        }

        frontRightCollieder.motorTorque = currentAcceleration;
        frontLeftCollider.motorTorque = currentAcceleration;

        frontRightCollieder.steerAngle = currentSteeringAngle;
        frontLeftCollider.steerAngle = currentSteeringAngle;

        frontRightCollieder.brakeTorque = currentBrakeForce;
        frontLeftCollider.brakeTorque = currentBrakeForce;
        backRightCollider.brakeTorque = currentBrakeForce;
        backLeftCollider.brakeTorque = currentBrakeForce;

        UpdateWheel(frontRightCollieder, frontRightTransform);
        UpdateWheel(frontLeftCollider, frontLeftTransform);
        UpdateWheel(backRightCollider, backRightTransform);
        UpdateWheel(backLeftCollider, backLeftTransform);
    }

    void UpdateWheel(WheelCollider col, Transform trans)
    {
        Vector3 pos;
        Quaternion rot;
        col.GetWorldPose(out pos, out rot);

        trans.position = pos;
        trans.rotation = rot;
    }
}
