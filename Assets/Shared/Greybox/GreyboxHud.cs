using AIRGAP.Facility.Guards;
using AIRGAP.Facility.Lighting;
using AIRGAP.Infiltrator;
using UnityEngine;

namespace AIRGAP.Shared.Greybox
{
    /// <summary>
    /// Grey-box debug overlay: stance and vent state (Phase 1), plus the light-level
    /// meter, guard vision categories, and the last hearing roll (Phase 2) — the
    /// per-event probability readout is the stealth model's main tuning surface.
    /// </summary>
    public class GreyboxHud : MonoBehaviour
    {
        private InfiltratorController _player;
        private GuardVision[] _vision;
        private GuardHearing[] _hearing;
        private GUIStyle _style;

        private void OnGUI()
        {
            if (_player == null)
            {
                _player = FindFirstObjectByType<InfiltratorController>();
                _vision = FindObjectsByType<GuardVision>(FindObjectsSortMode.None);
                _hearing = FindObjectsByType<GuardHearing>(FindObjectsSortMode.None);
                if (_player == null) return;
            }
            _style ??= new GUIStyle(GUI.skin.label) { fontSize = 13, richText = true };

            GUILayout.BeginArea(new Rect(12, 12, 430, 300), GUI.skin.box);
            GUILayout.Label("<b>AIRGAP grey-box</b>  (WASD move, Shift sprint, Ctrl/C crouch, F flashlight, T test noise)", _style);
            GUILayout.Label($"Stance: <b>{_player.Stance.Current}</b>   loudness {_player.Stance.CurrentLoudness:F2}   speed {_player.Stance.CurrentSpeed:F1}", _style);
            GUILayout.Label($"In vent: <b>{_player.InTraversal}</b>   flashlight: <b>{(_player.FlashlightOn ? "ON" : "off")}</b>", _style);

            float light = VisibilitySampler.SampleAt(_player.Position);
            LightCategory category = VisibilitySampler.Categorize(light);
            GUILayout.Label($"Light level: <b>{light:F2}</b> [{Meter(light)}]  <b>{category}</b>", _style);

            foreach (GuardVision guard in _vision)
            {
                if (guard == null) continue;
                GUILayout.Label($"{guard.GuardId} sees: <b>{guard.Current}</b>  (target light {guard.TargetLightCategory}, eff. range {guard.EffectiveRange:F1})", _style);
            }

            foreach (GuardHearing guard in _hearing)
            {
                if (guard == null || guard.LastResult == null) continue;
                GuardHearing.HearingResult heard = guard.LastResult.Value;
                GUILayout.Label($"{heard.GuardId} heard <i>{heard.Sound.Type}</i> d={heard.Distance:F1}: p=<b>{heard.Probability:F3}</b> roll={heard.Roll:F3} → <b>{(heard.Noticed ? "NOTICED" : "missed")}</b>", _style);
            }

            GUILayout.EndArea();
        }

        private static string Meter(float value)
        {
            int filled = Mathf.Clamp(Mathf.RoundToInt(value * 10f), 0, 10);
            return new string('█', filled) + new string('░', 10 - filled);
        }
    }
}
