using AIRGAP.Infiltrator;
using UnityEngine;

namespace AIRGAP.Shared.Greybox
{
    /// <summary>
    /// Grey-box debug overlay: stance, vent state, and (Phase 2) the light-level
    /// meter, guard vision categories, and last hearing-roll probability — the
    /// single most important tuning surface in the stealth model.
    /// </summary>
    public class GreyboxHud : MonoBehaviour
    {
        private InfiltratorController _player;
        private GUIStyle _style;

        private void OnGUI()
        {
            if (_player == null)
            {
                _player = FindFirstObjectByType<InfiltratorController>();
                if (_player == null) return;
            }
            _style ??= new GUIStyle(GUI.skin.label) { fontSize = 13, richText = true };

            GUILayout.BeginArea(new Rect(12, 12, 380, 260), GUI.skin.box);
            GUILayout.Label("<b>AIRGAP grey-box</b>  (WASD move, Shift sprint, Ctrl/C crouch, F flashlight, T test noise)", _style);
            GUILayout.Label($"Stance: <b>{_player.Stance.Current}</b>   loudness {_player.Stance.CurrentLoudness:F2}   speed {_player.Stance.CurrentSpeed:F1}", _style);
            GUILayout.Label($"In vent: <b>{_player.InTraversal}</b>   flashlight: <b>{(_player.FlashlightOn ? "ON" : "off")}</b>", _style);
            DrawPhase2Lines();
            GUILayout.EndArea();
        }

        private void DrawPhase2Lines()
        {
            // Extended by Phase 2 (light meter, vision categories, hearing rolls).
        }
    }
}
