using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace AIRGAP.CI
{
    /// <summary>
    /// Editor-time helper: attach a rectangular ShadowCaster2D so greybox walls,
    /// crates, and props block Light2D VISUALLY the same way AirgapLight's
    /// linecasts already block them mechanically — without this, the flashlight
    /// beam renders straight through walls the gameplay model treats as opaque.
    /// The shape is written via SerializedObject because ShadowCaster2D exposes
    /// no public shape API; if the property ever disappears, the component still
    /// degrades to its default unit-square path.
    /// </summary>
    public static class Shadow2D
    {
        public static void AddRectCaster(GameObject go, float width, float height)
        {
            var caster = go.AddComponent<ShadowCaster2D>();
            caster.selfShadows = true;

            var serialized = new SerializedObject(caster);
            SerializedProperty path = serialized.FindProperty("m_ShapePath");
            if (path == null) return;
            float hw = width * 0.5f, hh = height * 0.5f;
            path.arraySize = 4;
            path.GetArrayElementAtIndex(0).vector3Value = new Vector3(-hw, -hh, 0f);
            path.GetArrayElementAtIndex(1).vector3Value = new Vector3(hw, -hh, 0f);
            path.GetArrayElementAtIndex(2).vector3Value = new Vector3(hw, hh, 0f);
            path.GetArrayElementAtIndex(3).vector3Value = new Vector3(-hw, hh, 0f);
            SerializedProperty hash = serialized.FindProperty("m_ShapePathHash");
            if (hash != null)
                hash.intValue = Mathf.RoundToInt(width * 1000f) * 31 + Mathf.RoundToInt(height * 1000f);
            serialized.ApplyModifiedProperties();
        }
    }
}
