using System;
using System.Linq;
using TMPro;
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
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

        [UdonSynced()] private int[] dieValues = new int[20];
        [UdonSynced()] private int[] remaining = new int[4]; //stretch goal, make these modular, no magic numbers
        private int diceLeft;
        private Renderer[] diceMesh; 
        [SerializeField] private string materialFloatName = "_frame";
        [UdonSynced()] private int[] currentPlayers = new int[4];
        [UdonSynced()] private int numJoinedPlayers = 0;
        [UdonSynced()] private int playingPlayer = -1;
        private int postContestTurnPlayer = -1;
        private bool postContestTurn = false;
        [UdonSynced()] private bool gameStarted = false;
        [UdonSynced()] private int currentMulti = 1;
        [UdonSynced()] private int currentDie = -1;
        [UdonSynced()] private int lastWinner = -1;
        [SerializeField][UdonSynced()] private bool onesWild = true;
        [SerializeField] private GameObject onesWildStateVisual;
        private bool onesInvalid = false;
        private bool canInteract = false;
        

        [SerializeField] private TextMeshProUGUI displayText;
        [SerializeField] private TextMeshProUGUI rulesText;
        private String oldMessage;
        private PlayerHandle[] playerHandles;
        private int rulesIndex = 0;
        [SerializeField] private akaUdon.PositionTracker positionTracker;
        [SerializeField] private ProximityEnable proximityEnable;

        private readonly string[] rules = new string[10]
        {
            "",
            "To begin each round, all players roll their dice simultaneously.",
            "Each player looks at their own dice after they roll, keeping them hidden from the other players.",
            "The first player then states a bid consisting of a face (\"1's\", \"5's\", etc.) and a quantity.",
            "The quantity represents the player's guess as to how many of each face have been rolled by all the players at the table, including themselves. For example, a player might bid \"five 2's.\"",
            "Each subsequent player can either then make a higher bid of the same face (e.g., \"six 2's\"), or they can challenge the previous bid",
            "If the player challenges the previous bid, all players reveal their dice. If the bid is matched or exceeded, the bidder wins. Otherwise the challenger wins.",
            "If the bidder loses, they remove one of their dice from the game.",
            "The loser of the previous round begins the next round.",
            "The winner of the game is the last player to have any dice remaining."
        };
        private readonly string white = "<color=#FFFFFF>";
    

        #region AudioClips
        [SerializeField] private AudioClip TruthContestSfx;
        [SerializeField] private AudioClip LiarContestSfx;
        [SerializeField] private AudioClip DiceRollSmallSfx;
        [SerializeField] private AudioClip DiceRollLargeSfx;
        [SerializeField] private AudioClip gameEndSfx;
        [SerializeField] private AudioSource speaker;
        private bool audioState = true;
        [SerializeField] private GameObject audioStateVisual;
        [SerializeField] private Slider slider;
        
        public TextMeshProUGUI logger;
        public bool logging = false;
        
        #endregion

        #endregion

        #region initialization
        void Start()
        {
            positionTracker.logger = logger;
            positionTracker.logging = logging;
            proximityEnable.logger = logger;
            proximityEnable.logging = logging;

            _ShowRules();
            playerHandles = GetComponentsInChildren<PlayerHandle>(true);

            currentPlayers = new int[playerHandles.Length];
            remaining = new int[playerHandles.Length];
            
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
        #endregion
        #region settings methods

        public void _ShowRules()
        {
            if (rulesIndex >= rules.Length) { rulesIndex = 0;}

            rulesText.text = rules[rulesIndex];
            rulesIndex++;
        }
        public void _OnesWild()
        {
            onesWildStateVisual.SetActive(!onesWild);
            SendCustomNetworkEvent(NetworkEventTarget.All, nameof(OnesWildNetwork));
        }

        public void OnesWildNetwork()
        {
            if (!gameStarted && Networking.LocalPlayer.IsOwner(gameObject))
            {
                onesWild = !onesWild;
                RequestSerialization();
                AllDeserialization();
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

        public void _SetVolume()
        {
            speaker.volume = slider.value;
            foreach (PlayerHandle pH in playerHandles)
            {
                pH._SetVolume(slider.value);
            }
        }

        public void _LeaveGame()
        {
            if (gameStarted && canInteract)
            {
                for (int i = 0; i < currentPlayers.Length; i++)
                {
                    if (currentPlayers[i] == Networking.LocalPlayer.playerId)
                    {
                        Log(Networking.LocalPlayer.displayName + " requested to leave game");
                        playerHandles[i]._RequestToLeaveGame();
                        return;
                    }
                }
            }
        }

        #endregion

        #region game logic

        public bool _GetGameStarted()
        {
            return gameStarted;
        }
        
        public bool _GetCanInteract()
        {
            return canInteract;
        }

        public int _GetNumJoinedPlayers()
        {
            return numJoinedPlayers;
        }

        public void _InitializeGame()
        {
            if (Networking.IsOwner(gameObject))
            {
                Log("Starting new game with " + numJoinedPlayers + " number of players");
                gameStarted = true;
                playingPlayer = currentPlayers.Length-1;
                for (int i = 0; i < remaining.Length; i++)
                {
                    remaining[i] = 5;
                }
                _NewRound();
            }
        }
        public void _NewRound()
        {
            if (Networking.IsOwner(gameObject))
            {
                Randomize();
                SendCustomNetworkEvent(NetworkEventTarget.All, nameof(DiceRollSound));
                if (postContestTurn)
                {
                    NextTurn(postContestTurnPlayer);
                }
                else
                {
                    NextTurn();
                }
            }
        }

        public void _ReceiveBid(int multi, int die)
        {
            if (Networking.IsOwner(gameObject))
            {
                currentMulti = multi;
                currentDie = die;
                NextTurn();
            }
        }

        private void NextTurn()
        {
            NextTurn(playingPlayer);
        }

        private void NextTurn(int lastPlayer)
        {
            
            Log("passed in previous player is index " + lastPlayer);
            if (Networking.IsOwner(gameObject))
            {
                int next = ReturnNextUser(lastPlayer);

                postContestTurn = false;

                if (numJoinedPlayers <= 1 )
                {
                    gameStarted = false;
                    lastWinner = currentPlayers[playingPlayer];
                    for (int i = 0; i < currentPlayers.Length; i++)
                    {
                        
                        if (remaining[i] > 0 && currentPlayers[i] != -1)
                        {
                            
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

        private int ReturnNextUser(int next)
        {
            if (!postContestTurn && remaining[next] > 0)
            {
                do 
                { 
                    next++; 
                    if (next >= currentPlayers.Length) 
                    { 
                        next = 0;
                    }
                } while (currentPlayers[next] == -1 || remaining[next] < 1);
                
                Log("Next player id is " + next + " old one is " + playingPlayer + " numJoinedPlayer is " + numJoinedPlayers);
            }
            return next;
        }

        public void GameEndSound()
        {
            if (audioState && gameEndSfx != null && speaker != null)
            {
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
        
        public void _PlayerContests()
        {
            int lastPlayer = ReturnLastPlayer(playingPlayer);
            
            int sum = CalcSumShowDice();
            
            VRCPlayerApi player = VRCPlayerApi.GetPlayerById(currentPlayers[lastPlayer]);
            
            String lossPlayer;
            postContestTurn = true;
            if (currentMulti > sum)
            {
                displayText.text =_ReturnPlayerColor(player) + white+" is a liar:\n Bid: ";
                lossPlayer = ProcessALosingPlayer(lastPlayer, player);
                LiarFoundSound();
                if (Networking.IsOwner(gameObject))
                {
                    remaining[lastPlayer]--;
                    if (remaining[lastPlayer] == 0)
                    {
                        postContestTurnPlayer = playingPlayer;
                        _PlayerLeft(player, false);
                    }
                    playingPlayer = lastPlayer;
                }
            }
            else
            {
                displayText.text = _ReturnPlayerColor(player) + white+" is not a liar:\n Bid: ";
                player = VRCPlayerApi.GetPlayerById(currentPlayers[playingPlayer]);
                lossPlayer = ProcessALosingPlayer(playingPlayer, player);
                TruthFoundSound();
                if (Networking.IsOwner(gameObject))
                {
                    remaining[playingPlayer]--;
                    if (remaining[playingPlayer] == 0)
                    {
                        _PlayerLeft(player, false);
                        postContestTurnPlayer = lastPlayer;
                    }
                }
            }
            displayText.text += currentMulti + " X " + (currentDie+1) + " die\n Actual "+ sum + " X " + (currentDie+1) + " die" + lossPlayer;
           
            diceLeft--; //corrects number of dice left because _turnStart (the data given to the next player) is run before deseralization.

            if (Networking.IsOwner(gameObject))//Networking.IsMaster)
            {
                currentDie = -1;
                currentMulti = 1;
                playingPlayer = lastPlayer;
                if (remaining[postContestTurnPlayer] <= 0)
                { 
                    player = VRCPlayerApi.GetPlayerById(currentPlayers[postContestTurnPlayer]);
                    Log(player.displayName + " (station index "+postContestTurnPlayer+") should play next");
                }
            }
        }

        private int ReturnLastPlayer(int lastPlayer)
        {
            do
            {
                lastPlayer--;
                if (lastPlayer < 0)
                {
                    lastPlayer = currentPlayers.Length - 1;
                }
            } while (currentPlayers[lastPlayer] == -1);

            return lastPlayer;
        }

        private int CalcSumShowDice()
        {
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
            
            return sum;
        }

        private string ProcessALosingPlayer(int losingPlayer, VRCPlayerApi player)
        {
            string info;
            if (remaining[losingPlayer] - 1 == 0)
            {
                info = "\n" + _ReturnPlayerColor(player) + white+ " has been eliminated";
                if (player == Networking.LocalPlayer)
                {
                    canInteract = false;
                }
            }
            else
            {
                info = "\n" +_ReturnPlayerColor(player) + white+" losses a dice";
            }

            postContestTurnPlayer = losingPlayer;
            return info;
        }

        private void Randomize() 
        {
            for (int i = 0; i < dieValues.Length; i++)
            {
                dieValues[i] = Random.Range(0, 6);
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
            if (audioState && LiarContestSfx != null && speaker != null)
            {
                speaker.clip = LiarContestSfx;
                speaker.Play();
            }
        }

        private void TruthFoundSound()
        {
            if (audioState && TruthContestSfx != null && speaker != null)
            {
                speaker.clip = TruthContestSfx;
                speaker.Play();
            }
        }
        
        #endregion
        
        #region player Involement logic

        public void _AddPlayerToGame(VRCPlayerApi player, int playerNum)
        {
            Log("The player " + player.displayName + " has requested to join the game");
            if (Networking.IsOwner(gameObject) && Utilities.IsValid(player) && !gameStarted)
            {
                
                foreach (int id in currentPlayers) // check if already assigned
                {
                    
                    if (id == player.playerId) // return already if assigned
                    {
                        Log(player.displayName + " is already in the game");
                        RequestSerialization();
                        AllDeserialization();
                        return;
                    }
                    
                }
                
                Log("Adding " + player.displayName + " to the game");
                numJoinedPlayers++;
                currentPlayers[playerNum] = player.playerId;
                RequestSerialization();
                AllDeserialization();
            }
        }
        
        public void _RemovePlayerFromGame(VRCPlayerApi player)
        {
            Log("The player " + player.displayName + " has requested to be removed from the game");
            if (Networking.IsOwner(gameObject) && Utilities.IsValid(player))
            {
                Log("Removing player " + player.displayName);
                for (int i = 0; i < currentPlayers.Length; i++)
                {
                    if (currentPlayers[i] == player.playerId)
                    {
                        Log("The player " + player.displayName + " is being removed from the game");
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

        public void _PlayerLeft(VRCPlayerApi player, bool serialize)
        {
            if (Networking.IsOwner(gameObject) && Utilities.IsValid(player))
            {
                for (int i = 0; i < currentPlayers.Length; i++)
                {
                    if (currentPlayers[i] == player.playerId)
                    {
                        Log("The player " +player.displayName + " was in the game, but left the instance, removing them from the game");
                        currentPlayers[i] = -1;
                        numJoinedPlayers--;
                        if(serialize){
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
                        }

                        break;
                    }
                }
            }
        }

        public override void OnPlayerLeft(VRCPlayerApi player)
        {
            _PlayerLeft(player, true);
        }
        #endregion

        #region manual network update

        public string _ReturnPlayerColor(VRCPlayerApi player)
        {
            return _ReturnPlayerColor(player.displayName);
        }
        
        private string _ReturnPlayerColor(string player)
        {
            float hc = Math.Abs((float)(player.GetHashCode() % 1000) / 1000);
            Color p = Color.HSVToRGB(hc, 1 ,1);
            int r = (int)Math.Floor(Math.Sqrt(p.r*255));
            int g = (int)Math.Sqrt(p.g*255);
            int b = (int)Math.Sqrt(p.b*255);
            string cu = r.ToString("X") + r.ToString("X") + g.ToString("X")+ g.ToString("X")+ b.ToString("X") + b.ToString("X");// + Convert.ToString(r, 16) + Convert.ToString(g, 16) + Convert.ToString(g, 16) + Convert.ToString(b, 16) + Convert.ToString(b, 16);
            Log("Player color is " + cu +  " Normalize Hashcode is: " + hc);
            return "<color=#"+cu +">"+player;
        }

        public override void OnDeserialization()
        {
            AllDeserialization();
        }

        private void AllDeserialization()
        {
            int totalRemaining = 0;
            foreach (int i in remaining)
            {
                totalRemaining += i;
            }

            totalRemaining = totalRemaining - (remaining.Length - numJoinedPlayers)*5;
            Log("Joined amount is " + numJoinedPlayers+", Playing Player is " + playingPlayer + ", Multi is " +currentMulti + ", the die is " + currentDie +", and the remaining dice is " + totalRemaining);
            
            
            onesWildStateVisual.SetActive(onesWild);
            SetOnesWild();
            UpdateUIState();
            UpdateText("Liar's Dice");
            diceLeft = 0;
            UpdateMesh();
        }

        private void SetOnesWild()
        {
            if (onesWild)
            {
                if (currentDie == 0)
                {
                    onesInvalid = true;
                }
                else if(currentDie == -1) //reset validity since -1 means a new turn
                {
                    onesInvalid = false;
                }
            }      
        }

        private void UpdateUIState()
        {
            ///looped section
            bool included = false;
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
                    playerHandles[i]._SetPlayerNameUI();

                    if (currentPlayers[i] == Networking.LocalPlayer.playerId)
                    {
                        included = true;
                    }
                    if(!gameStarted){continue;}
                    
                    playerHandles[i]._isInGame();
                    
                    if (i == playingPlayer)
                    {
                        playerHandles[i]._TurnStart(currentMulti, currentDie, diceLeft, onesWild && !onesInvalid);
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
                    playerHandles[i]._ClearPlayerNameUI();
                }
                if(gameStarted){playerHandles[i]._GameStarted();}
            }
            //end loop
            canInteract = included;
        }

        private void UpdateText(string listPlayers)
        {

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
                    displayText.text = _ReturnPlayerColor(player) + "'s" + white +" turn\n";
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

                displayText.text = _ReturnPlayerColor(player) + "\n "+white+ "starts the round";
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
                displayText.text = _ReturnPlayerColor(player)+white + " is the Winner";
            }

            oldMessage = displayText.text;
        }

        private void UpdateMesh()
        {

            //loop
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
            //end loop
        }
                

        #endregion

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