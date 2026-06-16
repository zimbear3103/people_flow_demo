using System.Collections.Generic;
using UnityEngine;

namespace PeopleFlow
{
    /// <summary>
    /// Builds one level's gameplay objects from <see cref="LevelData"/>: the runway track + its
    /// visual, the holes placed around the loop, and the lanes with their populated queues. Then
    /// wires input/timer/UI and tells <see cref="GameManager"/> to start.
    /// </summary>
    public class LevelManager : MonoBehaviour
    {
        public RunwayTrack Track { get; private set; }
        public readonly List<Lane> Lanes = new List<Lane>();

        Transform m_holesRoot;
        Transform m_lanesRoot;
        Transform m_runnersRoot;

        public void Build(LevelData level, MaterialLibrary mats, InputManager input, Timer timer)
        {
            // --- track --- (everything is parented under this LevelManager so a host bridge can
            // tear the whole level down by destroying one object).
            var trackGo = new GameObject("RunwayTrack");
            trackGo.transform.SetParent(transform, false);
            Track = trackGo.AddComponent<RunwayTrack>();
            Track.Build(level);

            m_holesRoot = NewRoot("Holes");
            m_lanesRoot = NewRoot("Lanes");
            m_runnersRoot = NewRoot("CharactersRoot");

            BuildRunwayVisual(level, mats);
            BuildHoles(level, mats);
            BuildLanes(level, mats);

            input.Bind(Lanes, Camera.main);
            timer.Begin(level.timeLimit);
            UIManager.Instance?.Bind(level, timer, Track);
            GameManager.Instance.BeginLevel(level.TotalHoles);

            PFLog.Info($"Level {level.levelNumber} built: {level.TotalHoles} holes, " +
                       $"{Lanes.Count} lanes, capacity {level.runwayCapacity}.");
        }

        Transform NewRoot(string name)
        {
            var t = new GameObject(name).transform;
            t.SetParent(transform, false);
            return t;
        }

        // ---- holes ----------------------------------------------------------

        void BuildHoles(LevelData level, MaterialLibrary mats)
        {
            foreach (var setup in level.holes)
            {
                var pos = Track.Evaluate(setup.trackPosition);
                var go = new GameObject($"Hole_{setup.color}");
                go.transform.SetParent(m_holesRoot, false);
                go.transform.position = pos;

                var hole = go.AddComponent<Hole>();
                hole.Setup(setup, mats);
                Track.RegisterHole(hole);
            }
        }

        // ---- lanes ----------------------------------------------------------

        void BuildLanes(LevelData level, MaterialLibrary mats)
        {
            int n = level.lanes.Count;
            Vector3 entry = Track.Evaluate(RunwayTrack.EntryT);
            const float spacing = 1.4f;
            float zBack = entry.z - 2.2f; // lanes sit just outside the loop, toward the camera

            for (int i = 0; i < n; i++)
            {
                var go = new GameObject("Lane_" + i);
                go.transform.SetParent(m_lanesRoot, false);

                float x = entry.x + (i - (n - 1) * 0.5f) * spacing;
                var padPos = new Vector3(x, 0f, zBack);

                var lane = go.AddComponent<Lane>();
                lane.Setup(level.lanes[i], Track, mats, m_runnersRoot, level.runSpeed, padPos);
                Lanes.Add(lane);
            }
        }

        // ---- runway visual --------------------------------------------------

        void BuildRunwayVisual(LevelData level, MaterialLibrary mats)
        {
            // Ground plane (Plane is 10×10 units at scale 1).
            float sizeX = level.loopWidth + 9f;
            float sizeZ = level.loopHeight + 11f;
            Prim.Create(PrimitiveType.Plane, "Ground", transform,
                new Vector3(0f, 0f, 0f),
                new Vector3(sizeX / 10f, 1f, sizeZ / 10f), mats.Ground);

            // The loop path itself, drawn as a thick line the runners follow.
            var lineGo = new GameObject("TrackLine");
            lineGo.transform.SetParent(transform, false);
            var lr = lineGo.AddComponent<LineRenderer>();
            int n = 96;
            lr.positionCount = n;
            lr.loop = true;
            lr.useWorldSpace = true;
            lr.widthMultiplier = 0.95f;
            lr.numCapVertices = 4;
            lr.numCornerVertices = 4;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            for (int i = 0; i < n; i++)
            {
                Vector3 p = Track.Evaluate(i / (float)n);
                lr.SetPosition(i, p + Vector3.up * 0.02f);
            }
            var lineShader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
            var lineMat = new Material(lineShader);
            Color lineColor = new Color(0.95f, 0.96f, 1f);
            if (lineMat.HasProperty("_BaseColor")) lineMat.SetColor("_BaseColor", lineColor);
            lineMat.color = lineColor;
            lr.material = lineMat;

            // Entry marker so the player can see where pushed characters enter.
            Vector3 entry = Track.Evaluate(RunwayTrack.EntryT);
            Prim.Create(PrimitiveType.Cube, "EntryMarker", transform,
                new Vector3(entry.x, 0.04f, entry.z), new Vector3(1.5f, 0.05f, 0.5f),
                mats.Solid(new Color(1f, 1f, 1f)));
        }
    }
}
