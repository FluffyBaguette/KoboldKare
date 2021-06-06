﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class ColorExtensions {
    public static Color Invert(this Color rgbColor) {
        float h, s, v;
        Color.RGBToHSV(rgbColor, out h, out s, out v);
        if (v < 0.1f || v > 0.9f) {
            v = Mathf.MoveTowards(v, 1f-v, 0.75f);
        }
        return Color.HSVToRGB((h + 0.5f) % 1, s, v);
    }
}
