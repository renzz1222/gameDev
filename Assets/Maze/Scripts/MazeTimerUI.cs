using UnityEngine;
using UnityEngine.UI;

namespace BawalUmihiMaze
{
    [DisallowMultipleComponent]
    public class MazeTimerUI : MonoBehaviour
    {
        [SerializeField] private Text timerLabel;
        [SerializeField] private bool runOnStart = true;

        private float _elapsedSeconds;
        private bool _isRunning;
        private int _lastCentiseconds = -1;

        public float ElapsedSeconds => _elapsedSeconds;
        public bool IsRunning => _isRunning;

        private void Awake()
        {
            if (timerLabel == null)
                timerLabel = GetComponentInChildren<Text>();
        }

        private void Start()
        {
            _elapsedSeconds = 0f;
            _isRunning = runOnStart;
            UpdateDisplay(force: true);
        }

        private void Update()
        {
            if (!_isRunning)
                return;

            _elapsedSeconds += Time.deltaTime;
            UpdateDisplay(force: false);
        }

        public void StartTimer()
        {
            _isRunning = true;
        }

        public void StopTimer()
        {
            if (!_isRunning)
                return;

            _isRunning = false;
            UpdateDisplay(force: true);
            if (timerLabel != null)
                timerLabel.color = new Color(0.35f, 1.0f, 0.55f, 1.0f);
        }

        public void ResetTimer(bool autoStart = true)
        {
            _elapsedSeconds = 0f;
            _isRunning = autoStart;
            if (timerLabel != null)
                timerLabel.color = Color.white;
            UpdateDisplay(force: true);
        }

        public void SetLabel(Text label)
        {
            timerLabel = label;
            UpdateDisplay(force: true);
        }

        private void UpdateDisplay(bool force)
        {
            if (timerLabel == null)
                return;

            int totalCentiseconds = Mathf.Max(0, Mathf.FloorToInt(_elapsedSeconds * 100f));
            if (!force && totalCentiseconds == _lastCentiseconds)
                return;

            _lastCentiseconds = totalCentiseconds;
            int centiseconds = totalCentiseconds % 100;
            int totalSeconds = totalCentiseconds / 100;
            int seconds = totalSeconds % 60;
            int minutes = totalSeconds / 60;

            timerLabel.text = $"{minutes:00}:{seconds:00}.{centiseconds:00}";
        }
    }
}
