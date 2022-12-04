
using System;
using TMPro;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

namespace akaUdon
{
    public class PositionTracker : UdonSharpBehaviour
    {
        [Header("A system that will follow the players position and optionally their head and hands allow them to press buttons with their hands while in vr")]
        private bool allowVRHandCollision = true;
        private bool fingerCollision = false;
        [HideInInspector] public TextMeshProUGUI logger;
        [HideInInspector] public bool logging = true;
        private HumanBodyBones LeftBone;
        private HumanBodyBones RightBone;

        private VRCPlayerApi LocalPlayer;
        private bool isNull = false;
        private Transform[] trackedPoints;

        private Collider collision;
        private int count = 0;
        Transform one;
        Transform two;
        private float d = 0;
        
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

        #region InitializeAllTheThings
        void Start()
        {
            Log("Beging Initialization");
            collision = GetComponent<Collider>();
            
            if (Networking.LocalPlayer == null)
            {
                isNull = true;
                return;
            }
            LocalPlayer = Networking.LocalPlayer;

            trackedPoints = GetComponentsInChildren<Transform>(true);

            CheckVR();
        }
        public void CheckVR()
        {
            if (allowVRHandCollision)
            {
                allowVRHandCollision = LocalPlayer.IsUserInVR();
                if (allowVRHandCollision)
                {
                    fingerCollision = _Checkbones();
                }
                Log("VR and bone check returned " + allowVRHandCollision);
            }
            else
            {
                trackedPoints[1].gameObject.SetActive(false);
                trackedPoints[2].gameObject.SetActive(false);
                Log("Hand Colliders are disabled");
            }
        }
        

        private bool _Checkbones()
        {
            bool returnIfAssigned = false;
            if ((LocalPlayer.GetBonePosition(HumanBodyBones.RightIndexDistal) != Vector3.zero) ||
                (LocalPlayer.GetBonePosition(HumanBodyBones.LeftIndexDistal) != Vector3.zero))
            {
                RightBone = HumanBodyBones.RightIndexDistal;
                LeftBone = HumanBodyBones.LeftIndexDistal;
                returnIfAssigned = true;
            }
            else if ((LocalPlayer.GetBonePosition(HumanBodyBones.RightIndexIntermediate) != Vector3.zero) ||
                     (LocalPlayer.GetBonePosition(HumanBodyBones.LeftIndexIntermediate) != Vector3.zero))
            {
                RightBone = HumanBodyBones.RightIndexIntermediate;
                LeftBone = HumanBodyBones.LeftIndexIntermediate;
                returnIfAssigned = true;
            }
            else if ((LocalPlayer.GetBonePosition(HumanBodyBones.RightIndexProximal) != Vector3.zero) ||
                     (LocalPlayer.GetBonePosition(HumanBodyBones.LeftIndexProximal) != Vector3.zero))
            {
                RightBone = HumanBodyBones.RightIndexProximal;
                LeftBone = HumanBodyBones.LeftIndexProximal;
                returnIfAssigned = true;
            }

            return returnIfAssigned;
        }

        #endregion
        

        private void Update()
        {
            if (!isNull && allowVRHandCollision)
            {
                if (fingerCollision)
                {
                    trackedPoints[1].position = LocalPlayer.GetBonePosition(RightBone);
                    trackedPoints[2].position = LocalPlayer.GetBonePosition(LeftBone);
                }
                else
                { 
                    trackedPoints[1].position = LocalPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.RightHand).position;
                    trackedPoints[2].position = LocalPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.LeftHand).position;
                }

                count++;

                if (count % 240 == 0)
                {
                    
                       float f = Vector3.Distance(LocalPlayer.GetBonePosition(HumanBodyBones.Chest), LocalPlayer.GetBonePosition(HumanBodyBones.Spine));
                       if (Math.Abs(d - f) > .01)
                       {
                           Log("Avatar change detected");
                           CheckVR();
                       }

                       d = f;
                }

            }
        }
    }
}
