
using JetBrains.Annotations;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using VRC.Udon.Common.Interfaces;

namespace akaUdon
{
    [AddComponentMenu("")]
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]

    public class PooledObject : UdonSharpBehaviour
    {
        [PublicAPI, HideInInspector]
        public VRCPlayerApi Owner;
        
        private VRCPlayerApi _localPlayer;

        public LiarsDiceMaster liarDice;
        
        [UdonSynced]
        public int value = -1;
        private int _prevValue = -1;

        [UdonSynced()] private int multiValue;
        [UdonSynced()] private int dieValue;
        
        private void Start()
        {
            _localPlayer = Networking.LocalPlayer;
        }

        [PublicAPI]
        public void _OnOwnerSet()
        {
            // Initialize the object here
            if (Owner.isLocal)
            {
                _SetValue(Random.Range(0, 100));
            }
        }
        
        [PublicAPI]
        public void _OnCleanup()
        {
            // Cleanup the object here
            if (Networking.IsMaster) 
            {
                _SetValue(-1);
            }
            transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        }
        
        public override void OnDeserialization()
        {
            _OnValueChanged();
        }

        
        [PublicAPI]
        public void _SetValue(int newValue)
        {
            value = newValue;
            RequestSerialization();
            _OnValueChanged();
        }

        public void _SetMultiDie(int multi, int die)
        {
            multiValue = multi;
            dieValue = die;
            RequestSerialization();
            SendCustomNetworkEvent(NetworkEventTarget.All, nameof(SendMutliDie));
        }

        public void SendMutliDie()
        {
            liarDice.ReceiveValues(multiValue, dieValue);
        }
        
        private void _OnValueChanged()
        {
            if (_prevValue == value)
            {
                return;
            }

            _prevValue = value;

            //_UpdateDebugDisplay();
        }
        
        public void _IncreaseValue()
        {
            if (Owner.isLocal)
            {
                _SetValue((value + 1) % 100);
            }
        }

        public void _JoinGame()
        {
            SendCustomNetworkEvent(NetworkEventTarget.All, nameof(JoiningGame));
        }

        public void JoiningGame()
        {
            liarDice.AddPlayerToGame(Owner, value);
        }

        public void _RemoveGame()
        {
            SendCustomNetworkEvent(NetworkEventTarget.All, nameof(RemovingGame));
        }

        public void RemovingGame()
        {
            liarDice.RemovePlayerFromGame(Owner);
        }
        
    }
}