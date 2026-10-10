using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Animación a partir de sprite sheets horizontales (equivale a SpriteAnimation.kt).
/// Puede unir varios sheets (se usa para el ataque, que viene en 2 archivos).
/// Alinea automáticamente los "pies" del personaje con la base del hitbox, así el
/// padding transparente de cada sheet ya no hace que flote.
/// </summary>
public class SpriteAnim
{
    public readonly Sprite[] Frames;
    private readonly float frameDuration;
    private float timer;
    private int index;

    public Sprite Current => Frames[index];

    public SpriteAnim(float frameDuration, float pixelsPerUnit, params (Texture2D tex, int frames)[] parts)
    {
        this.frameDuration = frameDuration;

        // Pixel más bajo con contenido (en todos los frames de todas las partes)
        int minBottomPad = int.MaxValue;
        foreach (var part in parts)
            minBottomPad = Mathf.Min(minBottomPad, BottomPadding(part.tex, part.frames));
        if (minBottomPad == int.MaxValue) minBottomPad = 0;

        var list = new List<Sprite>();
        foreach (var part in parts)
        {
            int fw = part.tex.width / part.frames;
            int h = part.tex.height;
            var pivot = new Vector2(0.5f, minBottomPad / (float)h);
            for (int f = 0; f < part.frames; f++)
            {
                var rect = new Rect(f * fw, 0, fw, h);
                list.Add(Sprite.Create(part.tex, rect, pivot, pixelsPerUnit, 0, SpriteMeshType.FullRect));
            }
        }
        Frames = list.ToArray();
    }

    public void Update(float dt)
    {
        timer += dt;
        while (timer >= frameDuration)
        {
            timer -= frameDuration;
            index = (index + 1) % Frames.Length;
        }
    }

    public void Reset()
    {
        index = 0;
        timer = 0f;
    }

    // Filas transparentes debajo del contenido más bajo (en píxeles).
    private static int BottomPadding(Texture2D tex, int frames)
    {
        try
        {
            Color32[] px = tex.GetPixels32();
            int w = tex.width, h = tex.height, fw = w / frames;
            int best = h;
            for (int f = 0; f < frames; f++)
            {
                for (int y = 0; y < h && y < best; y++)
                {
                    int row = y * w + f * fw;
                    bool any = false;
                    for (int x = 0; x < fw; x++)
                    {
                        if (px[row + x].a > 10) { any = true; break; }
                    }
                    if (any) { best = y; break; }
                }
            }
            return best == h ? 0 : best;
        }
        catch (UnityException)
        {
            Debug.LogWarning("Textura no legible (activa Read/Write): " + tex.name);
            return 0;
        }
    }

    /// <summary>Rectángulo (en píxeles) que contiene solo la parte no transparente. Equivale a trimTransparentBorder.</summary>
    public static Rect OpaqueBounds(Texture2D tex)
    {
        try
        {
            Color32[] px = tex.GetPixels32();
            int w = tex.width, h = tex.height;
            int minX = w, minY = h, maxX = -1, maxY = -1;
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    if (px[y * w + x].a > 10)
                    {
                        if (x < minX) minX = x;
                        if (x > maxX) maxX = x;
                        if (y < minY) minY = y;
                        if (y > maxY) maxY = y;
                    }
                }
            }
            if (maxX < minX || maxY < minY) return new Rect(0, 0, w, h);
            return new Rect(minX, minY, maxX - minX + 1, maxY - minY + 1);
        }
        catch (UnityException)
        {
            Debug.LogWarning("Textura no legible (activa Read/Write): " + tex.name);
            return new Rect(0, 0, tex.width, tex.height);
        }
    }
}
