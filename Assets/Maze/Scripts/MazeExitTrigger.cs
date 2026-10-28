using UnityEngine;

namespace BawalUmihiMaze
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider))]
    public class MazeExitTrigger : MonoBehaviour
    {
        private void Reset()
        {
            BoxCollider box = GetComponent<BoxCollider>();
            box.isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.GetComponent<PlayerCubeController>() == null && !other.CompareTag("Player"))
                return;

            MazeTimerUI timer = Object.FindFirstObjectByType<MazeTimerUI>();
            if (timer != null)
                timer.StopTimer();
        }
    }
}
