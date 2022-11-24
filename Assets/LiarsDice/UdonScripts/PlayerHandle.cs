
using TMPro;
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
using VRC.Udon;
using VRC.Udon.Common.Interfaces;

namespace akaUdon
{
    /* 
     * Summary
     * This script handles all the logic for the individual "station" that player interacts from
     * In simplest terms, this script handles the UI for the player
     * It runs without any networking, networking is handles by the Liar's dice Master Script.
     * It contains some persistant data, this data is what element it is in the array of itself within the dice master script
     * It will also store which user is assigned to it during a game. This is cleared at game end.
     * It is stateless between each turn, the script will store any information for its next turn. 
     */
    /*
     * TODO naming consistency
     * TODO class name needs a better name
     */
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]


    public class PlayerHandle : UdonSharpBehaviour
    {
        #region Instance Variables

        [UdonSynced()] private int task = 0; //0: do nothing, 1: claim station, 2: leave station, 3: submit dice;
        [UdonSynced()] private int[] synchValues = new int[] {0, 0}; //0: multiplyer, 1: face value
        
        
        private int stationNum;
        private bool yourTurn = false;
        private VRCPlayerApi owner;
        private LiarsDiceMaster diceMaster;
        private int multiNum = 1;
        private int min = 1;
        private int max = 20;
        private int firstTurnFlag = -1;
        private int dieNumber = 6;
        private int prevDieNumber = 6;
        private bool interactDelay = true;

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
        #endregion
        
        //handles adding players to game
        //handles game ui   
        //greys out what you can't do

        public void _SetAudioState(bool state)
        {
            audioState = state;
        }
        void Start()
        {
            speaker = GetComponentInChildren<AudioSource>();
            
            diceMaster = GetComponentInParent<LiarsDiceMaster>();
            NumberCalc(0);
        }

        public void _SetPlayerNameUI()
        {
            Debug.Log("Station " + stationNum + " is given a user");
            if (owner != null)
            {
                playerNameDisplay.text = owner.displayName;
                Debug.Log("Player is assigned");
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
            Debug.Log("passed in player named "+ player.displayName+ " to station #" +stationNum);
            if (owner == null)
            {
                Debug.Log("Assigned player named "+ player.displayName+ " to station #" +stationNum);
                owner = player;
                joinUi.SetActive(false);
                leaveUi.SetActive(true);
            }
            else if (owner != player)
            {
                Debug.Log("passed in player named "+ player.displayName+ " to station #" +stationNum + " but the station is already assigned");
                joinUi.SetActive(true);
                leaveUi.SetActive(false);
            }
        }

        
        //called from LiarsDiceMaster
        public void _LeaveSetter()
        {
            if (owner != null)
            {
                Debug.Log(owner.displayName + "the owner of station #" + stationNum + "is abandoning the station");
            }

            owner = null;
            joinUi.SetActive(true);
            leaveUi.SetActive(false);
        }

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

        #region turn display logic

        public void _Turnstart(int multi, int die, int remainder, bool onesWild)
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

        private void _AltClickSound(float f) // todo implement
        {
            
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
                c = 6 == num ? Color.gray : c; //greys out every number if 6, which means not your turn. Why didn't I just use the my turn bool? that is me asking myself that question
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

        public void _StartGame()
        {
            if ( interactDelay && owner == Networking.LocalPlayer && diceMaster._GetCanInteract() && diceMaster._GetNumJoinedPlayers() > 1)
            {
                Debug.Log("You have clicked the start button");
                if(yourTurn){_LocalClickSound(1f);}
                interactDelay = false;
                SendCustomEventDelayedFrames(nameof(_InteractionDelay), 30);
                _StartState(false);
                //diceMaster._StartGame();
                task = 5;
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
                //diceMaster._ContinueGame();
            }
        }
        public void _Join()
        {
            if (interactDelay && owner == null)
            {
                Debug.Log("You have requested to join the game on station #" + stationNum);
                Networking.SetOwner(Networking.LocalPlayer, gameObject);
                interactDelay = false;
                SendCustomEventDelayedFrames(nameof(_InteractionDelay), 30);
                _LocalClickSound(1.1f);
                /*if (Networking.LocalPlayer.IsOwner(gameObject))
                {*/
                    joinUi.SetActive(false);
                    leaveUi.SetActive(true);

                    task = 1;
                    RequestSerialization();
                    AllDeserializtaion();
                //}
            }
        }

       /* public override void OnOwnershipTransferred(VRCPlayerApi player)
        {
            if(player != Networking.LocalPlayer){return;}
            joinUi.SetActive(false);
            leaveUi.SetActive(true);
                
            task = 1;
            RequestSerialization();
            AllDeserializtaion();
        }*/

        public void _Leave()
        {
            if (interactDelay && owner == Networking.LocalPlayer)
            {
                Debug.Log("You have requested to leave the game");
                interactDelay = false;
                SendCustomEventDelayedFrames(nameof(_InteractionDelay), 30);
                _LocalClickSound(0.9f);
                joinUi.SetActive(true);
                leaveUi.SetActive(false);
                startUi.SetActive(false);
                task = 2;
                RequestSerialization();
                AllDeserializtaion();
            }   
        }

        public void _Rules()
        {
            diceMaster._ShowRules();
        }

        public void _Submit()
        {
            if (interactDelay && owner == Networking.LocalPlayer && yourTurn)
            {
                if (dieNumber < 0) { return;}
                
                if (multiNum > min || dieNumber > prevDieNumber)
                {
                    //disable button
                    ColorBlock block;
                    Color c;
                    c = Color.gray;
                    block = otherButtons[2].colors;
                    block.normalColor = c;
                    block.selectedColor = c;
                    otherButtons[2].colors = block;
                    //end disable
                    interactDelay = false;
                    SendCustomEventDelayedFrames(nameof(_InteractionDelay), 30);
                    _LocalClickSound(1.1f);
                    synchValues[0] = multiNum;
                    synchValues[1] = dieNumber;
                    task = 3;
                    
                    RequestSerialization();
                    AllDeserializtaion();
                    
                }
            }
        }

        public void _Contest() //Call it button
        {
            if (interactDelay && owner == Networking.LocalPlayer && yourTurn && firstTurnFlag > -1)
            {
                interactDelay = false;
                SendCustomEventDelayedFrames(nameof(_InteractionDelay), 30);
                _LocalClickSound(0.9f);
                //diceMaster._PlayerContests();
                yourTurn = false;
                task = 4;
                RequestSerialization();
                AllDeserializtaion();
                ;
            }
        }

        public void _Reset()
        {
            if (owner == Networking.LocalPlayer && yourTurn)
            {
                multiNum = min;
                HighLightButton(prevDieNumber); //I think this is right
            }
        }
    
        
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

        #region synching

        public override void OnDeserialization()
        {
            Debug.Log("Deseralization requested on station #"+stationNum);
            AllDeserializtaion();
        }

        private void AllDeserializtaion()
        {
            Debug.Log("Running a Task, task #"+task);
            switch (task)
            {
                case 0 : return;
                
                case 1 : //claim station for yourself
                    if(owner == null){diceMaster._AddPlayerToGame(Networking.GetOwner(gameObject), stationNum);} //TODO, do ui toggling in here, have it reverse decisions in master script
                    break;
                case 2: //leave station
                    if(owner != null){diceMaster._RemovePlayerFromGame(owner);}
                    break;
                case 3: //sends dice values to master
                    diceMaster._ReceiveBid(synchValues[0], synchValues[1]);
                    break;
                case 4 : //contests bid
                    diceMaster._PlayerContests();
                    break;
                case 5 : //starts the game
                    diceMaster._InitializeGame();
                    break;
                case 6: //continues the game
                    diceMaster._NewRound();
                    break;
            }

            //task = 0;

        }

        #endregion
    }
}