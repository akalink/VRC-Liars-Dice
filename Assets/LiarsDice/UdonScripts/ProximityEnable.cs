
using TMPro;
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;

namespace akaUdon
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class ProximityEnable : UdonSharpBehaviour
    {
        [SerializeField] private GameObject objectToToggle;
        private GameObject parent;
        private TextMeshProUGUI[] textFields;
        private Image[] buttonImages;
        private bool currentState = false;
        private Collider collider;
        [HideInInspector] public TextMeshProUGUI logger;
        [HideInInspector] public bool logging = false;
        

        void Start()
        {
            parent = this.transform.parent.gameObject;
            collider = GetComponent<Collider>();
            collider.enabled = false;
            Log("Got the name of parent " + parent.name);
            textFields = parent.GetComponentsInChildren<TextMeshProUGUI>(true);
            buttonImages = parent.GetComponentsInChildren<Image>(true);
            SetSetter(currentState);
            Log("Buttons amount is " +buttonImages.Length + " and text is " + textFields.Length);
            collider.enabled = true;
        }
        

        public override void OnPlayerTriggerEnter(VRCPlayerApi player)
        {
            if (player == Networking.LocalPlayer)
            {
                currentState = true;
                SetSetter(currentState);
            }
        }

        public override void OnPlayerTriggerExit(VRCPlayerApi player)
        {
            if (player == Networking.LocalPlayer)
            {
                currentState = false;
                SetSetter(currentState);
            }
        }

        private void SetSetter(bool state)
        {
            Log("All text and button visablity should be " + state);
            objectToToggle.SetActive(state);
            int maxSize = textFields.Length > buttonImages.Length ? textFields.Length : buttonImages.Length;
            
            for (int i = 0; i < maxSize; i++)
            {
                if (i < textFields.Length)
                {
                    textFields[i].enabled = state;
                }

                if (i < buttonImages.Length)
                {
                    buttonImages[i].enabled = state;
                }
            }
        }
        
        private void Log(string message)
        {
            if(!logging){return;}
            
            string m = "-" + System.DateTime.Now + "-"+ gameObject.name + "- " + message + "\n";
            if (logger != null)
            {
                logger.text += m;
            }
            else
            {
                Debug.Log(m);
            }
        }
    }
}
