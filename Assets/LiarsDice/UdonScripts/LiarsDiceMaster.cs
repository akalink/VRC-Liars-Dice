using System;
using Cyan.PlayerObjectPool;
using JetBrains.Annotations;
using TMPro;
using UdonSharp;
using UnityEngine;
using UnityEngine.Windows.WebCam;
using VRC.SDKBase;
using VRC.Udon;
using VRC.Udon.Common.Interfaces;
using Random = UnityEngine.Random;


namespace akaUdon
{
    /*
     * Summary
     * The script handles all syncing and core logic for the liars dice table
     * It stores the state of the game and distributes that data to all the UI logic scripts (playerHandle)
     * It updates the visuals of all synced aspects of the table.
     */
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class LiarsDiceMaster : UdonSharpBehaviour
    {
        /*Buglist:
            Late joiners during contests will not see the correct message. It will say the last player will start the turn.
            Nothing is initalized until table is interacted with 
            */
        #region Instance Variables
        
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
        [UdonSynced()] private bool onesWild = false;
        [SerializeField] private GameObject onesWildStateVisual;
        private bool onesInvalid = false;
        private bool canInteract = false;
        private int diceLeft;
        private Collider[] canvasColliders;
        private Collider[] fingerColliders;
        public TextMeshProUGUI displayText;
        private String oldMessage;
        private PlayerHandle[] playerHandles;
        private bool toggleFTState;
        [SerializeField] private GameObject FTStateVisual;
        private int rulesIndex = 0;

        private string[] rules = new string[6]
        {
            "On the first turn of a round, the first player will declare how many of a certain dice is present among all players",
            "Each player must raise the bid in some fashion, either the multiplier or the face/pip value",
            "If you think the bid is too ridiculous, you can \"Call it\", this will compare the bid to the actual values among us",
            "If the bid is greater than the actual, the bidder losses a dice, but if the bid is equal to or less than the actual, you lose a dice",
            "If you loose all your dice, you are eliminated",
            "The winner is the last player who still has dice"
        };
    

        #region AudioClips
        [SerializeField] private AudioClip TruthContestSfx;
        [SerializeField] private AudioClip LiarContestSfx;
        [SerializeField] private AudioClip DiceRollSmallSfx;
        [SerializeField] private AudioClip DiceRollLargeSfx;
        [SerializeField] private AudioClip gameEndSfx;
        [SerializeField] private AudioSource speaker;
        private bool audioState = true;
        [SerializeField] private GameObject audioStateVisual;
        
        #endregion

        //from cyan object pool
        public CyanPlayerObjectAssigner objectPool;
        private PooledObject _localPoolObject;
        
        #endregion

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
            toggleFTState = inVR;
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
        
            //AllDeserialization();
        }
        #endregion
        #region settings methods

        public void _ShowRules()
        {
            if (rulesIndex >= rules.Length)
            {
                rulesIndex = 0;
                displayText.text = oldMessage;
            }
            else
            {
                displayText.text = rules[rulesIndex];
                rulesIndex++;
            }
        }
        public void _OnesWild()
        {
            SendCustomNetworkEvent(NetworkEventTarget.All, nameof(OnesWildNetwork));
        }

        public void OnesWildNetwork()
        {
            if (!gameStarted && Networking.LocalPlayer.isMaster)
            {
                onesWild = !onesWild;
                RequestSerialization();
                AllDeserialization();
            }
        }
        
        public void _ToggleInteractMethod()
        {
            Debug.Log("Toggled FT to " + !toggleFTState);
            if (Networking.LocalPlayer.IsUserInVR())
            {
                toggleFTState = !toggleFTState;
                FTStateVisual.SetActive(toggleFTState);
                for (int i = 0; i < fingerColliders.Length; i++)
                {
                    fingerColliders[i].enabled = toggleFTState;
                    if (i < canvasColliders.Length)
                    {
                        canvasColliders[i].enabled = !toggleFTState;
                    }
                }
            }
        }

        public void _ToggleCollidersOff()
        {
            for (int i = 0; i < fingerColliders.Length; i++)
            {
                fingerColliders[i].enabled = false;
                if (i < canvasColliders.Length)
                {
                    canvasColliders[i].enabled = false;
                }
            }
        }

        public void _ToggleCollidersOn()
        {
            bool tempBool = Networking.LocalPlayer.IsUserInVR() ? toggleFTState : false;
            for (int i = 0; i < fingerColliders.Length; i++)
            {
                fingerColliders[i].enabled = false;
                if (i < canvasColliders.Length)
                {
                    canvasColliders[i].enabled = false;
                }
            }
            
        }

        public void _ToggleAudio()
        {
            audioState = !audioState;
            audioStateVisual.SetActive(audioState);
            for (int i = 0; i < playerHandles.Length; i++)
            {
                playerHandles[i]._SetAudioState(audioState);
            }
        }

        public void _EndGame()
        {
            if (gameStarted && canInteract)
            {
                SendCustomNetworkEvent(NetworkEventTarget.All, nameof(EndGameNetwork));
            }
        }

        public void EndGameNetwork()
        {
            if (gameStarted && Networking.IsMaster)
            {
                gameStarted = false;
                for (int i = 0; i < currentPlayers.Length; i++)
                {
                    currentPlayers[i] = -1;
                }

                numJoinedPlayers = 0;
                currentMulti = 1;
                currentDie = -1;
                    
                SendCustomNetworkEvent(NetworkEventTarget.All,nameof(ResetStations));
                playingPlayer = -1;
                RequestSerialization();
                AllDeserialization();
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
                SendCustomNetworkEvent(NetworkEventTarget.All, nameof(DiceRollSound));
                NextTurn();
            }
        }

        public void ReceiveValues(int multi, int die)
        {
            if (Networking.IsMaster)
            {
                playerHandles[playingPlayer]._GlobalClickSound();
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
                Debug.Log("Next player id is " + next + " old one is " + playingPlayer + " numJoinedPlayer is " + numJoinedPlayers);
                
                if (numJoinedPlayers <= 1 || playingPlayer == next)
                {
                    gameStarted = false;
                    lastWinner = currentPlayers[playingPlayer];
                    for (int i = 0; i < currentPlayers.Length; i++)
                    {
                        
                        if (remaining[i] > 0 && currentPlayers[i] != -1)
                        {
                            Debug.Log("Remining " + i + " " + remaining[i]);
                            lastWinner = currentPlayers[i];
                        }
                        currentPlayers[i] = -1;
                    }

                    currentMulti = 1;
                    currentDie = -1;
                    numJoinedPlayers = 0;
                    
                    SendCustomNetworkEvent(NetworkEventTarget.All,nameof(ResetStations));
                    playingPlayer = -1;
                    RequestSerialization();
                    AllDeserialization();
                    SendCustomNetworkEvent(NetworkEventTarget.All, nameof(GameEndSound));
                    return;
                }

                playingPlayer = next;
                RequestSerialization();
                AllDeserialization();
            }
        }

        public void GameEndSound()
        {
            if (audioState)
            {
                speaker.pitch = 1f;
                speaker.clip = gameEndSfx;
                speaker.Play();
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
                    if (dieValues[i] == currentDie || (onesWild && !onesInvalid && dieValues[i] == 0))
                    {
                        sum++;
                        diceMesh[i].material.SetColor("_Color", Color.cyan);
                    }
                    else
                    {
                        diceMesh[i].material.SetColor("_Color", Color.white);
                    }
                    diceMesh[i].material.SetFloat(materialFloatName, dieValues[i]);
                   
                    
                }
                else
                {
                    diceMesh[i].material.SetFloat(materialFloatName, 7);
                }
            }
            VRCPlayerApi player = VRCPlayerApi.GetPlayerById(currentPlayers[lastPlayer]);
            
            String lossPlayer;
            if (currentMulti > sum)
            {
                //ha ha perish
                displayText.text = player.displayName +" is a liar:\n Bid: ";
                if (remaining[lastPlayer] - 1 == 0)
                {
                    lossPlayer = "\n" + player.displayName + " has been eliminated";
                }
                else
                {
                    lossPlayer = "\n" + player.displayName + " losses a dice";
                }

                LiarFoundSound();
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
                displayText.text = player.displayName + " is not a liar:\n Bid: ";
                player = VRCPlayerApi.GetPlayerById(currentPlayers[playingPlayer]);
                if (remaining[playingPlayer] - 1 == 0)
                {
                    lossPlayer = "\n" + player.displayName + " has been eliminated";
                }
                else
                {
                    lossPlayer = "\n" + player.displayName + " losses a dice";
                }
                
                TruthFoundSound();
                if (Networking.IsMaster)
                {
                    remaining[playingPlayer]--;
                    if (remaining[playingPlayer] == 0)
                    {
                        numJoinedPlayers--;
                    }
                }
            }
            displayText.text += currentMulti + " X " + (currentDie+1) + " die\n Actual "+ sum + " X " + (currentDie+1) + " die" + lossPlayer;
           
            diceLeft--; //corrects number of dice left because _turnStart (the data given to the next player) is run before deseralization.

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

        public void DiceRollSound()
        {
            if (audioState && DiceRollSmallSfx != null && DiceRollLargeSfx != null && speaker != null)
            {
                speaker.pitch = Random.Range(0.9f, 1.1f);
                if (diceLeft > 10)
                {
                    speaker.clip = DiceRollLargeSfx;
                    speaker.Play();
                }
                else if(diceLeft > 0)
                {
                    speaker.clip = DiceRollSmallSfx;
                    speaker.Play();
                }
            }
        }

        private void LiarFoundSound()
        {
            if (audioState)
            {
                speaker.pitch = 1f;
                speaker.clip = LiarContestSfx;
                speaker.Play();
            }
        }

        private void TruthFoundSound()
        {
            if (audioState)
            {
                speaker.pitch = 1f;
                speaker.clip = TruthContestSfx;
                speaker.Play();
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
                        RequestSerialization();
                        AllDeserialization();
                        return;
                    }
                }
                

                numJoinedPlayers++;
                currentPlayers[playerNum] = player.playerId;
                playerHandles[playerNum]._GlobalClickSound();

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
                        playerHandles[i]._GlobalClickSound();
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
            if (onesWild)
            {
                if (currentDie == 0)
                {
                    onesInvalid = true;
                }
                else if(currentDie == -1)
                {
                    onesInvalid = false;
                }
            }   
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
                        playerHandles[i]._Turnstart(currentMulti, currentDie, diceLeft, onesWild && !onesInvalid);
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
                displayText.text = "";
                if (playingPlayer != -1)
                {
                    VRCPlayerApi player = VRCPlayerApi.GetPlayerById(currentPlayers[playingPlayer]);
                    displayText.text = player.displayName + "'s turn\n";
                }
                
                displayText.text +=currentMulti.ToString() + " X " + (currentDie+1)  + " die";
                if (onesWild)
                {
                    if (!onesInvalid)
                    {
                        displayText.text += "\nOnes are Wild";
                    }
                    else
                    {
                        displayText.text += "\n<s>Ones are Wild</s>";
                    }
                }
            }
            else if(gameStarted && playingPlayer != -1)
            {
                VRCPlayerApi player = VRCPlayerApi.GetPlayerById(currentPlayers[playingPlayer]);
                displayText.text = player.displayName + "\n starts the round";
                if (onesWild)
                {
                    if (!onesInvalid)
                    {
                        displayText.text += "\nOnes are Wild";
                    }
                    else
                    {
                        displayText.text += "\n<s>Ones are Wild</s>";
                    }
                }
            }

            if (!gameStarted && lastWinner != -1)
            {
                VRCPlayerApi player = VRCPlayerApi.GetPlayerById(lastWinner);
                displayText.text = player.displayName + " is the Winner";
            }

            oldMessage = displayText.text;
        }

        private void UpdateMesh()
        {
            diceLeft = 0;
            onesWildStateVisual.SetActive(onesWild);
            for (int i = 0; i < dieValues.Length; i++)
            {
                if (i < diceMesh.Length && diceMesh[i] != null)
                {
                    int playerNum = i / 5;
                    diceMesh[i].material.SetColor("_Color", Color.white);
                    if (currentPlayers[playerNum] == -1)
                    {
                        diceMesh[i].material.SetFloat(materialFloatName, 8); //logo value in flipbook
                        continue;
                    }
                    
                    if ((i % 5 + 1) <= remaining[playerNum])
                    {
                        if (Networking.LocalPlayer.playerId == currentPlayers[playerNum])
                        {
                            /*if (dieValues[i] == currentDie || (onesWild && !onesInvalid && dieValues[i] == 0))
                            {
                                diceMesh[i].material.SetColor("_Color", Color.cyan);
                            }*/

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