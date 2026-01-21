using System.Collections.Generic;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace eradev.stolenrealm.ToggleDebugModeKeybind
{
    [BepInPlugin("eradev.stolenrealm.ToggleDebugModeKeybind", "Toggle Debug Mode (Keybind)", "1.0.0")]
    public class ToggleDebugModeKeybindPlugin : BaseUnityPlugin
    {
        private static ConfigEntry<KeyCode> _toggleKey;
        private static ManualLogSource _log;
        private static bool _isDebugEnabled = false;
        private static Harmony _harmony;

        private static readonly List<string> DebugKeys = new List<string>()
        {
            "DebugModeEnabled",
            "DebugMode:ToggleUnlockAllQuests",
            "DebugMode:ToggleUnlockMapNodes",
            "DebugMode:ToggleUnlockAllSkills",
            "DebugMode:ToggleFastMovement",
            "DebugMode:ToggleFreeCasting",
            "DebugMode:ToggleFreeMovement",
            "DebugMode:ToggleInvincibility",
            "DebugMode:ToggleFreeCrafting",
            "DebugMode:ToggleDamage_X10",
            "DebugMode:ToggleInfMoveObjectRange",
            "DebugMode:ToggleNeverExpendEvents",
            "DebugMode:ToggleHideTextEvents",
            "DebugMode:ToggleForceEvent",
            "DebugMode:InputEventRollResults",
            "DebugMode:EventRollValue",
            "DebugMode:ToggleForceProfession",
            "DebugMode:ToggleForceDestructible",
            "DebugMode:ToggleForceEnemyMod",
            "DebugMode:ProfessionToForce",
            "DebugMode:EnemyModToForce",
            "DebugMode:EventToForce",
            "DebugMode:DestructibleToForce",
            "DebugMode:StatusToGive"
        };

        private void Awake()
        {
            _log = Logger;
            _harmony = new Harmony("eradev.stolenrealm.ToggleDebugModeKeybind");

            // Configure the Delete key as the toggle key
            _toggleKey = Config.Bind("Keybind",
                "toggle_key",
                KeyCode.Delete,
                "Key to toggle debug mode and show/hide debug window");

            // Apply patches
            _harmony.PatchAll();

            Logger.LogInfo($"Plugin Toggle Debug Mode (Keybind) is loaded!");
        }

        private void OnDestroy()
        {
            // Clean up patches when plugin is unloaded
            _harmony?.UnpatchSelf();
        }

        private void Update()
        {
            // Check if the toggle key is pressed
            if (Input.GetKeyDown(_toggleKey.Value))
            {
                ToggleDebugMode();
            }
        }

        private void ToggleDebugMode()
        {
            // Always reload the instance reference to ensure we have a valid reference
            if (DebugWindow.Instance == null)
            {
                DebugWindow.LoadInstanceReference();
            }

            if (!_isDebugEnabled)
            {
                EnableDebugMode();
                _isDebugEnabled = true;
                _log.LogInfo("Debug mode enabled and window shown");
            }
            else
            {
                DisableDebugMode();
                _isDebugEnabled = false;
                _log.LogInfo("Debug mode disabled");
            }
        }

        private static void EnableDebugMode()
        {
            if (DebugWindow.Instance == null)
            {
                DebugWindow.LoadInstanceReference();
            }

            if (DebugWindow.Instance == null)
            {
                _log.LogError("Failed to load DebugWindow instance!");
                return;
            }

            // Set debug activated flag
            try
            {
                var fieldRef = AccessTools.FieldRefAccess<bool>(typeof(DebugWindow), "debugActivated");
                if (fieldRef != null)
                {
                    fieldRef(DebugWindow.Instance) = true;
                    _log.LogInfo("FieldRefAccess method succeeded");
                }
                else
                {
                    // Try alternative field name
                    var alternativeRef = AccessTools.FieldRefAccess<bool>(typeof(DebugWindow), "m_debugActivated");
                    if (alternativeRef != null)
                    {
                        alternativeRef(DebugWindow.Instance) = true;
                        _log.LogInfo("m_debugActivated method succeeded");
                    }
                    else
                    {
                        // Try direct reflection as fallback
                        var field = typeof(DebugWindow).GetField("debugActivated",
                            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        if (field != null)
                        {
                            field.SetValue(DebugWindow.Instance, true);
                            _log.LogInfo("Direct reflection method succeeded");
                        }
                        else
                        {
                            // Try any bool field that might control debug mode
                            var allFields = typeof(DebugWindow).GetFields(
                                System.Reflection.BindingFlags.NonPublic |
                                System.Reflection.BindingFlags.Instance |
                                System.Reflection.BindingFlags.Public);

                            foreach (var f in allFields)
                            {
                                if (f.FieldType == typeof(bool) &&
                                    (f.Name.ToLower().Contains("debug") ||
                                     f.Name.ToLower().Contains("activated")))
                                {
                                    f.SetValue(DebugWindow.Instance, true);
                                    _log.LogInfo($"Found and set field: {f.Name}");
                                    break;
                                }
                            }
                        }
                    }
                }
            }
            catch (System.Exception e)
            {
                _log.LogError($"Error setting debug activated: {e.Message}");
            }

            // Set PlayerPrefs flags to enable debug mode and bypass password
            PlayerPrefs.SetString("DebugModeEnabled", "TRUE");
            PlayerPrefs.SetString("DebugModeUnlocked", "TRUE");

            // Show the debug window
            if (DebugWindow.Instance != null)
            {
                DebugWindow.Instance.ShowDebugWindow();
            }
        }

        private static void DisableDebugMode()
        {
            if (DebugWindow.Instance != null)
            {
                DebugWindow.Instance.HideDebugWindow();

                // Try to set debug activated to false
                try
                {
                    var fieldRef = AccessTools.FieldRefAccess<bool>(typeof(DebugWindow), "debugActivated");
                    if (fieldRef != null)
                    {
                        fieldRef(DebugWindow.Instance) = false;
                    }
                }
                catch
                {
                    // If this fails, it's okay - we're hiding the window anyway
                }
            }

            // DON'T delete all debug keys anymore - we want to persist toggle states
            // Only delete the main debug mode enabled flag
            PlayerPrefs.DeleteKey("DebugModeEnabled");
        }

        // Harmony patches to fix the issues
        [HarmonyPatch(typeof(DebugWindow))]
        class DebugWindowPatches
        {
            // Patch ShowDebugWindow to always show controls (bypass password)
            [HarmonyPatch("ShowDebugWindow")]
            [HarmonyPrefix]
            static bool ShowDebugWindowPrefix(DebugWindow __instance)
            {
                // Force the password check to pass by setting the PlayerPrefs key
                PlayerPrefs.SetString("DebugModeUnlocked", "TRUE");

                // Set KeybindManager.instance.debugActivated to true
                try
                {
                    var keybindManagerType = AccessTools.TypeByName("KeybindManager");
                    if (keybindManagerType != null)
                    {
                        var instanceProperty = AccessTools.Property(keybindManagerType, "instance");
                        if (instanceProperty != null)
                        {
                            var keybindManagerInstance = instanceProperty.GetValue(null);
                            var debugActivatedField = AccessTools.Field(keybindManagerType, "debugActivated");
                            if (debugActivatedField != null)
                            {
                                debugActivatedField.SetValue(keybindManagerInstance, true);
                                _log.LogInfo("Set KeybindManager.instance.debugActivated to true");
                            }
                        }
                    }
                }
                catch (System.Exception e)
                {
                    _log.LogError($"Error setting debugActivated: {e.Message}");
                }

                // Call the private ShowControls method with false to bypass password
                var showControlsMethod = AccessTools.Method(typeof(DebugWindow), "ShowControls");
                if (showControlsMethod != null)
                {
                    showControlsMethod.Invoke(__instance, new object[] { false });
                }
                else
                {
                    _log.LogError("Could not find ShowControls method");
                }

                // Call the private InitAllData method
                var initAllDataMethod = AccessTools.Method(typeof(DebugWindow), "InitAllData");
                if (initAllDataMethod != null)
                {
                    initAllDataMethod.Invoke(__instance, null);
                }
                else
                {
                    _log.LogError("Could not find InitAllData method");
                }

                // Call OpenWindow
                __instance.OpenWindow();

                // Load toggle states from PlayerPrefs even when not in editor
                LoadToggleStates(__instance);

                return false; // Skip the original method
            }

            // Helper method to load toggle states
            private static void LoadToggleStates(DebugWindow instance)
            {
                try
                {
                    // Load all toggle states from PlayerPrefs
                    if (instance.UnlockAllSkillsToggle != null)
                        instance.UnlockAllSkillsToggle.isOn = PlayerPrefs.HasKey("DebugMode:ToggleUnlockAllSkills") && PlayerPrefs.GetString("DebugMode:ToggleUnlockAllSkills") == "TRUE";

                    if (instance.UnlockAllQuestsToggle != null)
                        instance.UnlockAllQuestsToggle.isOn = PlayerPrefs.HasKey("DebugMode:ToggleUnlockAllQuests") && PlayerPrefs.GetString("DebugMode:ToggleUnlockAllQuests") == "TRUE";

                    if (instance.UnlockMapNodesToggle != null)
                        instance.UnlockMapNodesToggle.isOn = PlayerPrefs.HasKey("DebugMode:ToggleUnlockMapNodes") && PlayerPrefs.GetString("DebugMode:ToggleUnlockMapNodes") == "TRUE";

                    if (instance.FastMovementToggle != null)
                        instance.FastMovementToggle.isOn = PlayerPrefs.HasKey("DebugMode:ToggleFastMovement") && PlayerPrefs.GetString("DebugMode:ToggleFastMovement") == "TRUE";

                    if (instance.FreeCastingToggle != null)
                        instance.FreeCastingToggle.isOn = PlayerPrefs.HasKey("DebugMode:ToggleFreeCasting") && PlayerPrefs.GetString("DebugMode:ToggleFreeCasting") == "TRUE";

                    if (instance.FreeMovementToggle != null)
                        instance.FreeMovementToggle.isOn = PlayerPrefs.HasKey("DebugMode:ToggleFreeMovement") && PlayerPrefs.GetString("DebugMode:ToggleFreeMovement") == "TRUE";

                    if (instance.InvincibilityToggle != null)
                        instance.InvincibilityToggle.isOn = PlayerPrefs.HasKey("DebugMode:ToggleInvincibility") && PlayerPrefs.GetString("DebugMode:ToggleInvincibility") == "TRUE";

                    if (instance.FreeCraftingToggle != null)
                        instance.FreeCraftingToggle.isOn = PlayerPrefs.HasKey("DebugMode:ToggleFreeCrafting") && PlayerPrefs.GetString("DebugMode:ToggleFreeCrafting") == "TRUE";

                    if (instance.Damage_X10Toggle != null)
                        instance.Damage_X10Toggle.isOn = PlayerPrefs.HasKey("DebugMode:ToggleDamage_X10") && PlayerPrefs.GetString("DebugMode:ToggleDamage_X10") == "TRUE";

                    if (instance.InfMoveObjectRangeToggle != null)
                        instance.InfMoveObjectRangeToggle.isOn = PlayerPrefs.HasKey("DebugMode:ToggleInfMoveObjectRange") && PlayerPrefs.GetString("DebugMode:ToggleInfMoveObjectRange") == "TRUE";

                    if (instance.HideTextEventsToggle != null)
                        instance.HideTextEventsToggle.isOn = PlayerPrefs.HasKey("DebugMode:ToggleHideTextEvents") && PlayerPrefs.GetString("DebugMode:ToggleHideTextEvents") == "TRUE";

                    if (instance.ForceProfessionToggle != null)
                        instance.ForceProfessionToggle.isOn = PlayerPrefs.HasKey("DebugMode:ToggleForceProfession") && PlayerPrefs.GetString("DebugMode:ToggleForceProfession") == "TRUE";

                    if (instance.ForceEnemyModToggle != null)
                        instance.ForceEnemyModToggle.isOn = PlayerPrefs.HasKey("DebugMode:ToggleForceEnemyMod") && PlayerPrefs.GetString("DebugMode:ToggleForceEnemyMod") == "TRUE";

                    if (instance.InputEventRollResultToggle != null)
                        instance.InputEventRollResultToggle.isOn = PlayerPrefs.HasKey("DebugMode:InputEventRollResults") && PlayerPrefs.GetString("DebugMode:InputEventRollResults") == "TRUE";

                    if (instance.ForceEventToggle != null)
                        instance.ForceEventToggle.isOn = PlayerPrefs.HasKey("DebugMode:ToggleForceEvent") && PlayerPrefs.GetString("DebugMode:ToggleForceEvent") == "TRUE";

                    if (instance.ForceDestructibleToggle != null)
                        instance.ForceDestructibleToggle.isOn = PlayerPrefs.HasKey("DebugMode:ToggleForceDestructible") && PlayerPrefs.GetString("DebugMode:ToggleForceDestructible") == "TRUE";

                    // Load EventRollValue
                    if (instance.EventRollValueInput != null && PlayerPrefs.HasKey("DebugMode:EventRollValue"))
                    {
                        instance.EventRollValueInput.text = PlayerPrefs.GetInt("DebugMode:EventRollValue").ToString();
                    }

                    _log.LogInfo("Loaded toggle states from PlayerPrefs");
                }
                catch (System.Exception e)
                {
                    _log.LogError($"Error loading toggle states: {e.Message}");
                }
            }

            // Patch SubmitPassword to always succeed
            [HarmonyPatch("SubmitPassword")]
            [HarmonyPrefix]
            static bool SubmitPasswordPrefix(DebugWindow __instance)
            {
                // Always succeed and unlock debug mode
                PlayerPrefs.SetString("DebugModeUnlocked", "TRUE");

                // Set KeybindManager.instance.debugActivated to true
                try
                {
                    var keybindManagerType = AccessTools.TypeByName("KeybindManager");
                    if (keybindManagerType != null)
                    {
                        var instanceProperty = AccessTools.Property(keybindManagerType, "instance");
                        if (instanceProperty != null)
                        {
                            var keybindManagerInstance = instanceProperty.GetValue(null);
                            var debugActivatedField = AccessTools.Field(keybindManagerType, "debugActivated");
                            if (debugActivatedField != null)
                            {
                                debugActivatedField.SetValue(keybindManagerInstance, true);
                            }
                        }
                    }
                }
                catch { }

                // Call the private ShowControls method with false
                var showControlsMethod = AccessTools.Method(typeof(DebugWindow), "ShowControls");
                if (showControlsMethod != null)
                {
                    showControlsMethod.Invoke(__instance, new object[] { false });
                }

                // Clear the password input
                __instance.PasswordInput.text = "";

                return false; // Skip original method
            }

            // Patch the toggle methods to save to PlayerPrefs when toggled
            // This ensures toggle states are saved even outside the editor
            [HarmonyPatch("ToggleUnlockAllQuests")]
            [HarmonyPostfix]
            static void ToggleUnlockAllQuestsPostfix()
            {
                PlayerPrefs.SetString("DebugMode:ToggleUnlockAllQuests", DebugWindow.UnlockAllQuests ? "TRUE" : "FALSE");
            }

            [HarmonyPatch("ToggleUnlockMapNodes")]
            [HarmonyPostfix]
            static void ToggleUnlockMapNodesPostfix()
            {
                PlayerPrefs.SetString("DebugMode:ToggleUnlockMapNodes", DebugWindow.UnlockMapNodes ? "TRUE" : "FALSE");
            }

            [HarmonyPatch("ToggleUnlockAllSkills")]
            [HarmonyPostfix]
            static void ToggleUnlockAllSkillsPostfix()
            {
                PlayerPrefs.SetString("DebugMode:ToggleUnlockAllSkills", DebugWindow.UnlockAllSkills ? "TRUE" : "FALSE");
            }

            [HarmonyPatch("ToggleFastMovement")]
            [HarmonyPostfix]
            static void ToggleFastMovementPostfix()
            {
                PlayerPrefs.SetString("DebugMode:ToggleFastMovement", DebugWindow.FastMovement ? "TRUE" : "FALSE");
            }

            [HarmonyPatch("ToggleFreeCasting")]
            [HarmonyPostfix]
            static void ToggleFreeCastingPostfix()
            {
                PlayerPrefs.SetString("DebugMode:ToggleFreeCasting", DebugWindow.FreeCasting ? "TRUE" : "FALSE");
            }

            [HarmonyPatch("ToggleFreeMovement")]
            [HarmonyPostfix]
            static void ToggleFreeMovementPostfix()
            {
                PlayerPrefs.SetString("DebugMode:ToggleFreeMovement", DebugWindow.FreeMovement ? "TRUE" : "FALSE");
            }

            [HarmonyPatch("ToggleInvincibility")]
            [HarmonyPostfix]
            static void ToggleInvincibilityPostfix()
            {
                PlayerPrefs.SetString("DebugMode:ToggleInvincibility", DebugWindow.Invincibility ? "TRUE" : "FALSE");
            }

            [HarmonyPatch("ToggleFreeCrafting")]
            [HarmonyPostfix]
            static void ToggleFreeCraftingPostfix()
            {
                PlayerPrefs.SetString("DebugMode:ToggleFreeCrafting", DebugWindow.FreeCrafting ? "TRUE" : "FALSE");
            }

            [HarmonyPatch("ToggleDamage_X10")]
            [HarmonyPostfix]
            static void ToggleDamage_X10Postfix()
            {
                PlayerPrefs.SetString("DebugMode:ToggleDamage_X10", DebugWindow.Damage_X10 ? "TRUE" : "FALSE");
            }

            [HarmonyPatch("ToggleInfMoveObjectRange")]
            [HarmonyPostfix]
            static void ToggleInfMoveObjectRangePostfix()
            {
                PlayerPrefs.SetString("DebugMode:ToggleInfMoveObjectRange", DebugWindow.InfMoveObjectRange ? "TRUE" : "FALSE");
            }

            [HarmonyPatch("ToggleHideTextEvents")]
            [HarmonyPostfix]
            static void ToggleHideTextEventsPostfix()
            {
                PlayerPrefs.SetString("DebugMode:ToggleHideTextEvents", DebugWindow.HideTextEvents ? "TRUE" : "FALSE");
            }

            [HarmonyPatch("ToggleForceEvent")]
            [HarmonyPostfix]
            static void ToggleForceEventPostfix()
            {
                PlayerPrefs.SetString("DebugMode:ToggleForceEvent", DebugWindow.ForceEvent ? "TRUE" : "FALSE");
            }

            [HarmonyPatch("ToggleInputEventRollResults")]
            [HarmonyPostfix]
            static void ToggleInputEventRollResultsPostfix()
            {
                PlayerPrefs.SetString("DebugMode:InputEventRollResults", DebugWindow.InputEventRollResults ? "TRUE" : "FALSE");
            }

            [HarmonyPatch("EventRollValueChanged")]
            [HarmonyPostfix]
            static void EventRollValueChangedPostfix()
            {
                PlayerPrefs.SetInt("DebugMode:EventRollValue", DebugWindow.InputEventRollResultsValue);
            }

            [HarmonyPatch("ToggleForceProfession")]
            [HarmonyPostfix]
            static void ToggleForceProfessionPostfix()
            {
                PlayerPrefs.SetString("DebugMode:ToggleForceProfession", DebugWindow.ForceProfession ? "TRUE" : "FALSE");
            }

            [HarmonyPatch("ToggleForceDestructible")]
            [HarmonyPostfix]
            static void ToggleForceDestructiblePostfix()
            {
                PlayerPrefs.SetString("DebugMode:ToggleForceDestructible", DebugWindow.ForceDestructible ? "TRUE" : "FALSE");
            }

            [HarmonyPatch("ToggleForceEnemyMod")]
            [HarmonyPostfix]
            static void ToggleForceEnemyModPostfix()
            {
                PlayerPrefs.SetString("DebugMode:ToggleForceEnemyMod", DebugWindow.ForceEnemyMod ? "TRUE" : "FALSE");
            }
        }
    }
}