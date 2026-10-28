using UnityEngine;
using UnityEngine.InputSystem;

namespace BawalUmihiMaze
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(AudioSource))]
    public class MazeBackgroundMusic : MonoBehaviour
    {
        [Header("Background Music")]
        public AudioClip musicClip;
        [Range(0f, 1f)] public float targetVolume = 0.45f;
        [Min(0f)] public float fadeInSeconds = 1.5f;
        public bool toggleMuteWithMKey = true;

        private AudioSource _source;
        private float _fadeTimer;
        private bool _muted;

        private void Awake()
        {
            _source = GetComponent<AudioSource>();
            _source.loop = true;
            _source.spatialBlend = 0f; // 2D stereo across the entire maze
            _source.playOnAwake = false;

            if (musicClip != null)
                _source.clip = musicClip;
            else if (_source.clip != null)
                musicClip = _source.clip;
        }

        private void Start()
        {
            if (_source.clip == null)
                return;

            _fadeTimer = 0f;
            _source.volume = fadeInSeconds > 0.01f ? 0f : targetVolume;
            _source.Play();
        }

        private void Update()
        {
            if (toggleMuteWithMKey && Keyboard.current != null && Keyboard.current.mKey.wasPressedThisFrame)
            {
                _muted = !_muted;
                _source.mute = _muted;
            }

            if (_source.clip != musicClip && musicClip != null)
            {
                _source.clip = musicClip;
                if (!_source.isPlaying)
                    _source.Play();
            }

            if (!_source.isPlaying)
                return;

            if (fadeInSeconds > 0.01f && _fadeTimer < fadeInSeconds)
            {
                _fadeTimer += Time.deltaTime;
                float t = Mathf.Clamp01(_fadeTimer / fadeInSeconds);
                _source.volume = Mathf.Lerp(0f, targetVolume, t);
            }
            else
            {
                _source.volume = targetVolume;
            }
        }
    }
}
