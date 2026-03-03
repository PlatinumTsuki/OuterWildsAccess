using UnityEngine;

namespace OuterWildsAccess
{
    /// <summary>
    /// Shared teleportation utilities used by ShipRecallHandler, AutopilotHandler,
    /// and player teleportation. Wraps WarpToPositionRotation + velocity matching.
    /// </summary>
    public static class WarpHelper
    {
        /// <summary>
        /// Warps a rigidbody to a new position/rotation and matches velocity
        /// to a reference body (e.g. planet surface velocity at the target point).
        /// If referenceBody is null, velocity is set to fallbackVelocity.
        /// </summary>
        public static void WarpAndMatchVelocity(
            OWRigidbody body,
            Vector3 targetPos,
            Quaternion targetRot,
            OWRigidbody referenceBody,
            Vector3 fallbackVelocity)
        {
            body.WarpToPositionRotation(targetPos, targetRot);

            if (referenceBody != null)
                body.SetVelocity(referenceBody.GetPointVelocity(targetPos));
            else
                body.SetVelocity(fallbackVelocity);
        }

        /// <summary>
        /// Warps a rigidbody to a new position/rotation and sets an explicit velocity.
        /// Simpler overload for cases where the desired velocity is already known
        /// (e.g. matching the player's velocity when recalling the ship).
        /// </summary>
        public static void WarpAndSetVelocity(
            OWRigidbody body,
            Vector3 targetPos,
            Quaternion targetRot,
            Vector3 velocity)
        {
            body.WarpToPositionRotation(targetPos, targetRot);
            body.SetVelocity(velocity);
        }
    }
}
