﻿//
// Cubemap Array Importer for Unity. Copyright (c) 2019-2021 Peter Schraut (www.console-dev.de). See LICENSE.md
// https://github.com/pschraut/UnityCubemapArrayImportPipeline
//
#pragma warning disable IDE1006, IDE0017
using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;

#if UNITY_2020_2_OR_NEWER
using UnityEditor.AssetImporters;
#else
using UnityEditor.Experimental.AssetImporters;
#endif

namespace Oddworm.EditorFramework
{
    [CanEditMultipleObjects]
    [HelpURL("https://docs.unity3d.com/ScriptReference/CubemapArray.html")]
    [ScriptedImporter(k_VersionNumber, kFileExtension)]
    public class CubemapArrayImporter : ScriptedImporter
    {
        [Tooltip("Selects how the Texture behaves when tiled.")]
        [SerializeField]
        TextureWrapMode m_WrapMode = TextureWrapMode.Repeat;

        [Tooltip("Selects how the Texture is filtered when it gets stretched by 3D transformations.")]
        [SerializeField]
        FilterMode m_FilterMode = FilterMode.Bilinear;

        [Tooltip("Increases Texture quality when viewing the texture at a steep angle.\n0 = Disabled for all textures\n1 = Enabled for all textures in Quality Settings\n2..16 = Anisotropic filtering level")]
        [Range(0, 16)]
        [SerializeField]
        int m_AnisoLevel = 1;

        [Tooltip("A list of cubemaps that are added to the CubemapArray asset.")]
        [SerializeField]
        List<Cubemap> m_Cubemaps = new List<Cubemap>();

        public enum VerifyResult
        {
            Valid,
            Null,
            MasterNull,
            WidthMismatch,
            HeightMismatch,
            FormatMismatch,
            MipmapMismatch,
            SRGBTextureMismatch,
            NotAnAsset,
            MasterNotAnAsset,
        }

        /// <summary>
        /// Gets or sets the cubemaps that are being used to create the texture array.
        /// </summary>
        public Cubemap[] cubemaps
        {
            get { return m_Cubemaps.ToArray(); }
            set
            {
                if (value == null)
                    throw new System.NotSupportedException("'cubemaps' must not be set to 'null'. If you want to clear the cubemaps array, set it to a zero-sized array instead.");

                for (var n = 0; n < value.Length; ++n)
                {
                    if (value[n] == null)
                        throw new System.NotSupportedException(string.Format("The cubemap at array index '{0}' must not be 'null'.", n));

                    if (string.IsNullOrEmpty(AssetDatabase.GetAssetPath(value[n])))
                        throw new System.NotSupportedException(string.Format("The cubemap '{1}' at array index '{0}' does not exist on disk. Only cubemap assets can be added.", n, value[n].name));
                }

                m_Cubemaps = new List<Cubemap>(value);
            }
        }

        /// <summary>
        /// Texture coordinate wrapping mode.
        /// </summary>
        public TextureWrapMode wrapMode
        {
            get { return m_WrapMode; }
            set { m_WrapMode = value; }
        }

        /// <summary>
        /// Filtering mode of the texture.
        /// </summary>
        public FilterMode filterMode
        {
            get { return m_FilterMode; }
            set { m_FilterMode = value; }
        }

        /// <summary>
        /// Anisotropic filtering level of the texture.
        /// </summary>
        public int anisoLevel
        {
            get { return m_AnisoLevel; }
            set { m_AnisoLevel = value; }
        }

        /// <summary>
        /// The file extension used for CubemapArray assets without leading dot.
        /// </summary>
        public const string kFileExtension = "cubemaparray";

#if UNITY_2020_1_OR_NEWER
        const int k_VersionNumber = 202010;
#else
        const int k_VersionNumber = 201940;
#endif

        public override void OnImportAsset(AssetImportContext ctx)
        {
            var width = 64;
            var mipmapEnabled = true;
            var textureFormat = TextureFormat.ARGB32;
            var srgbTexture = true;

            // Check if the input textures are valid to be used to build the texture array.
            var isValid = Verify(ctx, false);
            if (isValid)
            {
                // Use the texture assigned to the first slice as "master".
                // This means all other textures have to use same settings as the master texture.
                var sourceTexture = m_Cubemaps[0];
                width = sourceTexture.width;
                textureFormat = sourceTexture.format;

                var sourceTexturePath = AssetDatabase.GetAssetPath(sourceTexture);
                var textureImporter = (TextureImporter)AssetImporter.GetAtPath(sourceTexturePath);
                mipmapEnabled = textureImporter.mipmapEnabled;
                srgbTexture = textureImporter.sRGBTexture;
            }

            // Create the texture array.
            // When the texture array asset is being created, there are no input textures added yet,
            // thus we do Max(1, Count) to make sure to add at least 1 slice.
            var cubemapArray = new CubemapArray(width, Mathf.Max(1, m_Cubemaps.Count), textureFormat, mipmapEnabled, !srgbTexture);
            cubemapArray.wrapMode = m_WrapMode;
            cubemapArray.filterMode = m_FilterMode;
            cubemapArray.anisoLevel = m_AnisoLevel;

            if (isValid)
            {
                // If everything is valid, copy source textures over to the texture array.
                for (int face = 0; face < 6; face++)
                {
                    for (var n = 0; n < m_Cubemaps.Count; ++n)
                    {
                        var source = m_Cubemaps[n];
                        Graphics.CopyTexture(source, face, cubemapArray, face + (n * 6));
                    }
                }
            }
            else
            {
                // If there is any error, copy a magenta colored texture into every slice.
                // I was thinking to only make the invalid slice magenta, but then it's way less obvious that
                // something isn't right with the texture array. Thus I mark the entire texture array as broken.
                var errorTexture = new Cubemap(width, textureFormat, mipmapEnabled);
                try
                {
                    for (var face = 0; face < 6; ++face)
                    {
                        var errorPixels = errorTexture.GetPixels((CubemapFace)face);
                        for (var n = 0; n < errorPixels.Length; ++n)
                            errorPixels[n] = Color.magenta;
                        errorTexture.SetPixels(errorPixels, (CubemapFace)face);
                    }
                    errorTexture.Apply();

                    for (int face = 0; face < 6; face++)
                    {
                        for (var n = 0; n < cubemapArray.cubemapCount; ++n)
                            Graphics.CopyTexture(errorTexture, face, cubemapArray, face + (n * 6));
                    }
                }
                finally
                {
                    DestroyImmediate(errorTexture);
                }
            }

            // Mark all input textures as dependency to the CubemapArray.
            // This causes the CubemapArray to get re-generated when any input texture changes or when the build target changed.
            for (var n = 0; n < m_Cubemaps.Count; ++n)
            {
                var source = m_Cubemaps[n];
                if (source != null)
                {
                    var path = AssetDatabase.GetAssetPath(source);
#if UNITY_2020_1_OR_NEWER
                    ctx.DependsOnArtifact(path);
#else
                    ctx.DependsOnSourceAsset(path);
#endif
                }
            }

#if !UNITY_2020_1_OR_NEWER
            // This value is not really used in this importer,
            // but getting the build target here will add a dependency to the current active buildtarget.
            // Because DependsOnArtifact does not exist in 2019.4, adding this dependency on top of the DependsOnSourceAsset
            // will force a re-import when the target platform changes in case it would have impacted any texture this importer depends on.
            var buildTarget = ctx.selectedBuildTarget;
#endif

            // this should have been named "MainAsset" to be conform with Unity, but changing it now
            // would break all existing CubemapArray assets, so we don't touch it.
            ctx.AddObjectToAsset("CubemapArray", cubemapArray);
            ctx.SetMainObject(cubemapArray);

            if (!isValid)
            {
                // Run the verify step again, but this time we have the main object asset.
                // Console logs should ping the asset, but they don't in 2019.3 beta, bug?
                Verify(ctx, true);
            }
        }

        /// <summary>
        /// Checks if the asset is set up properly and all its dependencies are ok.
        /// </summary>
        /// <returns>
        /// Returns true if the asset can be imported, false otherwise.
        /// </returns>
        bool Verify(AssetImportContext ctx, bool logToConsole)
        {
            if (!SystemInfo.supportsCubemapArrayTextures)
            {
                if (logToConsole)
                    ctx.LogImportError(string.Format("Import failed '{0}'. Your system does not support cubemap arrays.", ctx.assetPath), ctx.mainObject);

                return false;
            }

            if (m_Cubemaps.Count > 0)
            {
                if (m_Cubemaps[0] == null)
                {
                    if (logToConsole)
                        ctx.LogImportError(string.Format("Import failed '{0}'. The first element in the 'Cubemaps' list must not be 'None'.", ctx.assetPath), ctx.mainObject);

                    return false;
                }
            }

            var result = m_Cubemaps.Count > 0;
            for (var n = 0; n < m_Cubemaps.Count; ++n)
            {
                var valid = Verify(n);
                if (valid != VerifyResult.Valid)
                {
                    result = false;
                    if (logToConsole)
                    {
                        var error = GetVerifyString(n);
                        if (!string.IsNullOrEmpty(error))
                        {
                            var msg = string.Format("Import failed '{0}'. {1}", ctx.assetPath, error);
                            ctx.LogImportError(msg, ctx.mainObject);
                        }
                    }
                }
            }

            return result;
        }


        /// <summary>
        /// Verifies the entry in the importer at the specified slice.
        /// </summary>
        /// <param name="importer">The CubemapArrayImporter.</param>
        /// <param name="slice">The texture slice. Must be an index in the 'textures' array.</param>
        /// <returns>Returns the verify result.</returns>
        public VerifyResult Verify(int slice)
        {
            Cubemap master = (m_Cubemaps.Count > 0) ? m_Cubemaps[0] : null;
            Cubemap texture = (slice >= 0 && m_Cubemaps.Count > slice) ? m_Cubemaps[slice] : null;

            if (texture == null)
                return VerifyResult.Null;

            var textureImporter = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(texture)) as TextureImporter;
            if (textureImporter == null)
                return VerifyResult.NotAnAsset;

            if (master == null)
                return VerifyResult.MasterNull;

            var masterImporter = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(master)) as TextureImporter;
            if (masterImporter == null)
                return VerifyResult.MasterNotAnAsset;

            if (texture.width != master.width)
                return VerifyResult.WidthMismatch;

            if (texture.height != master.height)
                return VerifyResult.HeightMismatch;

            if (texture.format != master.format)
                return VerifyResult.FormatMismatch;

            if (texture.mipmapCount != master.mipmapCount)
                return VerifyResult.MipmapMismatch;

            if (textureImporter.sRGBTexture != masterImporter.sRGBTexture)
                return VerifyResult.SRGBTextureMismatch;

            return VerifyResult.Valid;
        }

        /// <summary>
        /// Verifies the entry in the importer at the specified slice.
        /// </summary>
        /// <param name="importer">The CubemapArrayImporter.</param>
        /// <param name="slice">The texture slice. Must be an index in the 'textures' array.</param>
        /// <returns>Returns a human readable string that specifies if anything is wrong, or an empty string if it is ok.</returns>
        public string GetVerifyString(int slice)
        {
            var result = Verify(slice);
            switch (result)
            {
                case VerifyResult.Valid:
                    {
                        return "";
                    }

                case VerifyResult.MasterNull:
                    {
                        return "The cubemap for slice 0 must not be 'None'.";
                    }

                case VerifyResult.Null:
                    {
                        return string.Format("The cubemap for slice {0} must not be 'None'.", slice);
                    }

                case VerifyResult.FormatMismatch:
                    {
                        var master = m_Cubemaps[0];
                        var texture = m_Cubemaps[slice];

                        return string.Format("Cubemap '{0}' uses '{1}' as format, but must be using '{2}' instead, because the cubemap for slice 0 '{3}' is using '{2}' too.",
                            texture.name, texture.format, master.format, master.name);
                    }

                case VerifyResult.MipmapMismatch:
                    {
                        var master = m_Cubemaps[0];
                        var texture = m_Cubemaps[slice];

                        return string.Format("Cubemap '{0}' has '{1}' mipmap(s), but must have '{2}' instead, because the cubemap for slice 0 '{3}' is having '{2}' mipmap(s). Please check if the 'Generate Mip Maps' setting for both cubemaps is the same.",
                            texture.name, texture.mipmapCount, master.mipmapCount, master.name);
                    }

                case VerifyResult.SRGBTextureMismatch:
                    {
                        var master = m_Cubemaps[0];
                        var texture = m_Cubemaps[slice];

                        return string.Format("Cubemap '{0}' uses different 'sRGB' setting than slice 0 cubemap '{1}'.",
                            texture.name, master.name);
                    }

                case VerifyResult.WidthMismatch:
                case VerifyResult.HeightMismatch:
                    {
                        var master = m_Cubemaps[0];
                        var texture = m_Cubemaps[slice];

                        return string.Format("Cubemap '{0}' is {1}x{2} in size, but must be using the same size as the cubemap for slice 0 '{3}', which is {4}x{5}.",
                            texture.name, texture.width, texture.height, master.name, master.width, master.height);
                    }

                case VerifyResult.MasterNotAnAsset:
                case VerifyResult.NotAnAsset:
                    {
                        var texture = m_Cubemaps[slice];

                        return string.Format("Cubemap '{0}' is not saved to disk. Only cubemap assets that exist on disk can be added to a CubemapArray asset.",
                            texture.name);
                    }
            }

            return "Unhandled validation issue.";
        }

        [MenuItem("Assets/Create/Cubemap Array", priority = 315)]
        static void CreateCubemapArrayMenuItem()
        {
            // https://forum.unity.com/threads/how-to-implement-create-new-asset.759662/
            string directoryPath = "Assets";
            foreach (Object obj in Selection.GetFiltered(typeof(Object), SelectionMode.Assets))
            {
                directoryPath = AssetDatabase.GetAssetPath(obj);
                if (!string.IsNullOrEmpty(directoryPath) && File.Exists(directoryPath))
                {
                    directoryPath = Path.GetDirectoryName(directoryPath);
                    break;
                }
            }
            directoryPath = directoryPath.Replace("\\", "/");
            if (directoryPath.Length > 0 && directoryPath[directoryPath.Length - 1] != '/')
                directoryPath += "/";
            if (string.IsNullOrEmpty(directoryPath))
                directoryPath = "Assets/";

            var fileName = string.Format("New CubemapArray.{0}", kFileExtension);
            directoryPath = AssetDatabase.GenerateUniqueAssetPath(directoryPath + fileName);
            ProjectWindowUtil.CreateAssetWithContent(directoryPath, "This file represents a CubemapArray asset for Unity.\nYou need the 'CubemapArray Import Pipeline' package available at https://github.com/pschraut/UnityCubemapArrayImportPipeline to properly import this file in Unity.");
        }
    }
}
