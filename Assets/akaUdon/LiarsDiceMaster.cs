using System;
using Cyan.PlayerObjectPool;
using JetBrains.Annotations;
using TMPro;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using VRC.Udon.Common.Interfaces;
using Random = UnityEngine.Random;


namespace akaUdon
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class LiarsDiceMaster : UdonSharpBehaviour
    {
        //todo fix late joiner message
        
        //todo make it so table raised part is part of the playerHandle prefab
        [UdonSynced()] private int[] dieValues = new int[20];
        [UdonSynced()] private int[] remaining = new int[4]; //stretch goal, make these modular, no magic numbers
        private Renderer[] diceMesh; 
        [SerializeField] private string materialFloatName = "_frame";
        [UdonSynced()] private int[] currentPlayers = new int[4];
        [UdonSynced()] private int numJoinedPlayers = 0;
        [UdonSynced()] private int playingPlayer = -1;
        [UdonSynced()] private bool gameStarted = false;
        [UdonSynced()] private int currentMulti = 1;
        [UdonSynced()] private int currentDie = -1;
        [UdonSynced()] private int lastWinner = -1;
        private bool canInteract = false;
        private int diceLeft;
        private Collider[] canvasColliders;
        private Collider[] fingerColliders;
        public TextMeshProUGUI displayText;
        private PlayerHandle[] playerHandles;

        //from cyan object pool
        public CyanPlayerObjectAssigner objectPool;
        private PooledObject _localPoolObject;

        #region initialization
        void Start()
        {
            Collider[] tempColliders = GetComponentsInChildren<Collider>(true);

            
            playerHandles = GetComponentsInChildren<PlayerHandle>(true);
            canvasColliders = new Collider[playerHandles.Length];
            fingerColliders = new Collider[tempColliders.Length - playerHandles.Length];
            Debug.Log("temp colliders array length " + tempColliders.Length);
            currentPlayers = new int[playerHandles.Length];
            remaining = new int[playerHandles.Length];
            bool inVR = Networking.LocalPlayer.IsUserInVR();
            int tempIndexCanvas = 0;
            int tempIndexFinger = 0;
            foreach (Collider c in tempColliders)
            {
                if (c.gameObject.name.Contains("Canvas"))
                {
                    canvasColliders[tempIndexCanvas] = c;
                    canvasColliders[tempIndexCanvas].enabled = !inVR;
                    tempIndexCanvas++;
                }
                else
                {
                    fingerColliders[tempIndexFinger] = c;
                    fingerColliders[tempIndexFinger].enabled = inVR;
                    tempIndexFinger++;
                }
            }
            Debug.Log("Finger colliders length " + fingerColliders.Length + " tempFinger " + tempIndexFinger);
            
            diceMesh = GetComponentsInChildren<Renderer>();
            dieValues = new int[diceMesh.Length];
            for (int i = 0; i < currentPlayers.Length; i++) //initializes array of player ids to known value
            {
                currentPlayers[i] = -1;
                if (i < remaining.Length)
                {
                    remaining[i] = 5; //number of dice per player, make modular
                }

                if (i < playerHandles.Length)
                {
                    playerHandles[i]._SetPlayerNumber(i); //PlayerHandle behavior store their own index in the array.
                }
                
            }
            
            Randomize(); 
        
            AllDeserialization();
        }

        public void _ToggleInteractMethod()
        {
            if (Networking.LocalPlayer.IsUserInVR())
            {
                for (int i = 0; i < fingerColliders.Length; i++)
                {
                    fingerColliders[i].enabled = !fingerColliders[i].enabled;
                    if (i < canvasColliders.Length)
                    {
                        canvasColliders[i].enabled = !canvasColliders[i].enabled;
                    }
                }
            }
        }
        #endregion

        #region game logic
        
        public void _StartGame()
        {
            if(!canInteract){return;}
            Debug.Log("Clicked start game");
            if (numJoinedPlayers > 1)
            {
                SendCustomNetworkEvent(NetworkEventTarget.All, nameof(InitializeGame));
            }
        }

        public void _ContinueGame()
        {
            if(!canInteract){return;}
            SendCustomNetworkEvent(NetworkEventTarget.All, nameof(NewRound));
        }

        public void InitializeGame()
        {
            if (Networking.IsMaster)
            {
                Debug.Log("Starting new game with " + numJoinedPlayers + " number of players");
                gameStarted = true;
                playingPlayer = currentPlayers.Length-1;
                for (int i = 0; i < remaining.Length; i++)
                {
                    remaining[i] = 5;
                }
                NewRound();
            }
        }
        public void NewRound()
        {
            if (Networking.IsMaster)
            {
                Randomize();
                NextTurn();
            }
        }

        public void ReceiveValues(int multi, int die)
        {
            if (Networking.IsMaster)
            {
                currentMulti = multi;
                currentDie = die;
                NextTurn();
            }
        }

        private void NextTurn()
        {
            if (Networking.IsMaster)
            {
                int next = playingPlayer;
                do
                {
                    next++;
                    if (next >= currentPlayers.Length)
                    {
                        next = 0;
                    }
                } while (currentPlayers[next] == -1 || remaining[next] < 1);
                
                if (numJoinedPlayers <= 1 || playingPlayer == next)
                {
                    gameStarted = false;
                    lastWinner = currentPlayers[playingPlayer];
                    for (int i = 0; i < currentPlayers.Length; i++)
                    {
                        currentPlayers[i] = -1;
                    }

                    currentMulti = 1;
                    currentDie = -1;
                    
                    SendCustomNetworkEvent(NetworkEventTarget.All,nameof(ResetStations));
                    playingPlayer = -1;
                    RequestSerialization();
                    AllDeserialization();
                    return;
                }

                playingPlayer = next;
                RequestSerialization();
                AllDeserialization();
            }
        }

        public void ResetStations()
        {
            for (int i = 0; i < playerHandles.Length; i++)
            {
                playerHandles[i]._EndGame();
            }
            
        }

        public void _PlayerSubmitBid(int multi, int die)
        {
            _localPoolObject._SetMultiDie(multi, die);
        }

        public void _PlayerContests()
        {
            SendCustomNetworkEvent(NetworkEventTarget.All, nameof(Contest));
        }
        public void Contest()
        {
            int lastPlayer = playingPlayer;

            do
            {
                lastPlayer--;
                if (lastPlayer < 0)
                {
                    lastPlayer = currentPlayers.Length - 1;
                }
            } while (currentPlayers[lastPlayer] == -1);

            int sum = 0;

            for (int i = 0; i < dieValues.Length; i++)
            {
                int playerNum = i / 5;
                if (i < playerHandles.Length && currentPlayers[i] != -1)
                {
                    playerHandles[i]._ContinueState(true);
                }
                if (currentPlayers[playerNum] == -1) { continue; }
                
                
                if ((i % 5 + 1) <= remaining[playerNum])
                {
                    diceMesh[i].material.SetFloat(materialFloatName, dieValues[i]);
                   
                    if (dieValues[i] == currentDie) { sum++; }
                }
                else
                {
                    diceMesh[i].material.SetFloat(materialFloatName, 7);
                }
            }

            if (currentMulti > sum)
            {
                //ha ha perish
                displayText.text = "Liar found:\n Bid: ";
                if (Networking.IsMaster)
                {
                    remaining[lastPlayer]--;
                    if (remaining[lastPlayer] == 0)
                    {
                        numJoinedPlayers--;
                    }
                }
            }
            else
            {
                //oh god oh fuck
                if (Networking.IsMaster)
                {
                    remaining[playingPlayer]--;
                    if (remaining[playingPlayer] == 0)
                    {
                        numJoinedPlayers--;
                    }
                }

                displayText.text = " No Liars here:\n Bid: ";
            }
            displayText.text += currentMulti + " X " + (currentDie+1) + " die\n Actual "+ sum + " X " + (currentDie+1) + " die";
            diceLeft--; //corrects number of dice left becuase _turnStart (the data given to the next player) is run before deseralization.

            if (Networking.IsMaster)
            {
                currentDie = -1;
                currentMulti = 1;
                playingPlayer = lastPlayer;
            }

        }

        private void Randomize() 
        {
            if (!Networking.IsMaster && gameStarted)
            {
                return;
            }

            for (int i = 0; i < dieValues.Length; i++)
            {
                dieValues[i] = Random.Range(0, 5);
            }
            
        }
        
        #endregion
        
        #region player Involement logic
        
        [PublicAPI] //cyan object pool dependency
        public void _OnLocalPlayerAssigned()
        {

            // Get the local player's pool object so we can later perform operations on it.
            _localPoolObject = (PooledObject) objectPool._GetPlayerPooledUdon(Networking.LocalPlayer);

            // Allow the user to interact with this object.
            DisableInteractive = false;
        }
        
        public void _Join(int playerNum)
        {
            if (Utilities.IsValid(_localPoolObject) && !gameStarted) // check if valid aka someone is assigned it
            {
                Debug.Log("Playing attempting to join " + playerNum);
                _localPoolObject._SetValue(playerNum);
                _localPoolObject._JoinGame();
            }
        }

        public void _Leave()
        {
            if (Utilities.IsValid(_localPoolObject)) // check if valid
            {
                _localPoolObject._RemoveGame();
            }
        }

        public void AddPlayerToGame(VRCPlayerApi player, int playerNum)
        {
            if (Networking.IsMaster && Utilities.IsValid(player))
            {
                
                foreach (int id in currentPlayers) // check if already assigned
                {
                    
                    if (id == player.playerId) // return already if assigned
                    {
                        return;
                    }
                }
                

                numJoinedPlayers++;
                currentPlayers[playerNum] = player.playerId;

                RequestSerialization();
                AllDeserialization();
            }
        }
        
        public void RemovePlayerFromGame(VRCPlayerApi player)
        {
            
            if (Networking.IsMaster && Utilities.IsValid(player))
            {
                for (int i = 0; i < currentPlayers.Length; i++)
                {
                    if (currentPlayers[i] == player.playerId)
                    {
                        currentPlayers[i] = -1;
                        numJoinedPlayers--;
                        playerHandles[i]._LeaveSetter();
                        RequestSerialization();
                        AllDeserialization();
                        return;
                    }
                }
            }
        }

        public override void OnPlayerLeft(VRCPlayerApi player)
        {
            if (Networking.IsMaster)
            {
                for (int i = 0; i < currentPlayers.Length; i++)
                {
                    if (currentPlayers[i] == player.playerId)
                    {
                        currentPlayers[i] = -1;
                        numJoinedPlayers--;
                        if (playingPlayer == i)
                        {
                            NextTurn();
                        }

                        if (numJoinedPlayers == 1)
                        {
                            numJoinedPlayers = 0;
                            NextTurn();
                        }
                        
                        RequestSerialization();
                        AllDeserialization();
                        break;
                    }
                }
            }
        }
        #endregion

        #region manual network update

        public override void OnDeserialization()
        {
            AllDeserialization();
        }

        private void AllDeserialization()
        {

            Debug.Log("NumPlayers=" + numJoinedPlayers.ToString() + " Joined amount is " + numJoinedPlayers+" Playing Player is " + playingPlayer + ", Multi is " +currentMulti + " and die is " + currentDie);
            UpdateUIState();
            UpdateText();
            UpdateMesh();
        }

        private void UpdateUIState()
        {
            
            for (int i = 0; i < currentPlayers.Length; i++)
            {
                playerHandles[i]._ContinueState(false);
                if (currentPlayers[i] != -1)
                {
                    if (!gameStarted && numJoinedPlayers > 1)
                    {
                        playerHandles[i]._StartState(true);
                    }
                    playerHandles[i]._SetOwner(currentPlayers[i]);
                   

                    if (currentPlayers[i] == Networking.LocalPlayer.playerId)
                    {
                        canInteract = true;
                    }
                    if(!gameStarted){continue;}
                    
                    playerHandles[i]._isInGame();
                    
                    if (i == playingPlayer)
                    {
                        playerHandles[i]._Turnstart(currentMulti, currentDie, diceLeft);
                    }
                    else
                    {
                        playerHandles[i]._NotActive();
                    }
                }
                else
                {
                    playerHandles[i]._LeaveSetter();
                    playerHandles[i]._StartState(false);
                }
            }
        }

        private void UpdateText()
        {
            string listPlayers = "Liar's Dice";
            for (int i = 0; i < currentPlayers.Length; i++)
            {
                if (currentPlayers[i] != -1)
                {
                    playerHandles[i]._SetPlayerNameUI();
                }
                else
                {
                    playerHandles[i]._ClearPlayerNameUI();
                }
            }

            if (!gameStarted)
            {
                displayText.text = listPlayers;
            }


            if (gameStarted && currentDie > -1)
            {
                displayText.text = currentMulti.ToString() + " X " + (currentDie+1)  + " die";
            }
            else if(gameStarted && playingPlayer != -1)
            {
                VRCPlayerApi player = VRCPlayerApi.GetPlayerById(currentPlayers[playingPlayer]);
                displayText.text = player.displayName + "\n starts the round";
            }

            if (!gameStarted && lastWinner != -1)
            {
                VRCPlayerApi player = VRCPlayerApi.GetPlayerById(lastWinner);
                displayText.text = player.displayName + " is the Winner";
            }
        }

        private void UpdateMesh()
        {
            diceLeft = 0;
            for (int i = 0; i < dieValues.Length; i++)
            {
                if (i < diceMesh.Length && diceMesh[i] != null)
                {
                    int playerNum = i / 5;
                    
                    if (currentPlayers[playerNum] == -1)
                    {
                        diceMesh[i].material.SetFloat(materialFloatName, 8); //logo value in flipbook
                        continue;
                    }
                    
                    if ((i % 5 + 1) <= remaining[playerNum])
                    {
                        if (Networking.LocalPlayer.playerId == currentPlayers[playerNum])
                        {
                            diceMesh[i].material.SetFloat(materialFloatName, dieValues[i]);
                            diceLeft++;
                        }
                        else
                        {
                            diceMesh[i].material.SetFloat(materialFloatName, 6); //unknown value in flipbook
                            diceLeft++;
                        }
                    }
                    else
                    {
                        diceMesh[i].material.SetFloat(materialFloatName, 7); //elimanated value in flipbook
                    }
                }
            }
            Debug.Log("Dice Left " + diceLeft);
        }
                

        #endregion
    }
}