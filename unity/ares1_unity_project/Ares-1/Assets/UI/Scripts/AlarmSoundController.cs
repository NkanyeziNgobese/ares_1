using UnityEngine;

[DisallowMultipleComponent]
public class AlarmSoundController : MonoBehaviour
{
    [SerializeField] private TelemetryAlarmApplier alarmApplier;
    [SerializeField] private AudioSource audioSource;

    [Header("Behavior")]
    [SerializeField] private bool playOnlyOnDanger = true;
    [SerializeField] private bool includeWarning = false;
    [SerializeField] private float startDelaySeconds = 0.0f;
    [SerializeField] private float stopDelaySeconds = 0.5f;
    [SerializeField] private KeyCode muteToggleKey = KeyCode.F11;
    [SerializeField] private bool muted = false;

    private float _startTimer;
    private float _stopTimer;

    private void Reset()
    {
        if (!alarmApplier) alarmApplier = FindFirstObjectByType<TelemetryAlarmApplier>();
        if (!audioSource) audioSource = GetComponent<AudioSource>();
    }

    private void OnValidate()
    {
        if (!alarmApplier) alarmApplier = FindFirstObjectByType<TelemetryAlarmApplier>();
        if (!audioSource) audioSource = GetComponent<AudioSource>();
        if (startDelaySeconds < 0f) startDelaySeconds = 0f;
        if (stopDelaySeconds < 0f) stopDelaySeconds = 0f;
    }

    private void Update()
    {
        if (Input.GetKeyDown(muteToggleKey))
            muted = !muted;

        if (!alarmApplier || !audioSource) return;

        bool shouldPlay = ShouldPlay();

        if (shouldPlay)
        {
            _stopTimer = 0f;
            if (!audioSource.isPlaying)
            {
                _startTimer += Time.unscaledDeltaTime;
                if (_startTimer >= startDelaySeconds)
                {
                    audioSource.Play();
                    _startTimer = 0f;
                }
            }
            else
            {
                _startTimer = 0f;
            }
        }
        else
        {
            _startTimer = 0f;
            if (audioSource.isPlaying)
            {
                _stopTimer += Time.unscaledDeltaTime;
                if (_stopTimer >= stopDelaySeconds)
                {
                    audioSource.Stop();
                    _stopTimer = 0f;
                }
            }
            else
            {
                _stopTimer = 0f;
            }
        }
    }

    private bool ShouldPlay()
    {
        if (muted) return false;

        if (playOnlyOnDanger)
            return alarmApplier.HasDangerActive;

        if (includeWarning)
            return alarmApplier.HasWarningActive || alarmApplier.HasDangerActive;

        return alarmApplier.HasDangerActive;
    }
}
