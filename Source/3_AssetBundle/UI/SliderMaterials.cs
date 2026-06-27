using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

#nullable disable

namespace BeatLeader {
    [MovedFrom(true, "BeatLeader", "BeatLeader", null)]
    [CreateAssetMenu(fileName = "SliderMaterials", menuName = "SliderMaterials collection")]
    public class SliderMaterials : ScriptableObject {
        public Material normalized;
        public Material number;
        public Material value;
        public Material alpha;
    }
}
