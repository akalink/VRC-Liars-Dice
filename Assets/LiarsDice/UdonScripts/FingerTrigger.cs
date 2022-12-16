
using System;
using TMPro;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

namespace akaUdon
{
 
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class FingerTrigger : UdonSharpBehaviour
    {
        [SerializeField] private UdonBehaviour behavior;
        [SerializeField] private string methodName = "";
        [SerializeField] private string pushInMethod = "_DepressedClickSoundAll";
        private readonly string handTrackerName = "trackhand12345";
        private VRCPlayerApi localPlayer;
        private bool inVR;
        private Collider collider;
        private Animator anim;
        
        
        void Start()
        {
            anim = GetComponentInParent<Animator>();
            collider = GetComponent<Collider>();
            localPlayer = Networking.LocalPlayer;
            inVR = localPlayer.IsUserInVR();
            collider.enabled = inVR;
            gameObject.SetActive(inVR);
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
                if(anim != null){anim.SetBool(methodName, true);}
                if(behavior != null){behavior.SendCustomEvent(pushInMethod);}
                if (other.gameObject.name.Contains("L"))
                {
                    localPlayer.PlayHapticEventInHand(VRC_Pickup.PickupHand.Left, 0.5f, Single.MaxValue, 0.2f);
                }
                else
                {
                    localPlayer.PlayHapticEventInHand(VRC_Pickup.PickupHand.Right, 0.5f, Single.MaxValue, 0.2f);
                }
            }
        }

        public void OnTriggerExit(Collider other)
        {
            if (other != null && other.gameObject.name.Contains(handTrackerName))
            {
                if(behavior != null){behavior.SendCustomEvent(methodName);}
                if(anim != null){anim.SetBool(methodName, false);}
                if (other.gameObject.name.Contains("L"))
                {
                    localPlayer.PlayHapticEventInHand(VRC_Pickup.PickupHand.Left, 0.5f, Single.MaxValue, 0.5f);
                }
                else
                {
                    localPlayer.PlayHapticEventInHand(VRC_Pickup.PickupHand.Right, 0.5f, Single.MaxValue, 0.5f);
                }
            }
        }
    }
}
