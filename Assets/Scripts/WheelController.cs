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

    [Header("Networking")]
    public bool useExternalInput = false;

    private Fusion.NetworkObject _netObj;

    private float currentAcceleration = 0f;
    private float currentBrakeForce = 0f;
    private float currentSteeringAngle = 0f;

    // inputs set externally (e.g. from Fusion)
    private float _extMoveInput;
    private float _extSteerInput;
    private bool _extBrake;

    private void Start()
    {
        _netObj = GetComponentInParent<Fusion.NetworkObject>();

        // Always lock cursor when gameplay starts (pre-networked behaviour)
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // Default to external input (network-driven). Local path only used if explicitly set false.
        //useExternalInput = true;
        _extMoveInput = 0f;
        _extSteerInput = 0f;
        _extBrake = false;
    }

    private void FixedUpdate()
    {
#region agent log
        try
        {
            var payload = "{\"sessionId\":\"debug-session\",\"runId\":\"pre-fix\",\"hypothesisId\":\"H2\",\"location\":\"WheelController:FixedUpdate\",\"message\":\"tick\",\"data\":{\"useExternal\":" + (useExternalInput ? 1 : 0) + ",\"extMove\":" + _extMoveInput + ",\"extSteer\":" + _extSteerInput + ",\"extBrake\":" + (_extBrake ? 1 : 0) + "},\"timestamp\":" + System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + "}";
            System.IO.File.AppendAllText(@"c:\Users\marti\Desktop\FER\UMRIGR\project\My project\.cursor\debug.log", payload + "\n", System.Text.Encoding.UTF8);
        }
        catch { }
#endregion
        float moveInput;
        float steerInput;
        bool isBraking;

        // Prevent local input from driving objects we neither own nor simulate
        if (_netObj != null && useExternalInput == false)
        {
            if (_netObj.HasStateAuthority == false && _netObj.HasInputAuthority == false)
                return;
        }

        if (useExternalInput)
        {
            moveInput = _extMoveInput;
            steerInput = _extSteerInput;
            isBraking = _extBrake;
        }
        else
        {
            moveInput = Input.GetAxis("Vertical");
            steerInput = Input.GetAxis("Horizontal");
            isBraking = Input.GetKey(KeyCode.Space);
        }

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

    public void SetExternalInput(float move, float steer, bool brake)
    {
        _extMoveInput = move;
        _extSteerInput = steer;
        _extBrake = brake;
        useExternalInput = true;
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
