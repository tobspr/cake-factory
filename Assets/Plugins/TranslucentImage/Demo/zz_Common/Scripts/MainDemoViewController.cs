using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace LeTai.Asset.TranslucentImage.Demo
{
public class MainDemoViewController : MonoBehaviour
{
    public Toggle toggleLightMode;
    public Toggle toggleDarkMode;

    public Slider sliderBlurStrength;
    public Slider sliderVibrancy;
    public Slider sliderUpdateRate;

    public TranslucentImage[] translucentImages;

    TranslucentImageSource source;

    float   backupBlurStrength;
    float[] backupVibrancy;

    void Start()
    {
        source = Shims.FindObjectOfType<TranslucentImageSource>();
        var colorSchemeManager = GetComponent<ColorSchemeManager>();

        BackupValues();

        toggleLightMode.onValueChanged.AddListener(isOn =>
        {
            if (isOn) colorSchemeManager.SetColorScheme(ColorSchemeManager.DemoColorScheme.Light);
        });
        toggleDarkMode.onValueChanged.AddListener(isOn =>
        {
            if (isOn) colorSchemeManager.SetColorScheme(ColorSchemeManager.DemoColorScheme.Dark);
        });

        sliderBlurStrength.onValueChanged.AddListener(value =>
        {
            source.BlurConfig.Strength = value;
        });
        sliderVibrancy.onValueChanged.AddListener(value =>
        {
            for (int i = 0; i < translucentImages.Length; i++)
            {
                translucentImages[i].materialForRendering.SetFloat(ShaderID.VIBRANCY, value);
            }
        });
        sliderUpdateRate.onValueChanged.AddListener(value =>
        {
            source.MaxUpdateRate = Mathf.Approximately(value, sliderUpdateRate.maxValue) ? float.PositiveInfinity : value;
        });
    }

    void BackupValues()
    {
        backupBlurStrength = source.BlurConfig.Strength;
        backupVibrancy     = new float[translucentImages.Length];
        for (int i = 0; i < translucentImages.Length; i++)
        {
            backupVibrancy[i] = translucentImages[i].materialForRendering.GetFloat(ShaderID.VIBRANCY);
        }
    }

    void OnDestroy()
    {
        source.BlurConfig.Strength = backupBlurStrength;
        for (int i = 0; i < translucentImages.Length; i++)
        {
            translucentImages[i].materialForRendering.SetFloat(ShaderID.VIBRANCY, backupVibrancy[i]);
        }
    }
}
};
