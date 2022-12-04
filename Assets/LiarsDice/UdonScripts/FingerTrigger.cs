
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
        private readonly string handTrackerName = "trackhand12345";
        private bool inVR;
        private Collider collider;
        private Animator anim;
        
        
        void Start()
        {
            anim = GetComponentInParent<Animator>();
            //Debug.Log("Found the parent component " + anim.gameObject.name);
            collider = GetComponent<Collider>();
            inVR = true;// Networking.LocalPlayer.IsUserInVR();
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
                if(behavior != null){behavior.SendCustomEvent("_DepressedClickSound");}
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

        public void OnTriggerExit(Collider other)
        {
            if (other != null && other.gameObject.name.Contains(handTrackerName))
            {
                if(behavior != null){behavior.SendCustomEvent(methodName);}
                if(anim != null){anim.SetBool(methodName, false);}
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

        private void OnTriggerStay(Collider other)
        {
            if (other != null && other.gameObject.name.Contains(handTrackerName))
            {
                float f = Vector3.Distance(this.transform.position, other.transform.position);
                Debug.Log("Trigger is running "+ f + " name is " + other.gameObject.name);
                if (anim != null)
                {
                    anim.SetBool(methodName, true);
                }
            }
        }
        
    }
}
