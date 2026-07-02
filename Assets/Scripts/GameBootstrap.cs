using UnityEngine;

namespace FlatpackPanic
{
    public class GameBootstrap : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (FindObjectOfType<FlatpackGame>() != null) return;
            new GameObject("Flatpack Panic Runtime Prototype").AddComponent<FlatpackGame>();
        }
    }
}
