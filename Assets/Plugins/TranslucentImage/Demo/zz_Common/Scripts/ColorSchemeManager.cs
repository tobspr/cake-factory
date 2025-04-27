using System;
using UnityEngine;
using UnityEngine.UI;

namespace LeTai.Asset.TranslucentImage.Demo
{
public class ColorSchemeManager : MonoBehaviour
{
    public Color lightBackgroudColor = Color.white;
    public Color lightForegroudColor = Color.white;
    public Color lightTextColor      = Color.white;
    public Color darkBackgroudColor  = Color.black;
    public Color darkForegroudColor  = Color.black;
    public Color darkTextColor       = Color.black;

    float        lightSpriteBlending;
    public float darkSpriteBlending = .6f;

    public TranslucentImage[] foregroundGraphic;
    public Text[]             texts;

    Camera cam;

    void Start()
    {
        cam = Camera.main;

        lightSpriteBlending = foregroundGraphic[0].spriteBlending;
    }

    public enum DemoColorScheme
    {
        Light,
        Dark
    }

    public void SetLightScheme(bool on)
    {
        SetColorScheme(on ? DemoColorScheme.Light : DemoColorScheme.Dark);
    }

    public void SetColorScheme(DemoColorScheme scheme)
    {
        Color bg, fg, txt;
        float sb;
        switch (scheme)
        {
        case DemoColorScheme.Light:
            bg  = lightBackgroudColor;
            fg  = lightForegroudColor;
            txt = lightTextColor;
            sb  = lightSpriteBlending;
            break;
        case DemoColorScheme.Dark:
            bg  = darkBackgroudColor;
            fg  = darkForegroudColor;
            txt = darkTextColor;
            sb  = darkSpriteBlending;
            break;
        default:
            throw new ArgumentOutOfRangeException(nameof(scheme), scheme, null);
        }

        cam.backgroundColor = bg;
        foreach (var graphic in foregroundGraphic)
        {
            graphic.color          = fg;
            graphic.spriteBlending = sb;
        }

        foreach (var text in texts)
        {
            text.color = txt;
        }
    }
}
}
