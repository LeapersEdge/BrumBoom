using Fusion;
using UnityEngine;

namespace NetGame
{
    /// <summary>
    /// Network input payload for the car/turret.
    /// </summary>
    public struct CarInput : INetworkInput
    {
        public Vector2 Move;   // y = forward/back, x unused for now
        public float Steer;    // yaw of car body
        public float Turret;   // yaw input for turret (unused for now)
        public NetworkBool Fire;
        public NetworkBool Brake;
    }
}

