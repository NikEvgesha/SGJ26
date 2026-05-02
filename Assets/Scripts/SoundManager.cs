using UnityEngine;
using LittlePlanet.HybridTerraform;

public class SoundManager : MonoBehaviour
{
    [Header("Clips")]
    [SerializeField] private AudioClip musicClip;
    [SerializeField] private AudioClip shipNoiseClip;

    [Header("Sources")]
    [SerializeField] private AudioSource musicSource;
    [SerializeField] private AudioSource shipNoiseSource;

    [Header("Flight")]
    [SerializeField] private PlanetFlyTerraformSkill flySkill;
    [SerializeField, Min(0.05f)] private float flightStateCheckInterval = 0.1f;

    [Header("Volume")]
    [SerializeField, Range(0f, 1f)] private float musicVolume = 0.6f;
    [SerializeField, Range(0f, 1f)] private float shipNoiseVolume = 0.75f;
    [SerializeField] private bool logAudioWarnings;

    private bool _isShipNoiseActive;
    private float _nextFlightStateCheckTime;

    private void Awake()
    {
        ResolveReferences(createMissingSources: true);
        ConfigureSources();
    }

    private void Start()
    {
        PlayMusic();
        UpdateShipNoiseState(force: true);
    }

    private void Update()
    {
        if (Time.unscaledTime < _nextFlightStateCheckTime)
        {
            return;
        }

        _nextFlightStateCheckTime = Time.unscaledTime + flightStateCheckInterval;
        UpdateShipNoiseState(force: false);
    }

    private void OnValidate()
    {
        flightStateCheckInterval = Mathf.Max(0.05f, flightStateCheckInterval);
    }

    [ContextMenu("Setup Sound Sources")]
    private void SetupSoundSources()
    {
        ResolveReferences(createMissingSources: true);
        ConfigureSources();
    }

    private void ResolveReferences(bool createMissingSources)
    {
        if (flySkill == null)
        {
            flySkill = FindFirstObjectByType<PlanetFlyTerraformSkill>(FindObjectsInactive.Include);
        }

        musicSource = EnsureSource(musicSource, sourceIndex: 0, createMissingSources);
        shipNoiseSource = EnsureSource(shipNoiseSource, sourceIndex: 1, createMissingSources);
    }

    private AudioSource EnsureSource(AudioSource source, int sourceIndex, bool createMissing)
    {
        if (source != null)
        {
            return source;
        }

        var sources = GetComponents<AudioSource>();
        if (sourceIndex >= 0 && sourceIndex < sources.Length)
        {
            return sources[sourceIndex];
        }

        return createMissing ? gameObject.AddComponent<AudioSource>() : null;
    }

    private void ConfigureSources()
    {
        ConfigureSource(musicSource, musicClip, musicVolume, playOnAwake: true);
        ConfigureSource(shipNoiseSource, shipNoiseClip, shipNoiseVolume, playOnAwake: false);
    }

    private static void ConfigureSource(AudioSource source, AudioClip clip, float volume, bool playOnAwake)
    {
        if (source == null)
        {
            return;
        }

        source.clip = clip;
        source.loop = true;
        source.playOnAwake = playOnAwake;
        source.volume = volume;
        source.spatialBlend = 0f;
        source.mute = false;
        source.enabled = true;
        source.ignoreListenerPause = true;
    }

    private void PlayMusic()
    {
        if (musicSource == null || musicClip == null)
        {
            LogAudioWarning("Music cannot start: source or clip is missing.");
            return;
        }

        if (musicSource.isPlaying)
        {
            return;
        }

        PrepareClip(musicClip);
        musicSource.clip = musicClip;
        musicSource.Play();
        if (!musicSource.isPlaying)
        {
            LogAudioWarning($"Music source did not start. clip={musicClip.name}, loadState={musicClip.loadState}, enabled={musicSource.enabled}, active={musicSource.gameObject.activeInHierarchy}");
        }
    }

    private void UpdateShipNoiseState(bool force)
    {
        if (flySkill == null)
        {
            ResolveReferences(createMissingSources: false);
        }

        var shouldPlay = flySkill != null && flySkill.IsFlightModeActive;
        if (!force && _isShipNoiseActive == shouldPlay)
        {
            return;
        }

        _isShipNoiseActive = shouldPlay;
        if (shipNoiseSource == null || shipNoiseClip == null)
        {
            if (shouldPlay)
            {
                LogAudioWarning("Ship noise cannot start: source or clip is missing.");
            }

            return;
        }

        if (shouldPlay)
        {
            if (!shipNoiseSource.isPlaying)
            {
                PrepareClip(shipNoiseClip);
                shipNoiseSource.clip = shipNoiseClip;
                shipNoiseSource.Play();
                if (!shipNoiseSource.isPlaying)
                {
                    LogAudioWarning($"Ship noise source did not start. clip={shipNoiseClip.name}, loadState={shipNoiseClip.loadState}, enabled={shipNoiseSource.enabled}, active={shipNoiseSource.gameObject.activeInHierarchy}");
                }
            }
        }
        else if (shipNoiseSource.isPlaying)
        {
            shipNoiseSource.Stop();
        }
    }

    private static void PrepareClip(AudioClip clip)
    {
        if (clip != null && clip.loadState == AudioDataLoadState.Unloaded)
        {
            clip.LoadAudioData();
        }
    }

    private void LogAudioWarning(string message)
    {
        if (logAudioWarnings)
        {
            Debug.LogWarning($"[SoundManager] {message}", this);
        }
    }
}
