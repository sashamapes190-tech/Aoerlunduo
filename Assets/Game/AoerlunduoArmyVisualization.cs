using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Core;
using Core.Units;
using StarterKit;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace Aoerlunduo
{
    public sealed class AoerlunduoArmyVisualization : MonoBehaviour
    {
        private GameState gameState;
        private StarterKit.UnitSystem unitSystem;
        private Transform mapTransform;
        private Bounds meshBounds;
        private readonly Dictionary<ushort, Vector2> provincePixels = new Dictionary<ushort, Vector2>();
        private readonly Dictionary<ushort, MarkerVisual> markers = new Dictionary<ushort, MarkerVisual>();
        private readonly List<IDisposable> subscriptions = new List<IDisposable>();
        private Camera mapCamera;
        private bool initialized;
        private bool dirty;

        public bool IsInitialized => initialized;
        public int MarkerCount => markers.Count;

        public void Initialize(GameState state, StarterKit.UnitSystem units)
        {
            if (initialized || state == null || units == null) return;
            gameState = state;
            unitSystem = units;
            var engine = Engine.ArchonEngine.Instance;
            if (engine?.MapMeshRenderer == null || engine.TextureManager == null) return;

            mapTransform = engine.MapMeshRenderer.transform;
            var filter = mapTransform.GetComponent<MeshFilter>();
            if (filter == null || filter.sharedMesh == null) return;
            meshBounds = filter.sharedMesh.bounds;
            mapCamera = Camera.main;
            LoadProvinceCenters();

            var brokenBuiltIn = FindFirstObjectByType<UnitVisualization>();
            if (brokenBuiltIn != null) brokenBuiltIn.enabled = false;

            subscriptions.Add(gameState.EventBus.Subscribe<UnitCreatedEvent>(_ => dirty = true));
            subscriptions.Add(gameState.EventBus.Subscribe<UnitMovedEvent>(_ => dirty = true));
            subscriptions.Add(gameState.EventBus.Subscribe<UnitDestroyedEvent>(_ => dirty = true));
            subscriptions.Add(gameState.EventBus.Subscribe<UnitCountChangedEvent>(_ => dirty = true));
            subscriptions.Add(gameState.EventBus.Subscribe<UnitMovementStartedEvent>(_ => dirty = true));
            subscriptions.Add(gameState.EventBus.Subscribe<UnitMovementCompletedEvent>(_ => dirty = true));
            subscriptions.Add(gameState.EventBus.Subscribe<UnitMovementCancelledEvent>(_ => dirty = true));
            initialized = true;
            dirty = true;
        }

        private void LoadProvinceCenters()
        {
            provincePixels.Clear();
            string path = Path.Combine(GameSettings.Instance.DataDirectory, "map", "province_centers.csv");
            if (!File.Exists(path))
            {
                Debug.LogError($"[Aoerlunduo] Province center file missing: {path}");
                return;
            }

            string[] lines = File.ReadAllLines(path);
            for (int i = 1; i < lines.Length; i++)
            {
                string[] columns = lines[i].Split(';');
                if (columns.Length < 3) continue;
                if (!ushort.TryParse(columns[0], out ushort provinceId)) continue;
                if (!float.TryParse(columns[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float x)) continue;
                if (!float.TryParse(columns[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float y)) continue;
                provincePixels[provinceId] = new Vector2(x, y);
            }
        }

        private Vector3 ProvinceToWorld(ushort provinceId)
        {
            if (!provincePixels.TryGetValue(provinceId, out Vector2 pixel)) return mapTransform.position;
            var textureManager = Engine.ArchonEngine.Instance.TextureManager;
            float u = pixel.x / textureManager.MapWidth;
            float v = 1f - pixel.y / textureManager.MapHeight;
            Vector3 local = new Vector3(
                Mathf.Lerp(meshBounds.min.x, meshBounds.max.x, u),
                0f,
                Mathf.Lerp(meshBounds.min.z, meshBounds.max.z, v));
            Vector3 world = mapTransform.TransformPoint(local);
            world.y += 2.2f;
            return world;
        }

        private Vector3 GetUnitWorldPosition(ushort unitId, UnitState unit)
        {
            Vector3 position = ProvinceToWorld(unit.provinceID);
            if (gameState.Units.MovementQueue.TryGetMovementState(unitId, out var movement))
            {
                float progress = movement.GetProgress();
                if (movement.totalDays > 0)
                    progress += Engine.ArchonEngine.Instance.TimeManager.CurrentHour / (24f * movement.totalDays);
                position = Vector3.Lerp(
                    ProvinceToWorld(movement.originProvinceID),
                    ProvinceToWorld(movement.destinationProvinceID),
                    Mathf.Clamp01(progress));
            }

            float angle = unitId * 137.5f * Mathf.Deg2Rad;
            return position + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 0.35f;
        }

        private void RebuildMarkers()
        {
            ClearMarkers();
            if (!initialized) return;

            using var countries = gameState.CountryQueries.GetAllCountryIds(Allocator.Temp);
            for (int c = 0; c < countries.Length; c++)
            {
                ushort countryId = countries[c];
                if (countryId == 0) continue;
                var unitIds = gameState.Units.GetCountryUnits(countryId);
                for (int i = 0; i < unitIds.Count; i++)
                {
                    ushort unitId = unitIds[i];
                    var unit = unitSystem.GetUnit(unitId);
                    if (unit.unitCount > 0) CreateMarker(unitId, unit);
                }
            }
        }

        private void CreateMarker(ushort unitId, UnitState unit)
        {
            var marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            marker.name = $"Army_{unit.countryID}_{unitId}";
            marker.transform.SetParent(transform, true);
            marker.transform.position = GetUnitWorldPosition(unitId, unit);
            var collider = marker.GetComponent<Collider>();
            if (collider != null) Destroy(collider);

            var renderer = marker.GetComponent<MeshRenderer>();
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            var material = new Material(shader) { color = gameState.CountryQueries.GetColor(unit.countryID) };
            renderer.material = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            var labelObject = new GameObject("TroopCount");
            labelObject.transform.SetParent(marker.transform, false);
            labelObject.transform.localPosition = new Vector3(0f, 1.35f, 0f);
            labelObject.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var text = labelObject.AddComponent<TextMesh>();
            text.text = unit.unitCount.ToString();
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.fontSize = 48;
            text.characterSize = 0.28f;
            text.fontStyle = FontStyle.Bold;
            text.color = Color.white;

            markers[unitId] = new MarkerVisual { Root = marker, Label = text };
        }

        private void LateUpdate()
        {
            if (!initialized) return;
            if (dirty)
            {
                dirty = false;
                RebuildMarkers();
            }

            float scale = mapCamera != null ? Mathf.Clamp(mapCamera.orthographicSize / 35f, 0.75f, 2.2f) : 1f;
            foreach (var pair in markers)
            {
                if (pair.Value.Root == null || !gameState.Units.HasUnit(pair.Key)) continue;
                UnitState unit = gameState.Units.GetUnit(pair.Key);
                pair.Value.Root.transform.position = GetUnitWorldPosition(pair.Key, unit);
                pair.Value.Root.transform.localScale = new Vector3(1.9f * scale, 0.22f, 1.9f * scale);
                if (pair.Value.Label != null) pair.Value.Label.text = unit.unitCount.ToString();
            }
        }

        private void ClearMarkers()
        {
            foreach (var marker in markers.Values)
            {
                if (marker.Root == null) continue;
                var renderer = marker.Root.GetComponent<MeshRenderer>();
                if (renderer != null && renderer.material != null) Destroy(renderer.material);
                Destroy(marker.Root);
            }
            markers.Clear();
        }

        private void OnDestroy()
        {
            for (int i = 0; i < subscriptions.Count; i++) subscriptions[i]?.Dispose();
            ClearMarkers();
        }

        private sealed class MarkerVisual
        {
            public GameObject Root;
            public TextMesh Label;
        }
    }
}
