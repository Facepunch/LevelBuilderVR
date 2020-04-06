using System.IO;
using LevelBuilder.Geometry;
using Newtonsoft.Json.Linq;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace LevelBuilder.Interface
{
    [ExecuteInEditMode]
    public class LevelSaveLoad : MonoBehaviour
    {
        public Level Target;

        private void Start()
        {
            var saveDir = Path.Combine(Application.dataPath, "Levels");
            if (!Directory.Exists(saveDir)) Directory.CreateDirectory(saveDir);

            var savePath = Path.Combine(saveDir, "Level.json");

#if UNITY_EDITOR
            EditorApplication.playModeStateChanged += stateChange =>
            {
                if (stateChange == PlayModeStateChange.ExitingPlayMode)
                {
                    Debug.Log("Saving level");
                    
                    File.WriteAllText(savePath, Target.Serialize().ToString());
                }
                else if (stateChange == PlayModeStateChange.EnteredPlayMode)
                {
                    Debug.Log("Loading level");

                    if (File.Exists(savePath))
                    {
                        Target.Deserialize(JToken.Parse(File.ReadAllText(savePath)));
                    }
                    else
                    {
                        Target.Clear();

                        var cornerA = Corner.Create(Target, new Vector3(4f, 0f, -5f));
                        var cornerB = Corner.Create(Target, new Vector3(-4f, 0f, -5f));
                        var cornerC = Corner.Create(Target, new Vector3(-4f, 0f, 5f));
                        var cornerD = Corner.Create(Target, new Vector3(4f, 0f, 5f));

                        var room = Room.Create(Target);

                        Wall.Create(room, cornerA, cornerB);
                        Wall.Create(room, cornerB, cornerC);
                        Wall.Create(room, cornerC, cornerD);
                        Wall.Create(room, cornerD, cornerA);

                        room.Invalidate();
                        Target.Refresh();
                    }
                }
            };
#endif
        }
    }
}
