using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class QuestMapSceneSetup : MonoBehaviour
{
    private const string QuestMap1SceneName = "quest_map1";
    private const string MapRootName = "map_root";
    private const string QuestSignName = "panel ?";
    private const string MagicStoneName = "stone";
    private const string BearName = "bear_0";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
        SetupCurrentScene();
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        SetupCurrentScene();
    }

    private static void SetupCurrentScene()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.name != QuestMap1SceneName)
        {
            return;
        }

        GameObject mapRoot = GameObject.Find(MapRootName);
        if (mapRoot == null)
        {
            return;
        }

        QuestPointInteractable existing = mapRoot.GetComponent<QuestPointInteractable>();
        if (existing != null)
        {
            return;
        }

        Transform questSign = mapRoot.transform.Find(QuestSignName);
        Transform magicStone = mapRoot.transform.Find(MagicStoneName);
        Transform bear = mapRoot.transform.Find(BearName);

        QuestPointInteractable questPoint = mapRoot.AddComponent<QuestPointInteractable>();
        questPoint.Initialize(questSign, magicStone, bear);
    }
}
