using Fusion;
using UnityEngine;

namespace NetGame
{
    /// <summary>
    /// Network input payload for the car.
    /// </summary>
    public struct CarInput : INetworkInput
    {
        public Vector2 Move;   // y = forward/back, x unused for now
        public float Steer;    // yaw of car body
        public Vector2 TurretDir; // normalized XZ direction from camera
        public NetworkBool Fire;
        public NetworkBool Brake;
    }
}

