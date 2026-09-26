using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace LucidCatsBestiary
{
    internal class MonsterViewer : MonoBehaviour
    {
        private const int ViewerLayer = 31;
        private static readonly Vector3 StagePosition = new Vector3(0f, -1000f, 0f);

        private static readonly string[] KeptScripts = { "MMAutoRotate" };

        private static readonly Dictionary<string, GameObject> PrefabCache = new Dictionary<string, GameObject>();

        private static readonly int RetroPixelSizeId = Shader.PropertyToID("_RetroPixelSize");
        private int savedRetroPixelSize;

        public RenderTexture Texture { get; private set; }

        private Camera cam;
        private Transform turntable;
        private readonly List<Light> lights = new List<Light>();
        private GameObject model;
        private string currentPrefab;

        private Mesh bakeMesh;
        private int reframeCountdown;

        private bool flat;
        private float yaw;
        private float pitch;
        private float lastDragTime = -10f;

        public static MonsterViewer Create(Scene scene)
        {
            var go = new GameObject("Bestiary Viewer (mod)");
            SceneManager.MoveGameObjectToScene(go, scene);
            go.transform.position = StagePosition;
            var viewer = go.AddComponent<MonsterViewer>();
            viewer.Setup();
            return viewer;
        }

        private void Setup()
        {
            Texture = new RenderTexture(512, 512, 24, RenderTextureFormat.ARGB32)
            {
                name = "Bestiary Viewer Texture",
                antiAliasing = 2
            };
            Texture.Create();

            var camGo = new GameObject("Viewer Camera");
            camGo.transform.SetParent(transform, false);
            cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
            cam.cullingMask = 1 << ViewerLayer;
            cam.targetTexture = Texture;
            cam.fieldOfView = 30f;
            cam.enabled = false;
            ConfigureUrpCamera(cam);

            lights.Add(CreateLight("Key Light"));
            lights.Add(CreateLight("Fill Light"));

            turntable = new GameObject("Turntable").transform;
            turntable.SetParent(transform, false);

            RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
            RenderPipelineManager.endCameraRendering += OnEndCameraRendering;
        }

        private void OnBeginCameraRendering(ScriptableRenderContext context, Camera camera)
        {
            if (camera == cam)
                savedRetroPixelSize = Shader.GetGlobalInteger(RetroPixelSizeId);
        }

        private void OnEndCameraRendering(ScriptableRenderContext context, Camera camera)
        {
            if (camera == cam)
                Shader.SetGlobalInteger(RetroPixelSizeId, savedRetroPixelSize);
        }

        private Light CreateLight(string lightName)
        {
            var go = new GameObject(lightName);
            go.transform.SetParent(transform, false);
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.shadows = LightShadows.None;
            light.cullingMask = 1 << ViewerLayer;
            return light;
        }

        private static void ConfigureUrpCamera(Camera camera)
        {
            try
            {
                var data = camera.GetComponent<UniversalAdditionalCameraData>();
                if (data == null)
                    data = camera.gameObject.AddComponent<UniversalAdditionalCameraData>();

                data.renderPostProcessing = false;
                data.volumeLayerMask = 0;
                data.renderShadows = false;
                data.antialiasing = AntialiasingMode.None;
            }
            catch (Exception e)
            {
                BestiaryPlugin.Log.LogWarning($"Could not configure the viewer camera for URP: {e.Message}");
            }
        }

        public void SetRendering(bool on)
        {
            if (cam != null)
                cam.enabled = on && model != null;
        }

        public bool Show(string prefabName)
        {
            if (prefabName == currentPrefab && model != null)
                return true;

            Hide();

            GameObject prefab = FindPrefab(prefabName);
            if (prefab == null)
            {
                BestiaryPlugin.Log.LogWarning($"Viewer: model '{prefabName}' not found in memory.");
                return false;
            }

            try
            {
                Transform visuals = prefab.transform.Find("Visuals");
                GameObject source = visuals != null ? visuals.gameObject : prefab;

                var holder = new GameObject("Model Holder");
                holder.SetActive(false);
                holder.transform.SetParent(turntable, false);

                GameObject copy = Instantiate(source, holder.transform, false);
                copy.transform.localPosition = Vector3.zero;
                copy.transform.localRotation = Quaternion.identity;

                StripToVisuals(copy);
                SetLayerRecursively(copy, ViewerLayer);

                turntable.localRotation = Quaternion.identity;
                holder.SetActive(true);

                foreach (Animator animator in holder.GetComponentsInChildren<Animator>())
                {
                    try { animator.Update(0f); }
                    catch { /* framing is corrected again a few frames later anyway */ }
                }

                model = holder;
                currentPrefab = prefabName;
                flat = IsFlat(copy);
                yaw = 0f;
                pitch = 0f;
                FrameModel(holder);
                reframeCountdown = 3;
                return true;
            }
            catch (Exception e)
            {
                BestiaryPlugin.Log.LogError($"Viewer: could not show '{prefabName}': {e}");
                Hide();
                return false;
            }
        }

        public void Hide()
        {
            if (model != null)
                Destroy(model);
            model = null;
            currentPrefab = null;
            if (cam != null)
                cam.enabled = false;
        }

        public void Drag(Vector2 delta)
        {
            yaw -= delta.x * 0.4f;
            pitch = Mathf.Clamp(pitch + delta.y * 0.2f, -30f, 30f);
            if (flat)
                yaw = Mathf.Clamp(yaw, -35f, 35f);
            lastDragTime = Time.unscaledTime;
        }

        private void LateUpdate()
        {
            if (turntable == null || model == null)
                return;

            if (reframeCountdown > 0 && --reframeCountdown == 0)
            {
                Quaternion rotation = turntable.localRotation;
                turntable.localRotation = Quaternion.identity;
                FrameModel(model);
                turntable.localRotation = rotation;
            }

            bool idle = Time.unscaledTime - lastDragTime > 1.5f;
            if (idle)
            {
                if (flat)
                {
                    yaw = Mathf.Lerp(yaw, Mathf.Sin(Time.unscaledTime * 0.8f) * 15f, Time.unscaledDeltaTime * 2f);
                }
                else
                {
                    yaw += BestiaryPlugin.ViewerSpinSpeed.Value * Time.unscaledDeltaTime;
                }
                pitch = Mathf.Lerp(pitch, 0f, Time.unscaledDeltaTime * 2f);
            }

            turntable.localRotation = Quaternion.Euler(pitch, yaw, 0f);
        }

        private void OnDestroy()
        {
            RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
            RenderPipelineManager.endCameraRendering -= OnEndCameraRendering;

            if (Texture != null)
            {
                Texture.Release();
                Destroy(Texture);
            }

            if (bakeMesh != null)
                Destroy(bakeMesh);
        }

        // ---------------------------------------------------------------------------------

        private static GameObject FindPrefab(string prefabName)
        {
            if (PrefabCache.TryGetValue(prefabName, out GameObject cached) && cached != null)
                return cached;

            foreach (DreamEnemy enemy in Resources.FindObjectsOfTypeAll<DreamEnemy>())
            {
                if (enemy != null && !enemy.gameObject.scene.IsValid() && enemy.name == prefabName)
                {
                    PrefabCache[prefabName] = enemy.gameObject;
                    return enemy.gameObject;
                }
            }
            return null;
        }

        private static void StripToVisuals(GameObject copy)
        {
            for (int pass = 0; pass < 3; pass++)
            {
                MonoBehaviour[] scripts = copy.GetComponentsInChildren<MonoBehaviour>(true);
                for (int i = scripts.Length - 1; i >= 0; i--)
                {
                    MonoBehaviour script = scripts[i];
                    if (script == null || Array.IndexOf(KeptScripts, script.GetType().Name) >= 0)
                        continue;
                    try { DestroyImmediate(script); }
                    catch { /* retried in the next pass */ }
                }
            }

            foreach (AudioSource audio in copy.GetComponentsInChildren<AudioSource>(true))
                DestroyImmediate(audio);

            foreach (Collider collider in copy.GetComponentsInChildren<Collider>(true))
                DestroyImmediate(collider);

            foreach (Animator animator in copy.GetComponentsInChildren<Animator>(true))
            {
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            }

            foreach (Light light in copy.GetComponentsInChildren<Light>(true))
                light.cullingMask = 1 << ViewerLayer;
        }

        private static void SetLayerRecursively(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform child in go.transform)
                SetLayerRecursively(child.gameObject, layer);
        }

        private static bool IsFlat(GameObject copy)
        {
            bool hasSprites = false;
            foreach (Renderer r in copy.GetComponentsInChildren<Renderer>(true))
            {
                if (!r.enabled || r is ParticleSystemRenderer)
                    continue;
                if (r is MeshRenderer || r is SkinnedMeshRenderer)
                    return false;
                if (r is SpriteRenderer)
                    hasSprites = true;
            }
            return hasSprites;
        }

        private void FrameModel(GameObject holder)
        {
            bool found = false;
            Bounds bounds = new Bounds(holder.transform.position, Vector3.one);
            foreach (Renderer r in holder.GetComponentsInChildren<Renderer>())
            {
                if (!r.enabled || r is ParticleSystemRenderer)
                    continue;
                Bounds rendererBounds = MeasureBounds(r);
                if (!found)
                {
                    bounds = rendererBounds;
                    found = true;
                }
                else
                {
                    bounds.Encapsulate(rendererBounds);
                }
            }

            holder.transform.position += turntable.position - bounds.center;

            float radius = Mathf.Max(bounds.extents.magnitude, 0.1f);
            float distance = radius / Mathf.Sin(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * 1.05f;

            cam.transform.position = turntable.position + new Vector3(0f, radius * 0.1f, distance);
            cam.transform.LookAt(turntable.position);
            cam.nearClipPlane = Mathf.Max(0.01f, distance - radius * 2f);
            cam.farClipPlane = distance + radius * 3f;

            // Place the lights around the camera, scaled to the model's size.
            PlaceLight(lights[0], new Vector3(0.6f, 0.7f, 0.9f) * distance, 1f);
            PlaceLight(lights[1], new Vector3(-0.8f, 0.2f, 0.7f) * distance, 0.5f);
        }

        private Bounds MeasureBounds(Renderer renderer)
        {
            Bounds result = renderer.bounds;

            if (!(renderer is SkinnedMeshRenderer skinned) || skinned.sharedMesh == null)
                return result;

            try
            {
                if (bakeMesh == null)
                    bakeMesh = new Mesh();

                skinned.BakeMesh(bakeMesh, false);
                Vector3[] vertices = bakeMesh.vertices;
                if (vertices.Length == 0)
                    return result;

                Matrix4x4 toWorld = skinned.transform.localToWorldMatrix;
                var measured = new Bounds(toWorld.MultiplyPoint3x4(vertices[0]), Vector3.zero);
                for (int i = 1; i < vertices.Length; i++)
                    measured.Encapsulate(toWorld.MultiplyPoint3x4(vertices[i]));

                float ratio = measured.size.magnitude / Mathf.Max(0.0001f, result.size.magnitude);
                if (ratio > 0.2f && ratio < 2f)
                    result = measured;
            }
            catch (Exception e)
            {
                BestiaryPlugin.Log.LogDebug($"Viewer: could not measure {renderer.name}: {e.Message}");
            }

            return result;
        }

        private void PlaceLight(Light light, Vector3 offset, float relativeIntensity)
        {
            light.transform.position = turntable.position + offset;
            float d = offset.magnitude;
            light.range = d * 3f;
            light.intensity = BestiaryPlugin.ViewerLightIntensity.Value * relativeIntensity * d * d;
        }
    }

    internal class ViewerDragHandler : MonoBehaviour, IBeginDragHandler, IDragHandler
    {
        public MonsterViewer Viewer;

        public void OnBeginDrag(PointerEventData eventData) { }

        public void OnDrag(PointerEventData eventData)
        {
            if (Viewer != null)
                Viewer.Drag(eventData.delta);
        }
    }
}
