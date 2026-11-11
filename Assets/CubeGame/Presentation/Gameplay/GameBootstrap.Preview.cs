using System;
using System.Collections.Generic;
using CubeGame.AlphaModels;
using CubeGame.Content;
using CubeGame.Features.Contracts;
using CubeGame.Features.Session;
using CubeGame.Presentation.Visual;
using UnityEngine;
using UnityEngine.UIElements;

namespace CubeGame.Gameplay
{
    /// <summary>
    /// Character preview of the Alpha editor (CharacterPreviewWidget 0x424E80 / CharacterStyleWidget hold a live
    /// creature model, not a static image). The port renders the same composed hero visual — race body parts,
    /// face and haircut models, hair colour, starter weapon — into a RenderTexture shown in the editor screen.
    /// The exact Alpha camera framing and drag-to-rotate interaction are UNKNOWN; the preview is fixed and frontal.
    /// </summary>
    public sealed partial class GameBootstrap
    {
        private const int PreviewWidth = 260;
        private const int PreviewHeight = 340;
        private const float PreviewFarmY = -3000f;

        private GameObject _previewStage;
        private Camera _previewCamera;
        private RenderTexture _previewTexture;
        private AlphaActorView _previewView;
        private VisualElement _previewPanel;
        private Image _previewImage;
        private string _previewProblems = string.Empty;

        private VisualElement PreviewPanel(VisualElement parent)
        {
            _previewPanel = new VisualElement { name = "character-preview" };
            _previewPanel.AddToClassList("preview-panel");
            parent.Add(_previewPanel);
            return _previewPanel;
        }

        /// <summary>Rebuilds the preview model for the current editor draft.</summary>
        private void RefreshEditorPreview()
        {
            if (_previewPanel == null) return;
            DestroyPreviewStage();
            CreatePreviewStage(_previewPanel, _formLook);
        }

        private void CreatePreviewStage(VisualElement panel, HeroAppearance appearance)
        {
            if (panel == null) return;
            AlphaActorLibrary library = AlphaActorLibrary.Shared;
            if (library == null || library.State != AlphaContentState.Ready)
            {
                _previewProblems = "Alpha model content is not staged";
                Text(panel, "Preview unavailable", "preview-missing", "muted");
                return;
            }

            _previewTexture = new RenderTexture(PreviewWidth, PreviewHeight, 24, RenderTextureFormat.ARGB32)
            {
                name = "Character preview",
                antiAliasing = 1,
            };
            _previewTexture.Create();

            _previewStage = new GameObject("Character Preview Stage");
            _previewStage.transform.position = new Vector3(0f, PreviewFarmY, 0f);
            _previewStage.transform.rotation = Quaternion.identity;

            var cameraObject = new GameObject("Character Preview Camera");
            cameraObject.transform.SetParent(_previewStage.transform, false);
            cameraObject.transform.localPosition = new Vector3(0f, 1.0f, 4.4f);
            cameraObject.transform.localRotation = Quaternion.Euler(6f, 180f, 0f);
            _previewCamera = cameraObject.AddComponent<Camera>();
            _previewCamera.clearFlags = CameraClearFlags.SolidColor;
            _previewCamera.backgroundColor = new Color(0.16f, 0.18f, 0.22f, 1f);
            _previewCamera.fieldOfView = 30f;
            _previewCamera.nearClipPlane = 0.05f;
            _previewCamera.farClipPlane = 20f;
            _previewCamera.targetTexture = _previewTexture;
            _previewCamera.enabled = true;

            var lightObject = new GameObject("Character Preview Light");
            lightObject.transform.SetParent(_previewStage.transform, false);
            lightObject.transform.localRotation = Quaternion.Euler(20f, 160f, 0f);
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.25f;
            light.shadows = LightShadows.None;

            var fillObject = new GameObject("Character Preview Fill");
            fillObject.transform.SetParent(_previewStage.transform, false);
            fillObject.transform.localRotation = Quaternion.Euler(15f, -70f, 0f);
            Light fill = fillObject.AddComponent<Light>();
            fill.type = LightType.Directional;
            fill.intensity = 0.55f;
            fill.shadows = LightShadows.None;

            var empty = new Dictionary<VisualEquipmentSlot, HeroVisualItem>();
            HeroVisualSpec spec = HeroVisualComposer.Compose(appearance, empty);
            string root = library.Source != null ? library.Source.Root : null;
            string items = root != null ? System.IO.Path.Combine(root, "items") : null;
            CharacterVisualResult result = AlphaPlayerVisualBuilder.Build(spec.ToRequest(root), _previewStage.transform,
                library, items);
            _previewView = result.View;
            _previewProblems = string.Join("; ", result.Problems);
            if (_previewView != null && _previewView.Ready)
            {
                _previewView.Tick(0f);
                FitPreview(_previewView);
            }
            else
            {
                _previewProblems = string.IsNullOrEmpty(_previewProblems)
                    ? "the Alpha model of this appearance is not staged" : _previewProblems;
                Debug.LogWarning($"[Preview] {_previewProblems}");
            }

            _previewImage = new Image { image = _previewTexture, scaleMode = ScaleMode.ScaleToFit, name = "preview-image" };
            _previewImage.AddToClassList("preview-image");
            panel.Add(_previewImage);
            if (!string.IsNullOrEmpty(_previewProblems))
            {
                Text(panel, _previewProblems, "preview-problems", "muted");
            }
        }

        private static void FitPreview(AlphaActorView view)
        {
            Renderer[] renderers = view.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return;
            Bounds bounds = renderers[0].bounds;
            foreach (Renderer renderer in renderers) bounds.Encapsulate(renderer.bounds);
            float height = bounds.size.y;
            if (height <= 0.001f) return;

            // Alpha draws every creature at one voxel scale; the preview only normalizes the framing.
            // Renderer bounds are world-space and the preview stage lives far from the origin, so the offset
            // is taken relative to the actor root.
            float scale = 1.8f / height;
            Vector3 pivot = view.transform.position;
            view.transform.localScale = Vector3.one * scale;
            view.transform.localPosition = new Vector3(
                -(bounds.center.x - pivot.x) * scale,
                -(bounds.min.y - pivot.y) * scale,
                -(bounds.center.z - pivot.z) * scale);
        }

        private void DestroyPreviewStage()
        {
            if (_previewView != null)
            {
                // Same ownership rule as GameVisualSink: the view owns the meshes its rig generated.
                _previewView.gameObject.SetActive(false);
                foreach (MeshFilter filter in _previewView.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (filter.sharedMesh != null) Destroy(filter.sharedMesh);
                }

                Destroy(_previewView.gameObject);
                _previewView = null;
            }

            if (_previewStage != null)
            {
                Destroy(_previewStage);
                _previewStage = null;
            }

            _previewCamera = null;
            if (_previewTexture != null)
            {
                _previewTexture.Release();
                Destroy(_previewTexture);
                _previewTexture = null;
            }

            _previewImage = null;
            _previewPanel = null;
        }
    }
}
