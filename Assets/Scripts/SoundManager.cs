using UnityEngine;
using UnityEngine.UI;
using LittlePlanet.HybridTerraform;
using LittlePlanet.PlanetSystem;
using LittlePlanet.RuntimeInput;
using LittlePlanet.UI;
using System.Collections;

public class SoundManager : MonoBehaviour
{
    private const string MusicVolumePrefsKey = "SoundManager.MusicVolume";
    private const string SfxVolumePrefsKey = "SoundManager.SfxVolume";

    [Header("Clips")]
    [SerializeField] private AudioClip musicClip;
    [SerializeField] private AudioClip shipNoiseClip;

    [Header("Sources")]
    [SerializeField] private AudioSource musicSource;
    [SerializeField] private AudioSource shipNoiseSource;

    [Header("Flight")]
    [SerializeField] private PlanetFlyTerraformSkill flySkill;
    [SerializeField] private Planet planet;
    [SerializeField, Min(0.05f)] private float flightStateCheckInterval = 0.1f;

    [Header("Volume")]
    [SerializeField, Range(0f, 1f)] private float musicVolume = 0.6f;
    [SerializeField, Range(0f, 1f)] private float shipNoiseVolume = 0.75f;
    [SerializeField] private bool saveVolumeToPlayerPrefs = true;
    [SerializeField] private bool logAudioWarnings;

    [Header("Settings UI")]
    [SerializeField] private string settingsPanelObjectName = "Settings";
    [SerializeField] private string settingsButtonObjectName = "SettingsButton";
    [SerializeField] private string settingsCloseButtonObjectName = "CloseArea";
    [SerializeField] private string musicSliderObjectName = "MusicSlider";
    [SerializeField] private string soundSliderObjectName = "SoundSlider";

    [Header("Loading Screen")]
    [SerializeField] private string loadingScreenObjectName = "LoadingScreen";
    [SerializeField, Min(0f)] private float minimumLoadingScreenSeconds = 0.75f;
    [SerializeField, Min(0.5f)] private float loadingScreenMaxWaitSeconds = 20f;
    [SerializeField] private GameObject loadingScreenObject;
    [SerializeField] private CanvasGroup loadingScreenCanvasGroup;

    private bool _isShipNoiseActive;
    private float _nextFlightStateCheckTime;
    private bool _isSettingsOpen;
    private bool _isUiBound;

    private GameObject _settingsPanelObject;
    private CanvasGroup _settingsCanvasGroup;
    private Button _settingsButton;
    private Button _settingsCloseButton;
    private Slider _musicSlider;
    private Slider _soundSlider;
    private SettingsPanel _settingsPanelController;
    private bool _isStartupReady;

    public bool IsStartupReady => _isStartupReady;

    private void Awake()
    {
        ResolveReferences(createMissingSources: true);
        PrepareSourcesForManualStart();
        LoadVolumes();
        ConfigureSources();

        ResolveUiReferences();
        CacheSettingsState();
        ResolveLoadingScreenReferences();
        ShowLoadingScreen();
    }

    private void OnEnable()
    {
        ResolveUiReferences();
        BindUi();
        SyncSlidersFromState();
    }

    private void OnDisable()
    {
        UnbindUi();
    }

    private IEnumerator Start()
    {
        _isStartupReady = false;
        CloseSettingsPanel();
        yield return RunStartupLoading();
        PlayMusic();
        UpdateShipNoiseState(force: true);
        _isStartupReady = true;
        HideLoadingScreen();
    }

    private void Update()
    {
        HandleSettingsToggleInput();

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

    public void SetMusicVolume(float value)
    {
        musicVolume = Mathf.Clamp01(value);
        if (musicSource != null)
        {
            musicSource.volume = musicVolume;
        }

        SaveVolumes();
    }

    public void SetSoundVolume(float value)
    {
        shipNoiseVolume = Mathf.Clamp01(value);
        if (shipNoiseSource != null)
        {
            shipNoiseSource.volume = shipNoiseVolume;
        }

        SaveVolumes();
    }

    public void SetSfxVolume(float value)
    {
        SetSoundVolume(value);
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

        if (planet == null)
        {
            planet = FindFirstObjectByType<Planet>(FindObjectsInactive.Include);
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
        ConfigureSource(musicSource, musicClip, musicVolume, playOnAwake: false);
        ConfigureSource(shipNoiseSource, shipNoiseClip, shipNoiseVolume, playOnAwake: false);
    }

    private void PrepareSourcesForManualStart()
    {
        if (musicSource != null)
        {
            musicSource.playOnAwake = false;
            if (musicSource.isPlaying)
            {
                musicSource.Stop();
            }
        }

        if (shipNoiseSource != null)
        {
            shipNoiseSource.playOnAwake = false;
            if (shipNoiseSource.isPlaying)
            {
                shipNoiseSource.Stop();
            }
        }
    }

    private void HandleSettingsToggleInput()
    {
        if (_settingsPanelController != null)
        {
            return;
        }

        if (InputCompat.WasKeyPressedThisFrame(KeyCode.Tab))
        {
            ToggleSettingsPanel();
        }
    }

    private void ToggleSettingsPanel()
    {
        ResolveUiReferences();
        if (_settingsPanelController != null)
        {
            _settingsPanelController.Toggle();
            return;
        }

        SetSettingsOpen(!_isSettingsOpen);
    }

    private void SetSettingsOpen(bool isOpen)
    {
        _isSettingsOpen = isOpen;

        if (_settingsPanelObject == null)
        {
            return;
        }

        if (_settingsCanvasGroup == null)
        {
            _settingsCanvasGroup = _settingsPanelObject.GetComponent<CanvasGroup>();
        }

        if (_settingsCanvasGroup == null)
        {
            _settingsPanelObject.SetActive(isOpen);
            return;
        }

        if (!_settingsPanelObject.activeSelf)
        {
            _settingsPanelObject.SetActive(true);
        }

        _settingsCanvasGroup.alpha = isOpen ? 1f : 0f;
        _settingsCanvasGroup.interactable = isOpen;
        _settingsCanvasGroup.blocksRaycasts = isOpen;
    }

    private void CloseSettingsPanel()
    {
        ResolveUiReferences();

        if (_settingsPanelController != null)
        {
            _settingsPanelController.SetWindowOpen(false);
            return;
        }

        SetSettingsOpen(false);
    }

    private void ResolveUiReferences()
    {
        _settingsPanelObject ??= FindObjectByName(settingsPanelObjectName);
        _settingsButton ??= FindComponentByName<Button>(settingsButtonObjectName);
        _musicSlider ??= FindComponentByName<Slider>(musicSliderObjectName);
        _soundSlider ??= FindComponentByName<Slider>(soundSliderObjectName);
        if (_settingsPanelObject != null && _settingsPanelController == null)
        {
            _settingsPanelController = _settingsPanelObject.GetComponent<SettingsPanel>();
        }

        if (_settingsCloseButton == null && _settingsPanelObject != null)
        {
            _settingsCloseButton = FindChildComponentByName<Button>(_settingsPanelObject.transform, settingsCloseButtonObjectName);
        }

        if (_settingsPanelObject != null && _settingsCanvasGroup == null)
        {
            _settingsCanvasGroup = _settingsPanelObject.GetComponent<CanvasGroup>();
            if (_settingsCanvasGroup == null)
            {
                _settingsCanvasGroup = _settingsPanelObject.AddComponent<CanvasGroup>();
            }
        }
    }

    private void ResolveLoadingScreenReferences()
    {
        if (loadingScreenObject == null && !string.IsNullOrWhiteSpace(loadingScreenObjectName))
        {
            loadingScreenObject = GameObject.Find(loadingScreenObjectName);
        }

        if (loadingScreenObject != null && loadingScreenCanvasGroup == null)
        {
            loadingScreenCanvasGroup = loadingScreenObject.GetComponent<CanvasGroup>();
        }
    }

    private void ShowLoadingScreen()
    {
        ResolveLoadingScreenReferences();
        if (loadingScreenObject == null)
        {
            return;
        }

        if (!loadingScreenObject.activeSelf)
        {
            loadingScreenObject.SetActive(true);
        }

        if (loadingScreenCanvasGroup != null)
        {
            loadingScreenCanvasGroup.alpha = 1f;
            loadingScreenCanvasGroup.interactable = true;
            loadingScreenCanvasGroup.blocksRaycasts = true;
        }
    }

    private void HideLoadingScreen()
    {
        ResolveLoadingScreenReferences();
        if (loadingScreenObject == null)
        {
            return;
        }

        if (loadingScreenCanvasGroup != null)
        {
            loadingScreenCanvasGroup.alpha = 0f;
            loadingScreenCanvasGroup.interactable = false;
            loadingScreenCanvasGroup.blocksRaycasts = false;
        }

        loadingScreenObject.SetActive(false);
    }

    private IEnumerator RunStartupLoading()
    {
        ShowLoadingScreen();
        ResolveReferences(createMissingSources: true);

        var startupStartTime = Time.unscaledTime;
        var deadline = startupStartTime + Mathf.Max(0.5f, loadingScreenMaxWaitSeconds);

        yield return null;

        while (!IsPlanetReady() && Time.unscaledTime < deadline)
        {
            yield return null;
        }

        yield return EnsureMusicPrepared(deadline);

        var elapsed = Time.unscaledTime - startupStartTime;
        var remaining = Mathf.Max(0f, minimumLoadingScreenSeconds - elapsed);
        if (remaining > 0f)
        {
            yield return new WaitForSecondsRealtime(remaining);
        }
    }

    private bool IsPlanetReady()
    {
        if (planet == null)
        {
            planet = FindFirstObjectByType<Planet>(FindObjectsInactive.Include);
        }

        if (planet == null)
        {
            return true;
        }

        var tiles = planet.Tiles;
        return tiles != null && tiles.Count > 0;
    }

    private IEnumerator EnsureMusicPrepared(float deadline)
    {
        if (musicClip == null)
        {
            yield break;
        }

        PrepareClip(musicClip);
        while (musicClip.loadState == AudioDataLoadState.Loading && Time.unscaledTime < deadline)
        {
            yield return null;
        }
    }

    private void CacheSettingsState()
    {
        if (_settingsPanelObject == null)
        {
            _isSettingsOpen = false;
            return;
        }

        if (_settingsCanvasGroup == null)
        {
            _isSettingsOpen = _settingsPanelObject.activeSelf;
            return;
        }

        _isSettingsOpen = _settingsCanvasGroup.alpha > 0.5f && _settingsCanvasGroup.interactable;
    }

    private void BindUi()
    {
        if (_isUiBound)
        {
            return;
        }

        if (_settingsButton != null)
        {
            _settingsButton.onClick.RemoveListener(ToggleSettingsPanel);
            if (_settingsPanelController == null)
            {
                _settingsButton.onClick.AddListener(ToggleSettingsPanel);
            }
        }

        if (_settingsCloseButton != null)
        {
            _settingsCloseButton.onClick.RemoveListener(CloseSettingsPanel);
            _settingsCloseButton.onClick.AddListener(CloseSettingsPanel);
        }

        if (_musicSlider != null)
        {
            _musicSlider.onValueChanged.RemoveListener(SetMusicVolume);
            _musicSlider.onValueChanged.AddListener(SetMusicVolume);
        }

        if (_soundSlider != null)
        {
            _soundSlider.onValueChanged.RemoveListener(SetSoundVolume);
            _soundSlider.onValueChanged.AddListener(SetSoundVolume);
        }

        _isUiBound = true;
    }

    private void UnbindUi()
    {
        if (_settingsButton != null)
        {
            _settingsButton.onClick.RemoveListener(ToggleSettingsPanel);
        }

        if (_settingsCloseButton != null)
        {
            _settingsCloseButton.onClick.RemoveListener(CloseSettingsPanel);
        }

        if (_musicSlider != null)
        {
            _musicSlider.onValueChanged.RemoveListener(SetMusicVolume);
        }

        if (_soundSlider != null)
        {
            _soundSlider.onValueChanged.RemoveListener(SetSoundVolume);
        }

        _isUiBound = false;
    }

    private void SyncSlidersFromState()
    {
        if (_musicSlider != null)
        {
            _musicSlider.SetValueWithoutNotify(musicVolume);
        }

        if (_soundSlider != null)
        {
            _soundSlider.SetValueWithoutNotify(shipNoiseVolume);
        }
    }

    private void LoadVolumes()
    {
        if (!saveVolumeToPlayerPrefs)
        {
            return;
        }

        musicVolume = PlayerPrefs.GetFloat(MusicVolumePrefsKey, musicVolume);
        shipNoiseVolume = PlayerPrefs.GetFloat(SfxVolumePrefsKey, shipNoiseVolume);
    }

    private void SaveVolumes()
    {
        if (!saveVolumeToPlayerPrefs)
        {
            return;
        }

        PlayerPrefs.SetFloat(MusicVolumePrefsKey, musicVolume);
        PlayerPrefs.SetFloat(SfxVolumePrefsKey, shipNoiseVolume);
    }

    private static GameObject FindObjectByName(string objectName)
    {
        return string.IsNullOrWhiteSpace(objectName) ? null : GameObject.Find(objectName);
    }

    private static T FindComponentByName<T>(string objectName) where T : Component
    {
        var target = FindObjectByName(objectName);
        return target != null ? target.GetComponent<T>() : null;
    }

    private static T FindChildComponentByName<T>(Transform root, string objectName) where T : Component
    {
        if (root == null || string.IsNullOrWhiteSpace(objectName))
        {
            return null;
        }

        var children = root.GetComponentsInChildren<Transform>(true);
        for (var i = 0; i < children.Length; i++)
        {
            var child = children[i];
            if (child != null && string.Equals(child.name, objectName, System.StringComparison.Ordinal))
            {
                return child.GetComponent<T>();
            }
        }

        return null;
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
