
using System;
using TMPro;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

namespace akaUdon
{
    //todo player touch interacts are happening more than once;
    public class FingerTrigger : UdonSharpBehaviour
    {
        [SerializeField] private UdonBehaviour behavior;
        [SerializeField] private string methodName = "";
        private string handTrackerName = "trackhand12345";
        private bool inVR;
        private Collider collider;
        
        
        void Start()
        {
            collider = GetComponent<Collider>();
            inVR = Networking.LocalPlayer.IsUserInVR();
            collider.enabled = inVR;
        }

        public void SetEnabledState(bool state)
        {
            if (inVR)
            {
                collider.enabled = state;
            }
        }

        public void OnTriggerEnter(Collider other)
        {
            if (other != null && other.gameObject.name.Contains(handTrackerName))
            {
                if(behavior != null){behavior.SendCustomEvent(methodName);}
                if (other.gameObject.name.Contains("L"))
                {
                    Networking.LocalPlayer.PlayHapticEventInHand(VRC_Pickup.PickupHand.Left, 0.5f, Single.MaxValue, 0.2f);
                }
                else
                {
                    Networking.LocalPlayer.PlayHapticEventInHand(VRC_Pickup.PickupHand.Right, 0.5f, Single.MaxValue, 0.2f);
                }
            }
        }
    }
}
