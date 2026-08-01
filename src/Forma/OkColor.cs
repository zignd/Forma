// Copyright (c) 2021 Björn Ottosson
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.
//
// Adapted to C# for Forma from ok_color.h as distributed by Godot Engine at
// revision b4fb06cdb3db0c61db40c7b365bfa7adec3cb2ce.

using System;
using Microsoft.Xna.Framework;

namespace Forma
{
    /// <summary>
    /// OKHSL conversion used by Godot's ColorPicker. The math follows the MIT-licensed
    /// ok_color reference implementation bundled in Godot at thirdparty/misc/ok_color.h.
    /// </summary>
    internal static class OkColor
    {
        private readonly struct Lab { public Lab(float l, float a, float b) { L = l; A = a; B = b; } public float L { get; } public float A { get; } public float B { get; } }
        private readonly struct Lc { public Lc(float l, float c) { L = l; C = c; } public float L { get; } public float C { get; } }
        private readonly struct St { public St(float s, float t) { S = s; T = t; } public float S { get; } public float T { get; } }
        private readonly struct Cs { public Cs(float c0, float cmid, float cmax) { C0 = c0; CMid = cmid; CMax = cmax; } public float C0 { get; } public float CMid { get; } public float CMax { get; } }
        private const float Pi = MathF.PI;

        public static Vector3 ToOkHsl(Color color)
        {
            var rgb = new Vector3(color.R / 255f, color.G / 255f, color.B / 255f);
            if (rgb == Vector3.Zero) return Vector3.Zero;
            var lab = LinearSrgbToOkLab(new Vector3(SrgbTransferInverse(rgb.X), SrgbTransferInverse(rgb.Y), SrgbTransferInverse(rgb.Z)));
            var chroma = MathF.Sqrt(lab.A * lab.A + lab.B * lab.B);
            if (chroma <= 0.000001f) return new Vector3(0, 0, MathHelper.Clamp(Toe(lab.L), 0, 1));
            var a = lab.A / chroma; var b = lab.B / chroma;
            var cs = GetCs(lab.L, a, b);
            const float middle = .8f; const float middleInverse = 1.25f;
            float saturation;
            if (chroma < cs.CMid)
            {
                var k1 = middle * cs.C0; var k2 = 1 - k1 / cs.CMid;
                saturation = middle * chroma / (k1 + k2 * chroma);
            }
            else
            {
                var k0 = cs.CMid; var k1 = (1 - middle) * cs.CMid * cs.CMid * middleInverse * middleInverse / cs.C0; var k2 = 1 - k1 / (cs.CMax - cs.CMid);
                saturation = middle + (1 - middle) * (chroma - k0) / (k1 + k2 * (chroma - k0));
            }
            var hue = .5f + .5f * MathF.Atan2(-lab.B, -lab.A) / Pi;
            return new Vector3(MathHelper.Clamp(hue, 0, 1), MathHelper.Clamp(saturation, 0, 1), MathHelper.Clamp(Toe(lab.L), 0, 1));
        }

        public static Vector3 ToLinearSrgb(Color color) => new Vector3(SrgbTransferInverse(color.R / 255f), SrgbTransferInverse(color.G / 255f), SrgbTransferInverse(color.B / 255f));
        public static Color FromLinearSrgb(Vector3 value, float alpha = 1) => new Color(MathHelper.Clamp(SrgbTransfer(value.X), 0, 1), MathHelper.Clamp(SrgbTransfer(value.Y), 0, 1), MathHelper.Clamp(SrgbTransfer(value.Z), 0, 1), MathHelper.Clamp(alpha, 0, 1));

        public static Color FromOkHsl(float hue, float saturation, float lightness, float alpha = 1)
        {
            hue = hue - MathF.Floor(hue); saturation = MathHelper.Clamp(saturation, 0, 1); lightness = MathHelper.Clamp(lightness, 0, 1);
            if (lightness == 0) return new Color(0, 0, 0, MathHelper.Clamp(alpha, 0, 1));
            if (lightness == 1) return new Color(1, 1, 1, MathHelper.Clamp(alpha, 0, 1));
            var a = MathF.Cos(2 * Pi * hue); var b = MathF.Sin(2 * Pi * hue); var l = ToeInverse(lightness);
            var cs = GetCs(l, a, b);
            const float middle = .8f; const float middleInverse = 1.25f;
            float chroma;
            if (saturation < middle)
            {
                var t = middleInverse * saturation; var k1 = middle * cs.C0; var k2 = 1 - k1 / cs.CMid;
                chroma = t * k1 / (1 - k2 * t);
            }
            else
            {
                var t = (saturation - middle) / (1 - middle); var k0 = cs.CMid;
                var k1 = (1 - middle) * cs.CMid * cs.CMid * middleInverse * middleInverse / cs.C0; var k2 = 1 - k1 / (cs.CMax - cs.CMid);
                chroma = k0 + t * k1 / (1 - k2 * t);
            }
            var linear = OkLabToLinearSrgb(new Lab(l, chroma * a, chroma * b));
            return new Color(MathHelper.Clamp(SrgbTransfer(linear.X), 0, 1), MathHelper.Clamp(SrgbTransfer(linear.Y), 0, 1), MathHelper.Clamp(SrgbTransfer(linear.Z), 0, 1), MathHelper.Clamp(alpha, 0, 1));
        }

        private static float SrgbTransfer(float value) => value <= .0031308f ? 12.92f * value : 1.055f * MathF.Pow(value, 1f / 2.4f) - .055f;
        private static float SrgbTransferInverse(float value) => value > .04045f ? MathF.Pow((value + .055f) / 1.055f, 2.4f) : value / 12.92f;
        private static Lab LinearSrgbToOkLab(Vector3 c)
        {
            var l = .4122214708f * c.X + .5363325363f * c.Y + .0514459929f * c.Z;
            var m = .2119034982f * c.X + .6806995451f * c.Y + .1073969566f * c.Z;
            var s = .0883024619f * c.X + .2817188376f * c.Y + .6299787005f * c.Z;
            var lr = MathF.Cbrt(l); var mr = MathF.Cbrt(m); var sr = MathF.Cbrt(s);
            return new Lab(.2104542553f * lr + .7936177850f * mr - .0040720468f * sr, 1.9779984951f * lr - 2.4285922050f * mr + .4505937099f * sr, .0259040371f * lr + .7827717662f * mr - .8086757660f * sr);
        }
        private static Vector3 OkLabToLinearSrgb(Lab c)
        {
            var lr = c.L + .3963377774f * c.A + .2158037573f * c.B; var mr = c.L - .1055613458f * c.A - .0638541728f * c.B; var sr = c.L - .0894841775f * c.A - 1.2914855480f * c.B;
            var l = lr * lr * lr; var m = mr * mr * mr; var s = sr * sr * sr;
            return new Vector3(4.0767416621f * l - 3.3077115913f * m + .2309699292f * s, -1.2684380046f * l + 2.6097574011f * m - .3413193965f * s, -.0041960863f * l - .7034186147f * m + 1.7076147010f * s);
        }
        private static float ComputeMaxSaturation(float a, float b)
        {
            float k0, k1, k2, k3, k4, wl, wm, ws;
            if (-1.88170328f * a - .80936493f * b > 1) { k0 = 1.19086277f; k1 = 1.76576728f; k2 = .59662641f; k3 = .75515197f; k4 = .56771245f; wl = 4.0767416621f; wm = -3.3077115913f; ws = .2309699292f; }
            else if (1.81444104f * a - 1.19445276f * b > 1) { k0 = .73956515f; k1 = -.45954404f; k2 = .08285427f; k3 = .12541070f; k4 = .14503204f; wl = -1.2684380046f; wm = 2.6097574011f; ws = -.3413193965f; }
            else { k0 = 1.35733652f; k1 = -.00915799f; k2 = -1.15130210f; k3 = -.50559606f; k4 = .00692167f; wl = -.0041960863f; wm = -.7034186147f; ws = 1.7076147010f; }
            var saturation = k0 + k1 * a + k2 * b + k3 * a * a + k4 * a * b;
            var kl = .3963377774f * a + .2158037573f * b; var km = -.1055613458f * a - .0638541728f * b; var ks = -.0894841775f * a - 1.2914855480f * b;
            var lr = 1 + saturation * kl; var mr = 1 + saturation * km; var sr = 1 + saturation * ks;
            var l = lr * lr * lr; var m = mr * mr * mr; var s = sr * sr * sr;
            var ld = 3 * kl * lr * lr; var md = 3 * km * mr * mr; var sd = 3 * ks * sr * sr;
            var ld2 = 6 * kl * kl * lr; var md2 = 6 * km * km * mr; var sd2 = 6 * ks * ks * sr;
            var f = wl * l + wm * m + ws * s; var f1 = wl * ld + wm * md + ws * sd; var f2 = wl * ld2 + wm * md2 + ws * sd2;
            return saturation - f * f1 / (f1 * f1 - .5f * f * f2);
        }
        private static Lc FindCusp(float a, float b)
        {
            var saturation = ComputeMaxSaturation(a, b); var rgb = OkLabToLinearSrgb(new Lab(1, saturation * a, saturation * b));
            var lightness = MathF.Cbrt(1 / MathF.Max(rgb.X, MathF.Max(rgb.Y, rgb.Z)));
            return new Lc(lightness, lightness * saturation);
        }
        private static float FindGamutIntersection(float a, float b, float lightness, float chroma, float origin, Lc cusp)
        {
            float t;
            if ((lightness - origin) * cusp.C - (cusp.L - origin) * chroma <= 0) return cusp.C * origin / (chroma * cusp.L + cusp.C * (origin - lightness));
            t = cusp.C * (origin - 1) / (chroma * (cusp.L - 1) + cusp.C * (origin - lightness));
            var dl = lightness - origin; var dc = chroma;
            var kl = .3963377774f * a + .2158037573f * b; var km = -.1055613458f * a - .0638541728f * b; var ks = -.0894841775f * a - 1.2914855480f * b;
            var ldt = dl + dc * kl; var mdt = dl + dc * km; var sdt = dl + dc * ks;
            var currentL = origin * (1 - t) + t * lightness; var currentC = t * chroma;
            var lr = currentL + currentC * kl; var mr = currentL + currentC * km; var sr = currentL + currentC * ks;
            var l = lr * lr * lr; var m = mr * mr * mr; var s = sr * sr * sr;
            var ld = 3 * ldt * lr * lr; var md = 3 * mdt * mr * mr; var sd = 3 * sdt * sr * sr;
            var ld2 = 6 * ldt * ldt * lr; var md2 = 6 * mdt * mdt * mr; var sd2 = 6 * sdt * sdt * sr;
            var r = 4.0767416621f * l - 3.3077115913f * m + .2309699292f * s - 1; var r1 = 4.0767416621f * ld - 3.3077115913f * md + .2309699292f * sd; var r2 = 4.0767416621f * ld2 - 3.3077115913f * md2 + .2309699292f * sd2;
            var g = -1.2684380046f * l + 2.6097574011f * m - .3413193965f * s - 1; var g1 = -1.2684380046f * ld + 2.6097574011f * md - .3413193965f * sd; var g2 = -1.2684380046f * ld2 + 2.6097574011f * md2 - .3413193965f * sd2;
            var blue = -.0041960863f * l - .7034186147f * m + 1.7076147010f * s - 1; var blue1 = -.0041960863f * ld - .7034186147f * md + 1.7076147010f * sd; var blue2 = -.0041960863f * ld2 - .7034186147f * md2 + 1.7076147010f * sd2;
            var tr = -r * r1 / (r1 * r1 - .5f * r * r2); var tg = -g * g1 / (g1 * g1 - .5f * g * g2); var tb = -blue * blue1 / (blue1 * blue1 - .5f * blue * blue2);
            if (r1 / (r1 * r1 - .5f * r * r2) < 0) tr = float.MaxValue;
            if (g1 / (g1 * g1 - .5f * g * g2) < 0) tg = float.MaxValue;
            if (blue1 / (blue1 * blue1 - .5f * blue * blue2) < 0) tb = float.MaxValue;
            return t + MathF.Min(tr, MathF.Min(tg, tb));
        }
        private static float Toe(float value) { const float k1 = .206f, k2 = .03f, k3 = (1 + k1) / (1 + k2); return .5f * (k3 * value - k1 + MathF.Sqrt((k3 * value - k1) * (k3 * value - k1) + 4 * k2 * k3 * value)); }
        private static float ToeInverse(float value) { const float k1 = .206f, k2 = .03f, k3 = (1 + k1) / (1 + k2); return (value * value + k1 * value) / (k3 * (value + k2)); }
        private static St ToSt(Lc cusp) => new St(cusp.C / cusp.L, cusp.C / (1 - cusp.L));
        private static St GetStMid(float a, float b)
        {
            var s = .11516993f + 1 / (7.44778970f + 4.15901240f * b + a * (-2.19557347f + 1.75198401f * b + a * (-2.13704948f - 10.02301043f * b + a * (-4.24894561f + 5.38770819f * b + 4.69891013f * a))));
            var t = .11239642f + 1 / (1.61320320f - .68124379f * b + a * (.40370612f + .90148123f * b + a * (-.27087943f + .61223990f * b + a * (.00299215f - .45399568f * b - .14661872f * a))));
            return new St(s, t);
        }
        private static Cs GetCs(float lightness, float a, float b)
        {
            var cusp = FindCusp(a, b); var cmax = FindGamutIntersection(a, b, lightness, 1, lightness, cusp); var maximum = ToSt(cusp);
            var scale = cmax / MathF.Min(lightness * maximum.S, (1 - lightness) * maximum.T); var mid = GetStMid(a, b);
            var ca = lightness * mid.S; var cb = (1 - lightness) * mid.T;
            var cmid = .9f * scale * MathF.Sqrt(MathF.Sqrt(1 / (1 / MathF.Pow(ca, 4) + 1 / MathF.Pow(cb, 4))));
            ca = lightness * .4f; cb = (1 - lightness) * .8f;
            var c0 = MathF.Sqrt(1 / (1 / (ca * ca) + 1 / (cb * cb)));
            return new Cs(c0, cmid, cmax);
        }
    }
}
