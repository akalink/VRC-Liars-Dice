
using TMPro;
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
using VRC.Udon;
using VRC.Udon.Common.Interfaces;

namespace akaUdon
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class PlayerHandle : UdonSharpBehaviour
    {
         
        
        private int playerNum;
        private bool yourTurn = false;
        private VRCPlayerApi owner;
        private LiarsDiceMaster diceMaster;
        private int multiNum = 1;
        private int min = 1;
        private int max = 20;
        private int firstTurnFlag = -1;
        private int dieNumber = 6;
        private int prevDieNumber = 6;

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


        #endregion

        #region sfx

        private AudioSource speaker;
        [SerializeField] private AudioClip localSelection;
        [SerializeField] private AudioClip globalSelection;
        [SerializeField] private AudioClip turnNotification;

        #endregion
        
        //handles adding players to game
        //handles game ui   
        //greys out what you can't do
        void Start()
        {

            diceMaster = GetComponentInParent<LiarsDiceMaster>();
            NumberCalc(0);
        }

        public void _SetPlayerNameUI()
        {
            if (owner != null) { playerNameDisplay.text = owner.displayName;}
        }

        public void _ClearPlayerNameUI()
        {
            playerNameDisplay.text = "";
        }

        public void _SetPlayerNumber(int num)
        {
            playerNum = num;
        }
        
        public int GetPlayerNumber()
        {
            return playerNum;
        }
        
        public void _SetOwner(int id)
        {
            if (owner == null)
            {
                VRCPlayerApi player = VRCPlayerApi.GetPlayerById(id);
                owner = player;
                joinUi.SetActive(false);
                leaveUi.SetActive(true);
            }
        }

        
        //called from LiarsDiceMaster
        public void _LeaveSetter()
        {
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

        public void _Turnstart(int multi, int die, int remainder)
        {
            Debug.Log("Player Handle Remainder " + remainder);
            firstTurnFlag = die; //flag that die is -1
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
        
        private void NumberCalc(int num) //todo, I should make max equal the total amount of remaining dice
        {
            if (multiNum + num >= min && (multiNum + num <= max))
            {
                multiNum += num;
                UpdateMultiDisplay();
                HighLightButton(dieNumber);
            } 
        }
        #endregion

        #region buttonInteracts

        public void _StartGame()
        {
            if (owner == Networking.LocalPlayer)
            {
                diceMaster._StartGame();
            }
        }
        
        public void _ContinueGame()
        {
            if (owner == Networking.LocalPlayer)
            {
                diceMaster._ContinueGame();
            }
        }
        public void _Join()
        {
            if (owner == null)
            {
                diceMaster._Join(playerNum);
            }
        }
        public void _Leave()
        {
            if (owner == Networking.LocalPlayer)
            {
                
                diceMaster._Leave();
            }   
        }

        public void _Submit()
        {
            if (owner == Networking.LocalPlayer && yourTurn)
            {
                if (dieNumber < 0) { return;}
                
                if (multiNum > min || dieNumber > prevDieNumber)
                {
                    Debug.Log("Fullfilled submit criteria");
                    diceMaster._PlayerSubmitBid(multiNum, dieNumber);
                }
            }
        }

        public void _Contest() //Call it button
        {
            if (owner == Networking.LocalPlayer && yourTurn && firstTurnFlag > -1)
            {
                diceMaster._PlayerContests();
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
                dieNumber = 0;
                HighLightButton(0);
            }
        }
        
        public void _ChooseTwo()
        {
            if (owner == Networking.LocalPlayer && yourTurn)
            {
                dieNumber = 1;
                HighLightButton(1);
            }
        }
        
        public void _ChooseThree()
        {
            if (owner == Networking.LocalPlayer && yourTurn)
            {
                dieNumber = 2;
                HighLightButton(2);
            }
        }
        
        public void _ChooseFour()
        {
            if (owner == Networking.LocalPlayer && yourTurn)
            {
                dieNumber = 3;
                HighLightButton(3);
            }
        }
        
        public void _ChooseFive()
        {
            if (owner == Networking.LocalPlayer && yourTurn)
            {
                dieNumber = 4;
                HighLightButton(4);
            }
        }
        
        public void _ChooseSix()
        {
            if (owner == Networking.LocalPlayer && yourTurn)
            {
                dieNumber = 5;
                HighLightButton(5);
            }
        }
        #endregion
        #endregion
    }
}