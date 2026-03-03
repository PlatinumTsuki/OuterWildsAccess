using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace OuterWildsAccess
{
    /// <summary>
    /// Passively announces NPCs and interactable objects as the player walks near them.
    /// Scans every ScanInterval seconds. Announces each object once when it enters range;
    /// re-announces only if the player leaves and re-enters range.
    /// Silent during auto-walk (arrival already handles that).
    /// Guard: ModSettings.ProximityEnabled.
    /// </summary>
    public class ProximityHandler
    {
        #region Constants

        private const float ScanInterval  = 2f;  // seconds between passive scans
        private const float NpcRadius     = 5f;  // metres — NPCs
        private const float ObjectRadius  = 4f;  // metres — interactables

        #endregion

        #region State

        private float       _lastScan        = -999f;
        private Func<bool>  _isAutoWalkActive;

        // Transforms currently in range — used to detect new entries
        private readonly HashSet<Transform> _inRange = new HashSet<Transform>();

        // Cached reflection field for NPC name (same as NavigationHandler)
        private static FieldInfo _characterNameField;

        #endregion

        #region Lifecycle

        /// <summary>
        /// Call from Main.Start().
        /// isAutoWalkActive — delegate that returns true while auto-walk is running.
        /// </summary>
        public void Initialize(Func<bool> isAutoWalkActive)
        {
            _isAutoWalkActive = isAutoWalkActive;

            _characterNameField = typeof(CharacterDialogueTree).GetField(
                "_characterName",
                BindingFlags.NonPublic | BindingFlags.Instance);

            DebugLogger.Log(LogCategory.State, "ProximityHandler", "Initialisé");
        }

        /// <summary>Call from Main.Update().</summary>
        public void Update()
        {
            if (!ModSettings.ProximityEnabled)          return;
            if (_isAutoWalkActive != null && _isAutoWalkActive()) return;
            if (!OWInput.IsInputMode(InputMode.Character)) return;
            if (Time.time - _lastScan < ScanInterval)   return;

            _lastScan = Time.time;

            Transform playerTr = Locator.GetPlayerTransform();
            if (playerTr == null) return;

            Vector3 playerPos = playerTr.position;
            var     nowInRange = new HashSet<Transform>();

            // ── NPCs (CharacterDialogueTree) ──────────────────────────────────
            foreach (CharacterDialogueTree npc in
                     UnityEngine.Object.FindObjectsOfType<CharacterDialogueTree>())
            {
                if (!npc.gameObject.activeInHierarchy) continue;
                float dist = Vector3.Distance(playerPos, npc.transform.position);
                if (dist > NpcRadius) continue;

                nowInRange.Add(npc.transform);

                if (!_inRange.Contains(npc.transform))
                {
                    string name = GetNpcName(npc);
                    ScreenReader.Say(Loc.Get("proximity_nearby", name),
                                     SpeechPriority.Normal);
                    DebugLogger.Log(LogCategory.State, "ProximityHandler",
                                    $"NPC entré portée: {name} ({dist:F1}m)");
                }
            }

            // ── Interactables (InteractReceiver) ──────────────────────────────
            foreach (InteractReceiver recv in
                     UnityEngine.Object.FindObjectsOfType<InteractReceiver>())
            {
                if (!recv.gameObject.activeInHierarchy) continue;
                // Skip NPC-owned receivers (already covered above)
                if (recv.GetComponentInParent<CharacterDialogueTree>() != null) continue;
                // Skip ship interior
                Transform shipTr = Locator.GetShipTransform();
                if (shipTr != null && recv.transform.IsChildOf(shipTr)) continue;

                float dist = Vector3.Distance(playerPos, recv.transform.position);
                if (dist > ObjectRadius) continue;

                nowInRange.Add(recv.transform);

                if (!_inRange.Contains(recv.transform))
                {
                    string name = GetInteractName(recv);
                    ScreenReader.Say(Loc.Get("proximity_nearby", name),
                                     SpeechPriority.Normal);
                    DebugLogger.Log(LogCategory.State, "ProximityHandler",
                                    $"Objet entré portée: {name} ({dist:F1}m)");
                }
            }

            _inRange.Clear();
            foreach (Transform t in nowInRange) _inRange.Add(t);
        }

        #endregion

        #region Private helpers

        private string GetNpcName(CharacterDialogueTree npc)
        {
            try
            {
                if (_characterNameField != null)
                {
                    string raw = _characterNameField.GetValue(npc) as string;
                    if (!string.IsNullOrEmpty(raw))
                        return TextTranslation.Translate(raw);
                }
            }
            catch { }
            return npc.gameObject.name;
        }

        private static string GetInteractName(InteractReceiver recv)
        {
            string name = recv.gameObject.name;
            if (name.StartsWith("Interact") || name == "Interaction" || name == "InteractVolume")
            {
                Transform parent = recv.transform.parent;
                if (parent != null) name = parent.gameObject.name;
            }
            return name.Replace("_", " ").Trim();
        }

        #endregion
    }
}
