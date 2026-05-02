using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;

namespace LittlePlanet.UI
{
    public sealed class AudioMixerVolumeSettings : MonoBehaviour
    {
        private const float MinVolume = 0.0001f;
        private const float MinDecibels = -80f;

        [Header("Mixer")]
        [SerializeField] private AudioMixer audioMixer;
        [SerializeField] private string masterVolumeParameter = "MasterVolume";
        [SerializeField] private string musicVolumeParameter = "MusicVolume";
        [SerializeField] private string sfxVolumeParameter = "SfxVolume";

        [Header("Sliders")]
        [SerializeField] private Slider masterVolumeSlider;
        [SerializeField] private Slider musicVolumeSlider;
        [SerializeField] private Slider sfxVolumeSlider;

        [Header("Defaults")]
        [SerializeField, Range(0f, 1f)] private float defaultMasterVolume = 1f;
        [SerializeField, Range(0f, 1f)] private float defaultMusicVolume = 0.7f;
        [SerializeField, Range(0f, 1f)] private float defaultSfxVolume = 0.8f;
        [SerializeField] private bool saveToPlayerPrefs = true;

        private string MasterPrefsKey => $"{nameof(AudioMixerVolumeSettings)}.{masterVolumeParameter}";
        private string MusicPrefsKey => $"{nameof(AudioMixerVolumeSettings)}.{musicVolumeParameter}";
        private string SfxPrefsKey => $"{nameof(AudioMixerVolumeSettings)}.{sfxVolumeParameter}";

        private void Awake()
        {
            LoadAndApply();
        }

        private void OnEnable()
        {
            BindSliders();
            SyncSlidersFromSettings();
        }

        private void OnDisable()
        {
            UnbindSliders();
        }

        public void SetMasterVolume(float value)
        {
            SetVolume(masterVolumeParameter, value, MasterPrefsKey);
        }

        public void SetMusicVolume(float value)
        {
            SetVolume(musicVolumeParameter, value, MusicPrefsKey);
        }

        public void SetSfxVolume(float value)
        {
            SetVolume(sfxVolumeParameter, value, SfxPrefsKey);
        }

        public void ResetToDefaults()
        {
            ApplyVolume(masterVolumeParameter, defaultMasterVolume);
            ApplyVolume(musicVolumeParameter, defaultMusicVolume);
            ApplyVolume(sfxVolumeParameter, defaultSfxVolume);

            if (saveToPlayerPrefs)
            {
                PlayerPrefs.DeleteKey(MasterPrefsKey);
                PlayerPrefs.DeleteKey(MusicPrefsKey);
                PlayerPrefs.DeleteKey(SfxPrefsKey);
            }

            SyncSlidersFromSettings();
        }

        private void LoadAndApply()
        {
            ApplyVolume(masterVolumeParameter, LoadVolume(MasterPrefsKey, defaultMasterVolume));
            ApplyVolume(musicVolumeParameter, LoadVolume(MusicPrefsKey, defaultMusicVolume));
            ApplyVolume(sfxVolumeParameter, LoadVolume(SfxPrefsKey, defaultSfxVolume));
        }

        private float LoadVolume(string prefsKey, float defaultValue)
        {
            return saveToPlayerPrefs ? PlayerPrefs.GetFloat(prefsKey, defaultValue) : defaultValue;
        }

        private void SetVolume(string parameterName, float value, string prefsKey)
        {
            var normalizedValue = Mathf.Clamp01(value);
            ApplyVolume(parameterName, normalizedValue);
            if (saveToPlayerPrefs)
            {
                PlayerPrefs.SetFloat(prefsKey, normalizedValue);
            }
        }

        private void ApplyVolume(string parameterName, float normalizedValue)
        {
            if (audioMixer == null || string.IsNullOrWhiteSpace(parameterName))
            {
                return;
            }

            audioMixer.SetFloat(parameterName, NormalizedToDecibels(normalizedValue));
        }

        private void BindSliders()
        {
            BindSlider(masterVolumeSlider, SetMasterVolume);
            BindSlider(musicVolumeSlider, SetMusicVolume);
            BindSlider(sfxVolumeSlider, SetSfxVolume);
        }

        private void UnbindSliders()
        {
            UnbindSlider(masterVolumeSlider, SetMasterVolume);
            UnbindSlider(musicVolumeSlider, SetMusicVolume);
            UnbindSlider(sfxVolumeSlider, SetSfxVolume);
        }

        private static void BindSlider(Slider slider, UnityEngine.Events.UnityAction<float> callback)
        {
            if (slider == null)
            {
                return;
            }

            slider.onValueChanged.RemoveListener(callback);
            slider.onValueChanged.AddListener(callback);
        }

        private static void UnbindSlider(Slider slider, UnityEngine.Events.UnityAction<float> callback)
        {
            if (slider != null)
            {
                slider.onValueChanged.RemoveListener(callback);
            }
        }

        private void SyncSlidersFromSettings()
        {
            SetSliderWithoutNotify(masterVolumeSlider, LoadVolume(MasterPrefsKey, defaultMasterVolume));
            SetSliderWithoutNotify(musicVolumeSlider, LoadVolume(MusicPrefsKey, defaultMusicVolume));
            SetSliderWithoutNotify(sfxVolumeSlider, LoadVolume(SfxPrefsKey, defaultSfxVolume));
        }

        private static void SetSliderWithoutNotify(Slider slider, float value)
        {
            if (slider != null)
            {
                slider.SetValueWithoutNotify(Mathf.Clamp01(value));
            }
        }

        private static float NormalizedToDecibels(float normalizedValue)
        {
            return normalizedValue <= MinVolume ? MinDecibels : Mathf.Log10(normalizedValue) * 20f;
        }
    }
}
