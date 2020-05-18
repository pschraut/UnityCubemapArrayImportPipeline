//
// Cubemap Array Importer for Unity. Copyright (c) 2019-2020 Peter Schraut (www.console-dev.de). See LICENSE.md
// https://github.com/pschraut/UnityCubemapArrayImportPipeline
//

#if UNITY_2020_1_OR_NEWER
// Unity 2020.1 and newer has a built-in Cubemap preview inspector
#else
// Unity 2019.3 does not have a CubemapArray preview in the inspector.
// I implemented a custom preview instead, which you can find in this file.
// I use this define to toggle the custom preview, because newer Unity
// versions have built-in support and the built-in CubemapArray preview is
// way better than what I implemented.
#define USE_CUSTOM_PREVIEW
#endif

#pragma warning disable IDE1006, IDE0017
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.Experimental.AssetImporters;
using UnityEditorInternal;

namespace Oddworm.EditorFramework
{
    [CanEditMultipleObjects]
    [CustomEditor(typeof(CubemapArrayImporter), true)]
    class CubemapArrayImporterInspector : ScriptedImporterEditor
    {
        class Styles
        {
            public readonly GUIStyle preButton = "RL FooterButton";
            public readonly Texture2D popupIcon = EditorGUIUtility.FindTexture("_Popup");
            public readonly Texture2D errorIcon = EditorGUIUtility.FindTexture("console.erroricon.sml");
            public readonly Texture2D warningIcon = EditorGUIUtility.FindTexture("console.warnicon.sml");
            public readonly GUIContent textureTypeLabel = new GUIContent("Texture Type");
            public readonly GUIContent textureTypeValue = new GUIContent("Cubemap Array");
            public readonly GUIContent textureShapeLabel = new GUIContent("Texture Shape");
            public readonly GUIContent textureShapeValue = new GUIContent("Cube");
            public readonly GUIContent wrapModeLabel = new GUIContent("Wrap Mode", "Select how the Texture behaves when tiled.");
            public readonly GUIContent filterModeLabel = new GUIContent("Filter Mode", "Select how the Texture is filtered when it gets stretched by 3D transformations.");
            public readonly GUIContent anisoLevelLabel = new GUIContent("Aniso Level", "Increases Texture quality when viewing the Texture at a steep angle. Good for floor and ground Textures.");
            public readonly GUIContent anisotropicFilteringDisable = new GUIContent("Anisotropic filtering is disabled for all textures in Quality Settings.");
            public readonly GUIContent anisotropicFilteringForceEnable = new GUIContent("Anisotropic filtering is enabled for all textures in Quality Settings.");
            public readonly GUIContent texturesHeaderLabel = new GUIContent("Cubemaps", "Drag&drop one or multiple textures here to add them to the list.");
            public readonly GUIContent removeItemButton = new GUIContent("", EditorGUIUtility.FindTexture("Toolbar Minus"), "Remove from list.");

#if USE_CUSTOM_PREVIEW
            public readonly GUIContent prevSliceIcon = EditorGUIUtility.TrIconContent("Animation.PrevKey", "Go to previous slice in the array.");
            public readonly GUIContent nextSliceIcon = EditorGUIUtility.TrIconContent("Animation.NextKey", "Go to next slice in the array.");
            public readonly GUIStyle stepSlice = "TimeScrubberButton";
            public readonly GUIStyle sliceScrubber = "TimeScrubber";
#endif
        }

        static Styles s_Styles;
        Styles styles
        {
            get
            {
                s_Styles = s_Styles ?? new Styles();
                return s_Styles;
            }
        }

        SerializedProperty m_WrapMode = null;
        SerializedProperty m_FilterMode = null;
        SerializedProperty m_AnisoLevel = null;
        SerializedProperty m_Cubemaps = null;
        ReorderableList m_TextureList = null;

#if USE_CUSTOM_PREVIEW
        Cubemap m_PreviewCubemap = null;
        Editor m_PreviewEditor = null;
        int m_PreviewSlice = 0;
        bool m_PreviewDirty = true;
#endif

        public override void OnEnable()
        {
            base.OnEnable();

            m_WrapMode = serializedObject.FindProperty("m_WrapMode");
            m_FilterMode = serializedObject.FindProperty("m_FilterMode");
            m_AnisoLevel = serializedObject.FindProperty("m_AnisoLevel");
            m_Cubemaps = serializedObject.FindProperty("m_Cubemaps");

            m_TextureList = new ReorderableList(serializedObject, m_Cubemaps);
            m_TextureList.displayRemove = false;
            m_TextureList.drawElementCallback += OnDrawElement;
            m_TextureList.drawHeaderCallback += OnDrawHeader;

#if USE_CUSTOM_PREVIEW
            CreatePreview();
            m_PreviewDirty = false;
#endif
        }

        public override void OnDisable()
        {
#if USE_CUSTOM_PREVIEW
            DestroyPreview();
#endif

            base.OnDisable();
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            // This is just some visual nonsense to make it look&feel 
            // similar to Unity's Texture Inspector.
            using (new EditorGUI.DisabledGroupScope(true))
            {
                EditorGUILayout.LabelField(styles.textureTypeLabel, styles.textureTypeValue, EditorStyles.popup);
                EditorGUILayout.LabelField(styles.textureShapeLabel, styles.textureShapeValue, EditorStyles.popup);
                EditorGUILayout.Separator();
            }

            EditorGUILayout.PropertyField(m_WrapMode, styles.wrapModeLabel);
            EditorGUILayout.PropertyField(m_FilterMode, styles.filterModeLabel);
            EditorGUILayout.PropertyField(m_AnisoLevel, styles.anisoLevelLabel);

            // If Aniso is used, check quality settings and displays some info.
            // I've only added this, because Unity is doing it in the Texture Inspector as well.
            if (m_AnisoLevel.intValue > 1)
            {
                if (QualitySettings.anisotropicFiltering == AnisotropicFiltering.Disable)
                    EditorGUILayout.HelpBox(styles.anisotropicFilteringDisable.text, MessageType.Info);

                if (QualitySettings.anisotropicFiltering == AnisotropicFiltering.ForceEnable)
                    EditorGUILayout.HelpBox(styles.anisotropicFilteringForceEnable.text, MessageType.Info);
            }

            // Draw the reorderable texture list only if a single asset is selected.
            // This is to avoid issues drawing the list if it contains a different amount of textures.
            if (!serializedObject.isEditingMultipleObjects)
            {
                EditorGUILayout.Separator();
                m_TextureList.DoLayoutList();
            }

            serializedObject.ApplyModifiedProperties();
            ApplyRevertGUI();

#if USE_CUSTOM_PREVIEW
            if (m_PreviewDirty)
            {
                m_PreviewDirty = false;
                CreatePreview();
            }
#endif
        }

        void OnDrawHeader(Rect rect)
        {
            var label = rect; label.width -= 16;
            var popup = rect; popup.x += label.width; popup.width = 20;

            // Display textures list header
            EditorGUI.LabelField(label, styles.texturesHeaderLabel);

            // Show popup button to open a context menu
            using (new EditorGUI.DisabledGroupScope(m_Cubemaps.hasMultipleDifferentValues))
            {
                if (GUI.Button(popup, styles.popupIcon, EditorStyles.label))
                    ShowHeaderPopupMenu();
            }

            // Handle drag&drop on header label
            if (CanAcceptDragAndDrop(label))
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                if (Event.current.type == EventType.DragPerform)
                    AcceptDragAndDrop();
            }
        }

        void ShowHeaderPopupMenu()
        {
            var menu = new GenericMenu();

            menu.AddItem(new GUIContent("Select Cubemaps"), false, delegate ()
            {
                var importer = target as CubemapArrayImporter;
                Selection.objects = importer.cubemaps;
            });

            menu.ShowAsContext();
        }

        void OnDrawElement(Rect rect, int index, bool isActive, bool isFocused)
        {
            if (m_Cubemaps.arraySize <= index)
                return;

            rect.y += 1;
            rect.height -= 2;

            var r = rect;

            var importer = target as CubemapArrayImporter;
            var textureProperty = m_Cubemaps.GetArrayElementAtIndex(index);
            
            var errorMsg = importer.GetVerifyString(index);
            if (!string.IsNullOrEmpty(errorMsg))
            {
                r = rect;
                rect.width = 24;
                switch (importer.Verify(index))
                {
                    case CubemapArrayImporter.VerifyResult.Valid:
                    case CubemapArrayImporter.VerifyResult.MasterNull:
                        break;

                    default:
                        EditorGUI.LabelField(rect, new GUIContent(styles.errorIcon, errorMsg));
                        break;
                }
                rect = r;
                rect.width -= 24;
                rect.x += 24;
            }
            else
            {

                r = rect;
                rect.width = 24;
                EditorGUI.LabelField(rect, new GUIContent(string.Format("{0}", index), "Slice"), isFocused ? EditorStyles.whiteLabel : EditorStyles.label);
                rect = r;
                rect.width -= 24;
                rect.x += 24;
            }

            r = rect;
            rect.width -= 18;
            EditorGUI.BeginChangeCheck();
            EditorGUI.PropertyField(rect, textureProperty, GUIContent.none);
            if (EditorGUI.EndChangeCheck())
            {
                // We have to apply modification here, so that CubemapArrayImporter.Verify has the just changed values
                serializedObject.ApplyModifiedProperties();

                // Make sure we assign assets that exist on disk only.
                // During my tests, when selecting built-in assets,
                // Unity reimports the texture array asset infinitely, which is probably an Unity bug.
                var result = importer.Verify(index);
                if (result == CubemapArrayImporter.VerifyResult.NotAnAsset)
                {
                    textureProperty.objectReferenceValue = null;
                    var msg = importer.GetVerifyString(index);
                    Debug.LogError(msg, importer);
                }
            }

            rect = r;
            rect.x += rect.width - 15;
            rect.y += 2;
            rect.width = 20;
            if (GUI.Button(rect, styles.removeItemButton, styles.preButton))
                textureProperty.DeleteCommand();
        }

        bool CanAcceptDragAndDrop(Rect rect)
        {
            if (!rect.Contains(Event.current.mousePosition))
                return false;

            foreach (var obj in DragAndDrop.objectReferences)
            {
                var cubemap = obj as Cubemap;
                if (cubemap != null)
                    return true;
            }

            return false;
        }

        void AcceptDragAndDrop()
        {
            serializedObject.Update();

            // Add all textures from the drag&drop operation
            foreach (var obj in DragAndDrop.objectReferences)
            {
                var cubemap = obj as Cubemap;
                if (cubemap != null)
                {
                    m_Cubemaps.InsertArrayElementAtIndex(m_Cubemaps.arraySize);
                    var e = m_Cubemaps.GetArrayElementAtIndex(m_Cubemaps.arraySize - 1);
                    e.objectReferenceValue = cubemap;
                }
            }

            serializedObject.ApplyModifiedProperties();
            DragAndDrop.AcceptDrag();
        }

#if USE_CUSTOM_PREVIEW
        protected override void Apply()
        {
            base.Apply();
            m_PreviewDirty = true;
        }

        public override bool HasPreviewGUI()
        {
            if (!SystemInfo.supportsCubemapArrayTextures)
                return false;

            if (targets.Length != 1)
                return false;

            var cubemapArray = target as CubemapArrayImporter;
            if (cubemapArray == null)
                return false;

            return true;
        }

        public override string GetInfoString()
        {
            // https://forum.unity.com/threads/expose-textureutil-api.771137/
            return "";
        }

        public override void OnPreviewGUI(Rect r, GUIStyle background)
        {
            var importer = target as CubemapArrayImporter;
            if (!SystemInfo.supportsCubemapArrayTextures || m_PreviewCubemap == null || importer == null)
                return;

            Rect prevFrameRect = r;
            prevFrameRect.width = 33;

            Rect nextFrameRect = prevFrameRect;
            nextFrameRect.x += nextFrameRect.width;

            var scrubberRect = r;
            scrubberRect.xMin = nextFrameRect.xMax;

            // The cubemap editor is swallowing our input. Thus we handle our buttons
            // before we call the cubemap editor. However, the cubemap editor overdraws
            // our buttons, so we have to draw them afterwards again.
            if (Event.current.type != EventType.Repaint)
            {
                using (new EditorGUI.DisabledGroupScope(m_PreviewSlice <= 0))
                {
                    if (GUI.Button(prevFrameRect, styles.prevSliceIcon, styles.stepSlice))
                    {
                        m_PreviewSlice--;
                        m_PreviewDirty = true;
                    }
                }

                using (new EditorGUI.DisabledGroupScope(m_PreviewSlice >= importer.cubemaps.Length - 1))
                {
                    if (GUI.Button(nextFrameRect, styles.nextSliceIcon, styles.stepSlice))
                    {
                        m_PreviewSlice++;
                        m_PreviewDirty = true;
                    }
                }
            }

            // Draw the cubemap preview
            var previewRect = r;
            var toolbarHeight = styles.sliceScrubber.CalcHeight(new GUIContent("Wg"), 50);
            previewRect.y += toolbarHeight;
            previewRect.height -= toolbarHeight;
            m_PreviewEditor.OnPreviewGUI(previewRect, background);

            // Paint controls
            if (Event.current.type == EventType.Repaint)
            {
                styles.sliceScrubber.Draw(r, GUIContent.none, -1);

                if (m_PreviewSlice > 0)
                    EditorGUI.DropShadowLabel(new Rect(r.x, r.y + toolbarHeight, r.width, 20), string.Format("Slice {0}", m_PreviewSlice));

                // The cubemap editor overdraw our buttons. Just draw them again.
                using (new EditorGUI.DisabledGroupScope(m_PreviewSlice <= 0))
                    GUI.Button(prevFrameRect, styles.prevSliceIcon, styles.stepSlice);

                using (new EditorGUI.DisabledGroupScope(m_PreviewSlice >= importer.cubemaps.Length - 1))
                    GUI.Button(nextFrameRect, styles.nextSliceIcon, styles.stepSlice);
            }
        }

        void CreatePreview()
        {
            DestroyPreview();

            var importer = target as CubemapArrayImporter;
            var cubemapArray = AssetDatabase.LoadAssetAtPath<CubemapArray>(importer.assetPath);
            if (cubemapArray == null)
                return;

            m_PreviewCubemap = new Cubemap(cubemapArray.width, cubemapArray.format, cubemapArray.mipmapCount > 1);

            for (int face = 0; face < 6; face++)
                Graphics.CopyTexture(cubemapArray, face + (m_PreviewSlice * 6), m_PreviewCubemap, face);

            m_PreviewEditor = Editor.CreateEditor(m_PreviewCubemap);
        }

        void DestroyPreview()
        {
            if (m_PreviewCubemap != null)
            {
                DestroyImmediate(m_PreviewCubemap);
                m_PreviewCubemap = null;
            }

            if (m_PreviewEditor != null)
            {
                DestroyImmediate(m_PreviewEditor);
                m_PreviewEditor = null;
            }
        }
#endif
    }
}
