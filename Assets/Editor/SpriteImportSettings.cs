using UnityEditor;
using UnityEngine;

/// <summary>
/// COLOCAR EN: Assets/Editor/SpriteImportSettings.cs
/// Configura solos los PNG que estén en Assets/Resources/Sprites/ para pixel art:
/// sin compresión, filtro Point, legibles (Read/Write) y sin reescalar a potencia de 2.
/// Si ya habías importado las imágenes antes de agregar este script: clic derecho en la carpeta > Reimport.
/// </summary>
public class SpriteImportSettings : AssetPostprocessor
{
    private void OnPreprocessTexture()
    {
        if (!assetPath.Replace("\\", "/").Contains("/Resources/Sprites/")) return;

        var ti = (TextureImporter)assetImporter;
        ti.textureType = TextureImporterType.Sprite;
        ti.spriteImportMode = SpriteImportMode.Single;
        ti.spritePixelsPerUnit = 100f;
        ti.isReadable = true;
        ti.mipmapEnabled = false;
        ti.alphaIsTransparency = true;
        ti.filterMode = FilterMode.Point;
        ti.wrapMode = TextureWrapMode.Clamp;
        ti.textureCompression = TextureImporterCompression.Uncompressed;
        ti.maxTextureSize = 4096;
    }
}
