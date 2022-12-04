
using TMPro;
using UnityEditor;
using UnityEngine;
using VRC.Udon;

namespace akaUdon
{
    public class GenerateDiceTable : EditorWindow
    {


        [MenuItem("Window/Liar's Dice Table/Generate Only Table")]
        public static void GenerateOnlyDiceTable()
        {
            Debug.Log("The Generate Only Table Button was chosen");
            object tablePrefab =
                AssetDatabase.LoadAssetAtPath("Assets/LiarsDice/Liars Dice Table.prefab", typeof(GameObject));

            PrefabUtility.InstantiatePrefab(tablePrefab as GameObject);
            
        }

        [MenuItem("Window/Liar's Dice Table/Generate Table with Logger")]
        public static void GenerateDiceTableWithLogger()
        {
            Debug.Log("The Generate Table with Logger Button was chosen");
            object tablePrefab =
                AssetDatabase.LoadAssetAtPath("Assets/LiarsDice/Liars Dice Table.prefab", typeof(GameObject));
            object loggerPrefab =
                AssetDatabase.LoadAssetAtPath("Assets/LiarsDice/Logger.prefab", typeof(GameObject));

            var tv =PrefabUtility.InstantiatePrefab(tablePrefab as GameObject);
            GameObject tg = (GameObject) tv;

            var lv = PrefabUtility.InstantiatePrefab(loggerPrefab as GameObject);
            GameObject lg = (GameObject) lv;
            TextMeshProUGUI tmp = lg.GetComponentInChildren<TextMeshProUGUI>();

            akaUdon.LiarsDiceMaster ldm = tg.GetComponentInChildren<akaUdon.LiarsDiceMaster>();
            ldm.logger = tmp;
            ldm.logging = true;
            
        }
    }
}
