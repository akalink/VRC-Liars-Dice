
using TMPro;
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
        private GameObject parent;
        private TextMeshProUGUI[] textFields;

        void Start()
        {
            objectToToggle.SetActive(false);

            parent = this.transform.parent.gameObject;
            Debug.Log("Got the name of parent " +parent.name);
            textFields = parent.GetComponentsInChildren<TextMeshProUGUI>();
            foreach (TextMeshProUGUI tmp in textFields)
            {
                tmp.enabled = false;
            }
        }

        public override void OnPlayerTriggerEnter(VRCPlayerApi player)
        {
            objectToToggle.SetActive(true);
            foreach (TextMeshProUGUI tmp in textFields)
            {
                tmp.enabled = true;
            }
        }

        public override void OnPlayerTriggerExit(VRCPlayerApi player)
        {
            objectToToggle.SetActive(false);
            foreach (TextMeshProUGUI tmp in textFields)
            {
                tmp.enabled = false;
            }
        }
    }
}
