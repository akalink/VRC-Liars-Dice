
using TMPro;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

namespace akaUdon
{
    public class PositionTracker : UdonSharpBehaviour
    {
        [Header("A system that will follow the players position and optionally their head and hands allow them to press buttons with their hands while in vr")]
        public bool allowVRHandCollision = true;
        private bool fingerCollision = false;
        public TextMeshProUGUI logger;
        private HumanBodyBones LeftBone;
        private HumanBodyBones RightBone;
        private bool insideArea = false;
        private VRCPlayerApi LocalPlayer;
        private bool isNull = false;
        private Transform[] trackedPoints;

        private Collider collision;
        
        private void LoggerPrint(string text)
        {
            if (logger != null)
            {
                logger.text += "-" + this.name + "-" + text + "\n";
            }
        }

        #region InitializeAllTheThings
        void Start()
        {
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
                LoggerPrint("VR and bone check returned " + allowVRHandCollision);
            }
            else
            {
                trackedPoints[1].gameObject.SetActive(false);
                trackedPoints[2].gameObject.SetActive(false);
                LoggerPrint("Hand Colliders are disabled");
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

        public override void OnPlayerTriggerEnter(VRCPlayerApi player)
        {
            if (player == Networking.LocalPlayer)// && Networking.LocalPlayer.IsUserInVR())
            {
                insideArea = true;
                trackedPoints[1].gameObject.SetActive(true);
                trackedPoints[2].gameObject.SetActive(true);
                //collision.enabled = true;
            }
        }

        public override void OnPlayerTriggerExit(VRCPlayerApi player)
        {
            if (player == Networking.LocalPlayer)// && Networking.LocalPlayer.IsUserInVR())
            {
                insideArea = false;
                trackedPoints[1].gameObject.SetActive(false);
                trackedPoints[2].gameObject.SetActive(false);
                //collision.enabled = false;
            }
        }

        #endregion
        

        private void Update()
        {
            if (!isNull && insideArea)
            {
                if (allowVRHandCollision)
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
                    
                }
            }
        }
    }
}
