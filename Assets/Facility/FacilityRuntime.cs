using AIRGAP.Facility.Blueprints;
using UnityEngine;

namespace AIRGAP.Facility
{
    /// <summary>
    /// Scene-side bootstrap for facility backends that need the blueprint data at
    /// runtime (currently the door system). Placed by the scene loader; validators
    /// call EnsureInitialized directly.
    /// </summary>
    public class FacilityRuntime : MonoBehaviour
    {
        [SerializeField] private string blueprintBaseName = "blueprint01";

        private bool _initialized;

        public void SetBlueprint(string baseName) => blueprintBaseName = baseName;

        public void EnsureInitialized()
        {
            if (_initialized) return;
            Blueprint bp = Blueprint.LoadFromResources(blueprintBaseName);
            RoleAssignment assignment = RoleAssignment.LoadFromResources(
                AssignmentData.AssignmentPathFor(blueprintBaseName));
            DoorSystem.Initialize(bp, assignment);
            _initialized = true;
        }

        private void Awake() => EnsureInitialized();
    }
}
