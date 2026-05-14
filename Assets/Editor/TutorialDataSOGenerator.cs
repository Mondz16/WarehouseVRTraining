using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Valari.Data;

public static class TutorialDataSOGenerator
{
    [MenuItem("Tools/Valari/Generate Tutorial Data SOs")]
    public static void Generate()
    {
        const string outputFolder = "Assets/SO/TutorialData";

        string[] jsonFiles =
        {
            "Assets/ReferenceData/training_flow_module1.json",
            "Assets/ReferenceData/training_flow_module2.json",
            "Assets/ReferenceData/training_flow_module3.json",
            "Assets/ReferenceData/training_flow_safety.json"
        };

        FieldInfo challengeListField = typeof(TutorialData).GetField(
            "TutorialChallengeLists",
            BindingFlags.NonPublic | BindingFlags.Instance);

        int created = 0;

        foreach (string jsonPath in jsonFiles)
        {
            string fullPath = Path.GetFullPath(
                Path.Combine(Application.dataPath, "..", jsonPath));

            string json = File.ReadAllText(fullPath);
            var wrapper = JsonUtility.FromJson<TutorialEntryWrapper>("{\"items\":" + json + "}");

            foreach (var entry in wrapper.items)
            {
                var data = ScriptableObject.CreateInstance<TutorialData>();
                data.Title = entry.Title;
                data.Description = entry.Description;
                data.HasChallenges = entry.HasChallenges;
                data.IsInOrder = entry.IsInOrder;

                if (entry.TutorialChallengeLists != null && entry.TutorialChallengeLists.Count > 0)
                {
                    var challenges = new List<TutorialChallengeData>();
                    foreach (var c in entry.TutorialChallengeLists)
                    {
                        var challenge = new TutorialChallengeData
                        {
                            ChallengeTitle = c.ChallengeTitle,
                            ChallengeTriggerName = c.ChallengeTriggerName,
                            IsComplete = c.IsComplete
                        };
                        challenges.Add(challenge);
                    }

                    challengeListField?.SetValue(data, challenges);
                }

                string assetPath = $"{outputFolder}/{entry.name}.asset";
                AssetDatabase.CreateAsset(data, assetPath);
                created++;
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[TutorialDataSOGenerator] Created {created} TutorialData assets in {outputFolder}");
        EditorUtility.DisplayDialog("Done",
            $"Created {created} TutorialData ScriptableObjects in\n{outputFolder}", "OK");
    }
}

[System.Serializable]
internal class TutorialEntryWrapper
{
    public List<TutorialEntryJson> items;
}

[System.Serializable]
internal class TutorialEntryJson
{
    public string Title;
    public string Description;
    public bool HasChallenges;
    public bool IsInOrder;
    public string name;
    public List<TutorialChallengeJson> TutorialChallengeLists;
}

[System.Serializable]
internal class TutorialChallengeJson
{
    public string ChallengeTitle;
    public string ChallengeTriggerName;
    public bool IsComplete;
}
