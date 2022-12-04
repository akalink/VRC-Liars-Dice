
using System;
using TMPro;
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;

namespace akaUdon
{
    /* 
     * Summary
     * This script handles all the logic for the individual "station" that player interacts from
     * In simplest terms, this script handles the UI for the player
     * A user will request to access the game by claiming ownership, if the station is available the master assigns that user to the station via set owner and as a variable.
     * A variable is used because an udon object always has an owner but a variable can be null.
     * This object will sync its selections, it will use Deseralization with a switch statement to send events for the master to sync on the dice master.
     */
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class PlayerHandle : UdonSharpBehaviour
    {
        #region Instance Variables

        [UdonSynced()] private int task = 0; //0: do nothing, 1: claim station, 2: leave station, 3: submit dice, 4: contests bid, 5: starts the game, 6: continues the game
        private int stationNum;
        private bool yourTurn = false;
        private VRCPlayerApi owner;
        private LiarsDiceMaster diceMaster;
        [UdonSynced()]private int multiNum = 1;
        private int min = 1;
        private int max = 20;
        private int firstTurnFlag = -1;
        [UdonSynced()]private int dieNumber = 6;
        private int prevDieNumber = 6;
        private bool interactDelay = true;
        
        private Collider canvasCollider;
        private Collider[] fingerColliders;
        private readonly string handTrackerName = "trackhand12345";

        #region UIElements

        [SerializeField]private Button[] numButtons;
        [SerializeField]private Button[] otherButtons;
        [SerializeField] private TextMeshProUGUI numberDisplay;
        [SerializeField] private TextMeshProUGUI playerNameDisplay;
        [SerializeField] private GameObject joinUi;
        [SerializeField] private GameObject leaveUi;
        [SerializeField] private GameObject startUi;
        [SerializeField] private GameObject continueUi;
        [SerializeField] private GameObject preGameUi;
        
        [SerializeField] private GameObject inGameUi;
        [SerializeField] private GameObject wildIconUi;


        #endregion

        #region sfx

        private AudioSource speaker;
        [SerializeField] private AudioClip localSelectionSfx;
        [SerializeField] private AudioClip altSelectionSfx;
        [SerializeField] private AudioClip turnNotificationSfx;
        private bool audioState = true;

        #endregion
        
        [HideInInspector] public TextMeshProUGUI logger;
        [HideInInspector] public bool logging = false;
        #endregion
        
        //handles adding players to game
        //handles game ui   
        //greys out what you can't do

        #region Set Logic
        
        public void _SetAudioState(bool state)
        {
            audioState = state;
        }
        void Start()
        {
            speaker = GetComponentInChildren<AudioSource>();
            
            diceMaster = GetComponentInParent<LiarsDiceMaster>();
            if(diceMaster == null){return;}
            NumberCalc(0);

            logging = diceMaster.logging;
            if (diceMaster.logger != null) { logger = diceMaster.logger; }
            
            Collider[] tempColliders = GetComponentsInChildren<Collider>(true);
            Debug.Log("Temp colliders length = "+tempColliders.Length);
            bool inVR = true;//Networking.LocalPlayer.IsUserInVR();
            fingerColliders = new Collider[tempColliders.Length - 2];
            
            
            int tempIndexFinger = 0;
            foreach (Collider c in tempColliders)
            {
                if (c.gameObject.name.Contains("Canvas"))
                {
                    canvasCollider = c;
                    canvasCollider.enabled = !inVR;
                    Log(canvasCollider.gameObject.name + " found canvas collider");
                }
                else if(c.gameObject.name.Contains(("selector")))
                {
                    fingerColliders[tempIndexFinger] = c;
                    fingerColliders[tempIndexFinger].enabled = inVR;
                    Log(fingerColliders[tempIndexFinger].gameObject.name + " found finger collider");
                    tempIndexFinger++;
                    
                }
            }
        }

        public void _SetCollisionState(bool state)
        {
            if(canvasCollider != null){canvasCollider.enabled = state;}
            if (fingerColliders != null && true)//Networking.LocalPlayer.IsUserInVR())
            {
                foreach (Collider c in fingerColliders)
                {
                    if (c != null)
                    {
                        c.enabled = state;
                    }
                }
            }
        }

        private void SetOnlyCanvasCollision(bool state)
        {
            Log("Canvas should be "+ state +", but selectors should be " + !state);
            if(canvasCollider != null) {canvasCollider.enabled = state;}
            if (fingerColliders != null && true)//Networking.LocalPlayer.IsUserInVR())
            {
                foreach (Collider c in fingerColliders)
                {
                    if (c != null)
                    {
                        c.enabled = !state;
                    }
                }
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other != null && other.gameObject.name.Contains(handTrackerName))
            {
                SetOnlyCanvasCollision(false);
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (other != null && other.gameObject.name.Contains(handTrackerName))
            {
                SetOnlyCanvasCollision(true);
            }
        }

        public void _SetPlayerNameUI()
        {
            if (owner != null)
            {
                playerNameDisplay.text = owner.displayName;
            }
            else
            {
                Log(stationNum + " should be assignable, but its owner variable is null");
            }
        }
        public void _ClearPlayerNameUI()
        {
            playerNameDisplay.text = "";
        }
        public void _SetPlayerNumber(int num)
        {
            stationNum = num;
        }
        public int _GetPlayerNumber()
        {
            return stationNum;
        }
        public void _SetOwner(int id)
        {
            VRCPlayerApi player = VRCPlayerApi.GetPlayerById(id);
            if(owner == player){return;}
            Log("passed in player named "+ player.displayName+ " to station #" +stationNum);
            if (owner == null)
            {
                
                Log("Assigned player named "+ player.displayName+ " to station #" +stationNum);
                owner = player;
                Networking.SetOwner(player, gameObject);
                joinUi.SetActive(false);
                leaveUi.SetActive(true);
            }
            else if (owner != player)
            {
                Log("passed in player named "+ player.displayName+ " to station #" +stationNum + " ,but the station is already assigned");
                joinUi.SetActive(true);
                leaveUi.SetActive(false);
            }
        }
        public void _LeaveSetter()
        {
            if (owner != null) { Log(owner.displayName + " the owner of station #" + stationNum + " is abandoning the station"); }
            owner = null;
            joinUi.SetActive(true);
            leaveUi.SetActive(false);
        }
        #endregion

        #region UI display logic
        

        public void _StartState(bool state)
        {
            startUi.SetActive(state);
        }

        public void _ContinueState(bool state)
        {
            
            continueUi.SetActive(state);
            if (state)
            {
                inGameUi.SetActive(false);
            }
            
        }
        #endregion

        #region turn display logic
        public void _isInGame()
        {
            inGameUi.SetActive(true);
            preGameUi.SetActive(false);
        }

        public void _NotInGame()
        {
            inGameUi.SetActive(false);
            preGameUi.SetActive(true);
        }

        public void _TurnStart(int multi, int die, int remainder, bool onesWild)
        {
            TurnNotificationSound();
            firstTurnFlag = die; //flag that die is -1
            wildIconUi.SetActive(onesWild);
            yourTurn = true;
            min = multi;
            multiNum = multi;
            max = remainder;
            UpdateMultiDisplay();
            dieNumber = die;
            prevDieNumber = die;
            HighLightButton(die);
        }

        public void _NotActive()
        {
            yourTurn = false;
            HighLightButton(6);
        }

        public void _EndGame()
        {
            yourTurn = false;
            HighLightButton(6);
            owner = null;
            _NotInGame();
        }

        private void _LocalClickSound(float f)
        {
            
            if (audioState && localSelectionSfx != null)
            {
                speaker.pitch = f;
                speaker.clip = localSelectionSfx;
                speaker.Play();
            }
        }

        public void _DepressedClickSound()
        {
            _LocalClickSound(0.7f);
        }

        public void _UserDrepressClickSound()
        {
            if (owner == Networking.LocalPlayer && yourTurn)
            {
                _LocalClickSound(0.7f);
            }
        }

        private void _AltClickSound(float f) 
        {
            // todo implement
        }

        public void _DepressedAltClickSound()
        {
            if (owner == Networking.LocalPlayer && yourTurn)
            {
                _AltClickSound(0.7f);
            }
        }
        

        private void TurnNotificationSound()
        {
            if (audioState && Networking.LocalPlayer == owner && turnNotificationSfx != null && speaker != null)
            {
                speaker.pitch = 1f;
                speaker.clip = turnNotificationSfx;
                speaker.Play();
            }
        }
        #endregion

        #region panel visual update methods
        
        private void UpdateMultiDisplay()
        {
            if(numberDisplay != null){numberDisplay.text = multiNum.ToString();}
        }
        
        private void HighLightButton(int num)
        {
            
            ColorBlock block;
            Color c;
            for (int i = 0; i < numButtons.Length; i++)
            {
                block = numButtons[i].colors;
                c = i == num ? Color.cyan : Color.white; // if i and num are equal, assign the color cyan, if not use white
                c = 6 == num ? Color.gray : c; //greys out every number if 6, which means not your turn.
                block.normalColor = c;
                block.selectedColor = c;
                numButtons[i].colors = block;
            }
            
            for (int i = 0; i < otherButtons.Length; i++) // 0 = +, 1 = -, 2 = submit, 3 = call it | this is because I didn't want to make enums
            {
                c = num < 6 ? Color.white : Color.gray; //watch as I use so many control structures
                switch (i) 
                {
                    case 0:
                        if(multiNum >= max){c = Color.gray;}
                        break;
                    case 1:
                        if(multiNum <= min){c = Color.gray;}
                        break;
                    case 2:
                        if (!(multiNum > min || dieNumber > prevDieNumber) || dieNumber==-1)
                        {
                            c = Color.gray;
                        }
                        break;
                    default: 
                        if(firstTurnFlag < 0)
                        {
                            c = Color.gray;}
                        break;
                }
                block = otherButtons[i].colors;
                block.normalColor = c;
                block.selectedColor = c;
                otherButtons[i].colors = block;
            }
        }
        
        private void NumberCalc(int num)
        {
            if (multiNum + num >= min && (multiNum + num <= max))
            {
                multiNum += num;
                if(yourTurn){_LocalClickSound(multiNum/10 + 0.9f);}
                UpdateMultiDisplay();
                HighLightButton(dieNumber);
            } 
        }
        #endregion

        #region buttonInteracts

        public void _InteractionDelay()
        {
            interactDelay = true;
        }
        #region Pre-Game Buttons
        public void _StartGame()
        {
            if ( interactDelay && owner == Networking.LocalPlayer && diceMaster._GetCanInteract() && diceMaster._GetNumJoinedPlayers() > 1)
            {
                //Debug.Log("You have clicked the start button");
                Log("You have clicked the start button");
                if(yourTurn){_LocalClickSound(1f);}
                interactDelay = false;
                SendCustomEventDelayedFrames(nameof(_InteractionDelay), 30);
                _StartState(false);
                task = 5;
                RequestSerialization();
                AllDeserializtaion();
            }
        }
        public void _Join()
        {
            if (interactDelay && owner == null && !diceMaster._GetGameStarted())
            {
                Log("You have requested to join the game on station #" + stationNum);
                Networking.SetOwner(Networking.LocalPlayer, gameObject);
                interactDelay = false;
                SendCustomEventDelayedFrames(nameof(_InteractionDelay), 30);
                _LocalClickSound(1.1f);
                task = 1;
                RequestSerialization();
                AllDeserializtaion();
            }
        }

        public void _Leave()
        {
            if (interactDelay && owner == Networking.LocalPlayer)
            {
                //Debug.Log("You have requested to leave the game");
                Log("You have requested to leave the game");
                interactDelay = false;
                SendCustomEventDelayedFrames(nameof(_InteractionDelay), 30);
                _LocalClickSound(0.9f);
                task = 2;
                RequestSerialization();
                AllDeserializtaion();
            }   
        }

        public void _Rules()
        {
            diceMaster._ShowRules();
            _LocalClickSound(.8f);
        }

        public void _Submit()
        {
            if (interactDelay && owner == Networking.LocalPlayer && yourTurn)
            {
                if (dieNumber < 0) { return;}
                
                if (multiNum > min || dieNumber > prevDieNumber)
                {
                    { //disable button
                        ColorBlock block;
                        Color c;
                        c = Color.gray;
                        block = otherButtons[2].colors;
                        block.normalColor = c;
                        block.selectedColor = c;
                        otherButtons[2].colors = block;
                    } //end disable
                    interactDelay = false;
                    SendCustomEventDelayedFrames(nameof(_InteractionDelay), 30);
                    _LocalClickSound(1.1f);
                    task = 3;
                    RequestSerialization();
                    AllDeserializtaion();
                    
                }
            }
        }
        #endregion

        #region In-Game Buttons
        public void _Contest() //Call it button
        {
            if (interactDelay && owner == Networking.LocalPlayer && yourTurn && firstTurnFlag > -1)
            {
                interactDelay = false;
                SendCustomEventDelayedFrames(nameof(_InteractionDelay), 30);
                _LocalClickSound(0.9f);
                yourTurn = false;
                task = 4;
                RequestSerialization();
                AllDeserializtaion();
            }
        }
        public void _ContinueGame()
        {
            if (interactDelay && owner == Networking.LocalPlayer)
            {
                if(yourTurn){_LocalClickSound(1f);}
                interactDelay = false;
                SendCustomEventDelayedFrames(nameof(_InteractionDelay), 30);
                continueUi.SetActive(false);
                task = 6;
                RequestSerialization();
                AllDeserializtaion();
            }
        }

        // public void _Reset() never used, might user later
        // {
        //     if (owner == Networking.LocalPlayer && yourTurn)
        //     {
        //         multiNum = min;
        //         HighLightButton(prevDieNumber); //I think this is right
        //     }
        // }
    
        
        public void _Add()
        {
            if (owner == Networking.LocalPlayer && yourTurn)
            {
                NumberCalc(1);
            }
        }

        public void _Subtract()
        {
            if (owner == Networking.LocalPlayer && yourTurn)
            {
                NumberCalc(-1);
            }
        }

        #region number buttons

        public void _ChooseOne() {
            if (owner == Networking.LocalPlayer && yourTurn)
            {
                _LocalClickSound(1f);
                dieNumber = 0;
                HighLightButton(0);
            }
        }
        
        public void _ChooseTwo()
        {
            if (owner == Networking.LocalPlayer && yourTurn)
            {
                _LocalClickSound(1f);
                dieNumber = 1;
                HighLightButton(1);
            }
        }
        
        public void _ChooseThree()
        {
            if (owner == Networking.LocalPlayer && yourTurn)
            {
                _LocalClickSound(1f);
                dieNumber = 2;
                HighLightButton(2);
            }
        }
        
        public void _ChooseFour()
        {
            if (owner == Networking.LocalPlayer && yourTurn)
            {
                _LocalClickSound(1f);
                dieNumber = 3;
                HighLightButton(3);
            }
        }
        
        public void _ChooseFive()
        {
            if (owner == Networking.LocalPlayer && yourTurn)
            {
                _LocalClickSound(1f);
                dieNumber = 4;
                HighLightButton(4);
            }
        }
        
        public void _ChooseSix()
        {
            if (owner == Networking.LocalPlayer && yourTurn)
            {
                _LocalClickSound(1f);
                dieNumber = 5;
                HighLightButton(5);
            }
        }
        #endregion
        #endregion
        #endregion

        #region synching

        public override void OnDeserialization()
        {
            AllDeserializtaion();
        }

        private void AllDeserializtaion()
        {
            //Debug.Log("Running a Task, task #"+task);
            string m = "Running a Task, task #" + task + " ";
            
            switch (task)
            {
                case 0 : return;
                
                case 1 : //claim station for yourself
                    m += "claims station";
                    joinUi.SetActive(false);
                    leaveUi.SetActive(true);
                    if(owner == null){diceMaster._AddPlayerToGame(Networking.GetOwner(gameObject), stationNum);}
                    
                    break;
                case 2: //leave station
                    m += "leaves station";
                    joinUi.SetActive(true);
                    leaveUi.SetActive(false);
                    startUi.SetActive(false);
                    if(owner != null){diceMaster._RemovePlayerFromGame(owner);}
                    break;
                case 3: //sends dice values to master
                    m += "submit bid";
                    diceMaster._ReceiveBid(multiNum, dieNumber);
                    break;
                case 4 : //contests bid
                    m += "contest bid";
                    diceMaster._PlayerContests();
                    break;
                case 5 : //starts the game
                    m += "start game";
                    diceMaster._InitializeGame();
                    break;
                case 6: //continues the game
                    m += "continue game";
                    diceMaster._NewRound();
                    break;
            }
            Log(m);
            //task = 0;

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