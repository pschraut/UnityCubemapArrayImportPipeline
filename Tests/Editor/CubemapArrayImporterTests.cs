//
// Cubemap Array Importer for Unity. Copyright (c) 2019 Peter Schraut (www.console-dev.de). See LICENSE.md
// https://github.com/pschraut/UnityCubemapArrayImportPipeline
//
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEditor;
using Oddworm.EditorFramework;

namespace Oddworm.EditorFramework.Tests
{
    class CubemapArrayImporterTests
    {
        /// <summary>
        /// Creates a new Texture2DArray asset and returns the asset path.
        /// </summary>
        string BeginAssetTest()
        {
            var path = AssetDatabase.GenerateUniqueAssetPath("Assets/" + string.Format("Test_CubemapArray.{0}", CubemapArrayImporter.kFileExtension));
            System.IO.File.WriteAllText(path, "");
            AssetDatabase.Refresh();
            return path;
        }

        /// <summary>
        /// Deletes the asset specified by path.
        /// </summary>
        /// <param name="path">The path returned by BeginAssetTest().</param>
        void EndAssetTest(string path)
        {
            AssetDatabase.DeleteAsset(path);
        }

        [Test]
        public void DefaultSettings()
        {
            var path = BeginAssetTest();
            try
            {
                var importer = (CubemapArrayImporter)AssetImporter.GetAtPath(path);

                Assert.AreEqual(1, importer.anisoLevel);
                Assert.AreEqual(FilterMode.Bilinear, importer.filterMode);
                Assert.AreEqual(TextureWrapMode.Repeat, importer.wrapMode);
                Assert.AreEqual(0, importer.cubemaps.Length);
            }
            finally
            {
                EndAssetTest(path);
            }
        }

        [Test]
        public void ScriptingAPI_SetProperties()
        {
            var path = BeginAssetTest();
            try
            {
                var anisoLevel = 10;
                var filterMode = FilterMode.Trilinear;
                var wrapMode = TextureWrapMode.Mirror;

                var importer = (CubemapArrayImporter)AssetImporter.GetAtPath(path);
                importer.anisoLevel = anisoLevel;
                importer.filterMode = filterMode;
                importer.wrapMode = wrapMode;
                EditorUtility.SetDirty(importer);
                importer.SaveAndReimport();

                // Reload importer
                importer = (CubemapArrayImporter)AssetImporter.GetAtPath(path);

                Assert.AreEqual(anisoLevel, importer.anisoLevel);
                Assert.AreEqual(filterMode, importer.filterMode);
                Assert.AreEqual(wrapMode, importer.wrapMode);
                Assert.AreEqual(0, importer.cubemaps.Length);
            }
            finally
            {
                EndAssetTest(path);
            }
        }


        [Test]
        public void ScriptingAPI_AddMemoryCubemap()
        {
            var path = BeginAssetTest();
            try
            {
                System.Exception exception = null;
                var importer = (CubemapArrayImporter)AssetImporter.GetAtPath(path);
                var texture = new Cubemap(64, TextureFormat.RGB24, true);

                try
                {
                    importer.cubemaps = new Cubemap[] { texture };
                }
                catch (System.Exception e)
                {
                    exception = e;
                }
                finally
                {
                    Cubemap.DestroyImmediate(texture);
                }

                Assert.IsTrue(exception is System.NotSupportedException);
            }
            finally
            {
                EndAssetTest(path);
            }
        }


        [Test]
        public void ScriptingAPI_AddNullCubemap()
        {
            var path = BeginAssetTest();
            try
            {
                System.Exception exception = null;
                var importer = (CubemapArrayImporter)AssetImporter.GetAtPath(path);

                try
                {
                    importer.cubemaps = new Cubemap[] { null };
                }
                catch (System.Exception e)
                {
                    exception = e;
                }
                Assert.IsTrue(exception is System.NotSupportedException);
            }
            finally
            {
                EndAssetTest(path);
            }
        }

        [Test]
        public void ScriptingAPI_SetNullArray()
        {
            var path = BeginAssetTest();
            try
            {
                System.Exception exception = null;
                var importer = (CubemapArrayImporter)AssetImporter.GetAtPath(path);

                try
                {
                    importer.cubemaps = null;
                }
                catch (System.Exception e)
                {
                    exception = e;
                }
                Assert.IsTrue(exception is System.NotSupportedException);
            }
            finally
            {
                EndAssetTest(path);
            }
        }

        [Test]
        public void LoadCubemapArray()
        {
            var path = BeginAssetTest();
            try
            {
                var asset = AssetDatabase.LoadMainAssetAtPath(path) as CubemapArray;
                Assert.IsNotNull(asset);
            }
            finally
            {
                EndAssetTest(path);
            }
        }
    }
}
