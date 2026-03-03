using UnityEngine;

namespace OuterWildsAccess
{
    /// <summary>
    /// Teleports the player's ship directly above their current position.
    /// Essential for blind players who may lose track of their ship.
    ///
    /// Uses OWRigidbody.WarpToPositionRotation() for safe teleportation
    /// (triggers game events) and matches velocity to avoid orbital drift.
    ///
    /// Guard: ModSettings.ShipRecallEnabled
    /// </summary>
    public class ShipRecallHandler
    {
        /// <summary>Offset above the player (in meters) where the ship is placed.</summary>
        private const float RecallOffset = 10f;

        /// <summary>
        /// Initializes the handler. Call from Main.Start().
        /// </summary>
        public void Initialize()
        {
            DebugLogger.Log(LogCategory.State, "ShipRecallHandler", "Initialized");
        }

        /// <summary>
        /// Teleports the ship above the player. Call from Main.ProcessHotkeys() on F3.
        /// </summary>
        public void RecallShip()
        {
            if (!ModSettings.ShipRecallEnabled) return;

            // Guard: already inside the ship
            if (PlayerState.IsInsideShip())
            {
                ScreenReader.Say(Loc.Get("recall_inside"));
                return;
            }

            OWRigidbody playerBody = Locator.GetPlayerBody();
            OWRigidbody shipBody   = Locator.GetShipBody();

            if (playerBody == null || shipBody == null)
            {
                ScreenReader.Say(Loc.Get("recall_unavailable"));
                DebugLogger.Log(LogCategory.State, "ShipRecallHandler",
                    "Recall failed: playerBody or shipBody is null");
                return;
            }

            // Guard: ship destroyed
            var dmgCtrl = shipBody.GetComponentInChildren<ShipDamageController>();
            if (dmgCtrl != null && dmgCtrl.IsDestroyed())
            {
                ScreenReader.Say(Loc.Get("recall_destroyed"));
                return;
            }

            // Calculate target position: above the player, aligned to local gravity "up"
            Vector3    playerPos = playerBody.GetPosition();
            Vector3    playerUp  = playerBody.transform.up;
            Quaternion playerRot = playerBody.GetRotation();
            Vector3    playerVel = playerBody.GetVelocity();

            Vector3 targetPos = playerPos + playerUp * RecallOffset;

            // Warp ship (safe method — triggers OnWarpOWRigidbody event)
            WarpHelper.WarpAndSetVelocity(shipBody, targetPos, playerRot, playerVel);

            ScreenReader.Say(Loc.Get("recall_success"));
            DebugLogger.Log(LogCategory.State, "ShipRecallHandler",
                $"Ship recalled to {targetPos} (offset {RecallOffset}m above player)");
        }

        /// <summary>
        /// Cleanup. Call from Main.OnDestroy().
        /// </summary>
        public void Cleanup() { }
    }
}
