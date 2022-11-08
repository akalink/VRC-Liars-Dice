
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

namespace akaUdon
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class ProximityEnable : UdonSharpBehaviour
    {
        [SerializeField] private GameObject objectToToggle;

        void Start()
        {
            objectToToggle.SetActive(false);
        }

        public override void OnPlayerTriggerEnter(VRCPlayerApi player)
        {
            objectToToggle.SetActive(true);
        }

        public override void OnPlayerTriggerExit(VRCPlayerApi player)
        {
            objectToToggle.SetActive(false);
        }
    }
}
