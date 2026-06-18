#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using PeopleFlow; // LevelData / TrackShape live in the enclosing PeopleFlow namespace; the nested
                  // PeopleFlow.EditorTools namespace already resolves them, but this explicit using
                  // keeps the file compiling if it is ever moved out of the PeopleFlow.* tree.

namespace PeopleFlow.EditorTools
{
    /// <summary>
    /// Editor window that bakes a scene <see cref="LineRenderer"/>'s vertices into a
    /// <see cref="LevelData"/> asset's <c>customWaypoints</c> so a hand-drawn loop can drive the
    /// runway path (<see cref="TrackShape.Custom"/>). The importer normalises the line to world
    /// space, projects it onto the XZ ground plane (Y discarded), drops the trailing close /
    /// duplicate vertices, recentres the points on their XZ bounding-box centre (because at runtime
    /// RunwayTrack adds the loop's <c>transform.position</c> back as the world offset, and the
    /// built-in shapes are centred on their bounds), AUTO-CORRECTS the travel direction to the
    /// runtime's winding (bottom-centre then +X first → positive XZ signed area, matching
    /// <see cref="TrackPath"/>'s oval / rectangle), then rotates the list so the bottom-centre vertex
    /// (min Z, |X| tie-break) becomes index 0 to line up with <see cref="RunwayTrack.EntryT"/>. The
    /// "Reverse winding" option is an extra manual override XORed on top of the auto-correction. Only
    /// the computed point values are copied onto the asset — the scene <see cref="LineRenderer"/>
    /// reference is never stored, as a scene-to-asset reference cannot be serialised. Open via
    /// PeopleFlow ▸ Import LineRenderer Path…; no manual dragging required when a LineRenderer is
    /// selected first (selection only auto-fills the source until you pick one manually).
    /// </summary>
    public class LineRendererPathImporter : EditorWindow
    {
        // Coincident-point / Z-tie / degenerate-bounds epsilon in world units. Consistent with the
        // runtime's 1e-4 / 1e-5 / 1e-6 guards in RunwayTrack — a hair looser so authored vertices
        // that are "visually the same" collapse reliably.
        const float Epsilon = 1e-3f;

        // --- Transient window state (never written onto the asset) ---
        LineRenderer m_source;            // scene LineRenderer to read from
        LevelData m_target;               // project LevelData asset to write into

        // True once the user has manually picked a source (via the ObjectField or a deliberate
        // selection). While false, OnEnable / OnSelectionChange may auto-fill the source from the
        // current selection; once true, Selection changes and domain reloads never clobber the pick.
        // Serialised so a deliberate pick is remembered across a domain reload even though the scene
        // reference in m_source itself is nulled by the reload.
        [SerializeField] bool m_userPickedSource = false;

        // --- Options (plain serializable fields so they survive a domain reload) ---
        [SerializeField] bool m_recenter = true;       // subtract XZ bounding-box centre so the path centres on origin
        [SerializeField] bool m_startAtBottom = true;  // rotate so the bottom-centre vertex is index 0
        [SerializeField] bool m_reverse = false;       // EXTRA manual flip XORed on top of the auto winding correction
        [SerializeField] bool m_setCustomShape = true; // also set target.trackShape = TrackShape.Custom
        [SerializeField] bool m_dropDuplicates = true; // collapse consecutive dupes + drop trailing close-point

        // --- Cached preview result (recomputed only when inputs change, not every OnGUI repaint) ---
        List<Vector3> m_processed;        // last computed waypoint list (null until first build)
        bool m_previewDirty = true;       // recompute m_processed on the next OnGUI pass
        int m_lastPositionCount = -1;     // detect external edits to the source's vertex count

        [MenuItem("PeopleFlow/Import LineRenderer Path…")]
        public static void Open()
        {
            var window = GetWindow<LineRendererPathImporter>("Import Path");
            window.titleContent = new GUIContent("Import Path");
            window.minSize = new Vector2(360f, 340f);
            window.Show();
        }

        /// <summary>
        /// Default the source from the current selection when the window is enabled (also fires after
        /// a domain reload). Only auto-fills while the user has not made a manual pick
        /// (<see cref="m_userPickedSource"/>), so a deliberate source survives a reload even though
        /// the scene reference in <see cref="m_source"/> itself is nulled by the reload.
        /// Selection.activeGameObject is null when nothing — or a non-GameObject — is selected, so the
        /// null check is required to avoid a NullReferenceException.
        /// </summary>
        void OnEnable()
        {
            if (m_source == null && !m_userPickedSource)
            {
                var go = Selection.activeGameObject;
                m_source = go != null ? go.GetComponent<LineRenderer>() : null;
            }
            m_previewDirty = true;
        }

        /// <summary>
        /// While the user has not made a manual pick, re-default the source from the selection. Always
        /// repaint at the end so the window reflects selection-driven state (including a fake-null
        /// source whose scene object was just deleted) and the preview/Info messaging stays accurate.
        /// </summary>
        void OnSelectionChange()
        {
            if (!m_userPickedSource)
            {
                var go = Selection.activeGameObject;
                var lr = go != null ? go.GetComponent<LineRenderer>() : null;
                if (lr != null && lr != m_source)
                {
                    m_source = lr;
                    m_previewDirty = true;
                }
            }
            Repaint();
        }

        void OnGUI()
        {
            EditorGUILayout.LabelField("Source & Target", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();

            // Source LineRenderer — allowSceneObjects:true so the picker accepts scene components.
            var newSource = (LineRenderer)EditorGUILayout.ObjectField(
                new GUIContent("Source (LineRenderer)", "Scene LineRenderer whose vertices define the loop."),
                m_source, typeof(LineRenderer), allowSceneObjects: true);
            if (newSource != m_source)
            {
                m_source = newSource;
                // Any change made directly through the field is a deliberate pick (including clearing
                // it back to None) — stop selection from auto-filling it from then on.
                m_userPickedSource = true;
            }

            // Target LevelData — allowSceneObjects:false so only project assets are selectable.
            m_target = (LevelData)EditorGUILayout.ObjectField(
                new GUIContent("Target (LevelData)", "Project LevelData asset to write customWaypoints into."),
                m_target, typeof(LevelData), allowSceneObjects: false);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Options", EditorStyles.boldLabel);
            m_dropDuplicates = EditorGUILayout.Toggle(
                new GUIContent("Drop closing / duplicate points",
                    "Collapse consecutive coincident vertices and drop a trailing point equal to the first " +
                    "(the loop auto-closes last → first at runtime)."),
                m_dropDuplicates);
            m_recenter = EditorGUILayout.Toggle(
                new GUIContent("Recenter on bounds",
                    "Subtract the XZ bounding-box centre so the path is centred on the origin (matching the " +
                    "built-in oval / rectangle shapes). Required: the loop's world placement comes from " +
                    "RunwayTrack.transform.position at runtime, so waypoints must be LOCAL offsets from the " +
                    "loop centre."),
                m_recenter);
            m_startAtBottom = EditorGUILayout.Toggle(
                new GUIContent("Start at bottom-centre",
                    "Rotate the list so the vertex nearest bottom-centre (min Z, tie-break |X|) becomes " +
                    "index 0, matching RunwayTrack.EntryT (= 0, where lanes feed in)."),
                m_startAtBottom);
            m_reverse = EditorGUILayout.Toggle(
                new GUIContent("Reverse winding (manual override)",
                    "Travel direction is auto-corrected to the runtime's winding (bottom-centre then +X " +
                    "first). Enable this to flip that auto-corrected direction — it is XORed on top, so " +
                    "leave it OFF unless you specifically need the mirror direction."),
                m_reverse);
            m_setCustomShape = EditorGUILayout.Toggle(
                new GUIContent("Set shape to Custom",
                    "Also set target.trackShape = TrackShape.Custom (the only shape that consumes " +
                    "customWaypoints)."),
                m_setCustomShape);

            if (EditorGUI.EndChangeCheck())
                m_previewDirty = true;

            EditorGUILayout.Space();
            DrawPreviewAndImport();
        }

        // ------------------------------------------------------------------ Preview + import button

        void DrawPreviewAndImport()
        {
            EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel);

            if (m_source == null)
            {
                EditorGUILayout.HelpBox("Select a scene LineRenderer to import from.", MessageType.Info);
                m_lastPositionCount = -1;
                return;
            }
            if (m_target == null)
            {
                EditorGUILayout.HelpBox("Select a target LevelData asset to write into.", MessageType.Info);
            }

            // Read-only note about which space the source line is authored in, so the bounds / coords
            // below make sense. World-space lines ignore the GameObject transform (Unity behaviour).
            EditorGUILayout.LabelField("Source space",
                m_source.useWorldSpace ? "World (used as-is)" : "Local (transformed by the object)");

            // Recompute the processed list only when something that affects it changed. EditorWindows
            // repaint frequently (mouse move, hover, neighbouring inspectors); rebuilding the Lists on
            // every OnGUI pass is wasteful GC churn, so cache and reuse between repaints.
            if (m_source.positionCount != m_lastPositionCount)
            {
                m_lastPositionCount = m_source.positionCount;
                m_previewDirty = true;
            }
            if (m_previewDirty || m_processed == null)
            {
                m_processed = BuildWaypoints(
                    m_source, m_dropDuplicates, m_recenter, m_startAtBottom, m_reverse);
                m_previewDirty = false;
            }

            List<Vector3> processed = m_processed;
            int count = processed != null ? processed.Count : 0;
            bool enoughPoints = count >= 3;

            EditorGUILayout.LabelField("Processed points", count.ToString());

            // Detect non-finite (NaN / Infinity) waypoints — these can flow in from a broken procedural
            // authoring tool or a degenerate transform, and would corrupt the runtime arc-length engine
            // (zero / NaN total length, division by NaN). They must block the import outright.
            bool hasNonFinite = false;
            for (int i = 0; i < count; i++)
            {
                Vector3 p = processed[i];
                if (float.IsNaN(p.x) || float.IsNaN(p.z) || float.IsInfinity(p.x) || float.IsInfinity(p.z))
                {
                    hasNonFinite = true;
                    break;
                }
            }

            // XZ bounds size of the processed points (after recentring, if enabled). Only meaningful
            // when every point is finite; otherwise the min/max sentinels never update.
            Vector2 size = Vector2.zero;
            if (count > 0 && !hasNonFinite)
            {
                float minX = float.MaxValue, maxX = float.MinValue;
                float minZ = float.MaxValue, maxZ = float.MinValue;
                for (int i = 0; i < count; i++)
                {
                    Vector3 p = processed[i];
                    if (p.x < minX) minX = p.x;
                    if (p.x > maxX) maxX = p.x;
                    if (p.z < minZ) minZ = p.z;
                    if (p.z > maxZ) maxZ = p.z;
                }
                size = new Vector2(maxX - minX, maxZ - minZ);
            }
            EditorGUILayout.LabelField(m_recenter ? "Recentred XZ bounds (W × H)" : "XZ bounds (W × H)",
                hasNonFinite ? "—" : $"{size.x:0.###} × {size.y:0.###}");

            bool degenerateBounds = !hasNonFinite && enoughPoints && (size.x < Epsilon || size.y < Epsilon);

            // Warnings / errors. canImport gates both the success message and the button so the
            // messaging always matches the button state.
            bool canImport = false;
            if (m_source.positionCount == 0)
            {
                EditorGUILayout.HelpBox("Source LineRenderer has no vertices (positionCount == 0).",
                    MessageType.Error);
            }
            else if (hasNonFinite)
            {
                EditorGUILayout.HelpBox(
                    "Source contains NaN / Infinite vertices — cannot import. Fix the LineRenderer (or the " +
                    "tool / transform that produced it) so every vertex is finite.",
                    MessageType.Error);
            }
            else if (!enoughPoints)
            {
                EditorGUILayout.HelpBox(
                    $"Need at least 3 valid points after processing — got {count}. " +
                    "A line with < 3 distinct vertices cannot form a loop (runtime would fall back to an oval).",
                    MessageType.Error);
            }
            else if (degenerateBounds)
            {
                // Blocking: a zero-area / collinear loop bakes a zero-length path that breaks the
                // runtime arc-length engine. Treat as an error, not a non-blocking warning.
                EditorGUILayout.HelpBox(
                    (m_recenter ? "Recentred bounds" : "Bounds") +
                    " are near-zero in one axis — the loop is collinear / degenerate and would bake a " +
                    "zero-length path. Import is blocked until the loop has area.",
                    MessageType.Error);
            }
            else if (m_startAtBottom && Mathf.Abs(processed[0].x) > Epsilon)
            {
                // Non-blocking: index 0 landed on a corner, not the true bottom-centre. Lanes feed at
                // EntryT = 0, so runners would enter at the corner. We try to synthesise a centred
                // start vertex in BuildWaypoints, but warn if the source still has no centred bottom
                // vertex to anchor to.
                EditorGUILayout.HelpBox(
                    $"Index 0 is offset from bottom-centre (X = {processed[0].x:0.###}). Lanes feed in at " +
                    "EntryT = 0, so runners will enter at this offset point. Add a vertex at the centre of " +
                    "the bottom edge of your line for a clean entry.",
                    MessageType.Warning);
                canImport = true;
            }
            else
            {
                canImport = true;
            }

            // Warn if the target is not a persisted asset — without one the change cannot be written
            // to disk. This also keeps the button disabled (canImport requires a persisted target).
            bool targetPersisted = m_target != null && AssetDatabase.Contains(m_target);
            if (m_target != null && !targetPersisted)
            {
                EditorGUILayout.HelpBox(
                    "Target LevelData is not a saved project asset — changes cannot be persisted to disk.",
                    MessageType.Warning);
            }

            canImport = canImport && m_target != null && targetPersisted;

            if (canImport)
                EditorGUILayout.HelpBox($"Ready: {count} valid waypoints.", MessageType.Info);

            EditorGUILayout.Space();

            EditorGUI.BeginDisabledGroup(!canImport);
            if (GUILayout.Button("Import Path into LevelData", GUILayout.Height(28f)))
            {
                Import(processed);
            }
            EditorGUI.EndDisabledGroup();
        }

        void Import(List<Vector3> processed)
        {
            // Guard everything again at the point of mutation (the button can only be pressed when
            // these hold, but defend against stale state between Repaint and click).
            if (m_source == null)
            {
                Debug.LogError("[PeopleFlow] Import aborted: no source LineRenderer assigned.");
                return;
            }
            if (m_target == null)
            {
                Debug.LogError("[PeopleFlow] Import aborted: no target LevelData assigned.");
                return;
            }
            if (processed == null || processed.Count < 3)
            {
                Debug.LogError(
                    "[PeopleFlow] Import aborted: need at least 3 valid waypoints, got " +
                    $"{(processed != null ? processed.Count : 0)}.");
                return;
            }

            // Final non-finite / degenerate guard before writing — never bake a path that corrupts the
            // runtime arc-length engine, even if the UI state was somehow stale.
            float minX = float.MaxValue, maxX = float.MinValue;
            float minZ = float.MaxValue, maxZ = float.MinValue;
            for (int i = 0; i < processed.Count; i++)
            {
                Vector3 p = processed[i];
                if (float.IsNaN(p.x) || float.IsNaN(p.z) || float.IsInfinity(p.x) || float.IsInfinity(p.z))
                {
                    Debug.LogError("[PeopleFlow] Import aborted: processed waypoints contain NaN / Infinity.");
                    return;
                }
                if (p.x < minX) minX = p.x;
                if (p.x > maxX) maxX = p.x;
                if (p.z < minZ) minZ = p.z;
                if (p.z > maxZ) maxZ = p.z;
            }
            if ((maxX - minX) < Epsilon || (maxZ - minZ) < Epsilon)
            {
                Debug.LogError(
                    "[PeopleFlow] Import aborted: loop bounds are near-zero in one axis (collinear / " +
                    "degenerate) — this would bake a zero-length path.");
                return;
            }

            Undo.RecordObject(m_target, "Import LineRenderer Path");

            // Assign a fresh copy — never hold a reference to the LineRenderer's array, and never the
            // LineRenderer itself.
            m_target.customWaypoints = new List<Vector3>(processed);
            if (m_setCustomShape)
                m_target.trackShape = TrackShape.Custom;

            // SetDirty (no explicit SaveAssets): the change persists on the next project save, which
            // keeps a Ctrl+Z Undo and the on-disk .asset consistent — an immediate SaveAssets would
            // leave the imported copy on disk even after the user undoes the in-memory change.
            EditorUtility.SetDirty(m_target);

            string assetPath = AssetDatabase.GetAssetPath(m_target);
            Debug.Log(
                $"[PeopleFlow] Imported {processed.Count} waypoints into '{m_target.name}' " +
                $"({assetPath})" +
                (m_setCustomShape ? ", trackShape set to Custom" : "") +
                $" [recenter={m_recenter}, startAtBottom={m_startAtBottom}, reverse={m_reverse}, " +
                $"dropDupes={m_dropDuplicates}]. Saved to disk on the next project save (Ctrl+S).");
        }

        // ------------------------------------------------------------------ Pure processing pipeline

        /// <summary>
        /// Pure helper: read a LineRenderer's vertices and return the processed, ready-to-store list
        /// of LOCAL XZ waypoints (Y = 0). Returns an empty list (never null) on a null source or
        /// when the source has no vertices. Order of operations matters and follows the project spec:
        /// (1) read + world-convert, (2) project to XZ dropping any non-finite vertex, (3) drop
        /// trailing close-point + collapse consecutive dupes, (4) recentre on the XZ bounding-box
        /// centre, (5) auto-correct travel direction to the runtime winding (XORed with the manual
        /// <paramref name="reverse"/> override), (6) rotate bottom-centre → index 0.
        /// </summary>
        static List<Vector3> BuildWaypoints(LineRenderer lr, bool dropDuplicates, bool recenter,
                                            bool startAtBottom, bool reverse)
        {
            var result = new List<Vector3>();
            if (lr == null)
                return result;

            // 1. Read positions. Size the buffer from positionCount; GetPositions fills the supplied
            //    array in place and returns the count copied (it does NOT allocate a new array).
            int n = lr.positionCount;
            if (n <= 0)
                return result;

            var pts = new Vector3[n];
            lr.GetPositions(pts);

            // 2. Normalise to world space. GetPositions returns points in the SAME space the line is
            //    authored in — WORLD when useWorldSpace, otherwise LOCAL to lr.transform — with no
            //    automatic conversion. Use TransformPoint (affine: translation+rotation+scale) for
            //    LOCAL points; do NOT double-transform world-space lines.
            Transform t = lr.transform;
            bool worldSpace = lr.useWorldSpace;

            // 3. Project each world point onto the XZ ground plane (Y discarded). Skip any non-finite
            //    vertex here so NaN / Infinity (from a broken authoring tool or a degenerate transform)
            //    can never enter the pipeline, bias the bounds, or get baked onto the asset.
            var planar = new List<Vector3>(n);
            for (int i = 0; i < n; i++)
            {
                Vector3 world = worldSpace ? pts[i] : t.TransformPoint(pts[i]);
                if (float.IsNaN(world.x) || float.IsNaN(world.z) ||
                    float.IsInfinity(world.x) || float.IsInfinity(world.z))
                    continue;
                planar.Add(new Vector3(world.x, 0f, world.z));
            }

            // 4. Drop the trailing close-point + collapse consecutive duplicates. Must run BEFORE
            //    reversing (so the correct end is trimmed) and BEFORE recentring (so a duplicated
            //    first/last vertex cannot bias the bounds).
            List<Vector3> cleaned = dropDuplicates ? DropDuplicates(planar) : planar;

            if (cleaned.Count < 3)
            {
                // Degenerate input — return what we have so the preview can report "< 3".
                return cleaned;
            }

            // 5. Recentre on the XZ bounding-box centre (midpoint of the XZ extents), matching the
            //    built-in oval / rectangle which are centred on their bounds. (The arithmetic centroid
            //    would be pulled toward densely-sampled sides on an unevenly-sampled custom loop.)
            if (recenter)
            {
                Vector3 centre = BoundsCentre(cleaned);
                for (int i = 0; i < cleaned.Count; i++)
                {
                    Vector3 p = cleaned[i];
                    cleaned[i] = new Vector3(p.x - centre.x, 0f, p.z - centre.z);
                }
            }

            // 6. Normalise travel DIRECTION to the runtime winding. TrackPath winds bottom-centre then
            //    +X first (sin/cos with an increasing angle from -90°), which is a POSITIVE XZ signed
            //    (shoelace) area. If the authored line winds the other way the signed area is negative,
            //    so flip it. The manual `reverse` toggle is XORed on top as an explicit override.
            //    Done BEFORE the bottom-centre rotate, which is direction-agnostic and has the final
            //    say on which vertex is index 0. (When startAtBottom is ON, a full list reverse and the
            //    index-0-fixed partial reverse below are equivalent — the rotate re-fixes index 0 — so
            //    preserving index 0 here only matters when startAtBottom is OFF.)
            bool windingIsWrong = SignedArea(cleaned) < 0f; // negative ⇒ opposite of runtime winding
            if (windingIsWrong ^ reverse)
                cleaned.Reverse(1, cleaned.Count - 1); // keep index 0 fixed; flip the remainder's order

            // 7. Rotate so the bottom-centre vertex (min Z, tie-break smallest |X|) is index 0. This
            //    aligns with RunwayTrack.EntryT (= 0, bottom-centre where lanes feed in). If the bottom
            //    edge has only its two corners (straddling X = 0) and no centred vertex, synthesise one
            //    at the bottom-edge centre first so EntryT = 0 sits dead-centre like the built-in shapes
            //    (TrackPath inserts (0,0,-b)).
            if (startAtBottom)
            {
                EnsureBottomCentreVertex(cleaned);
                int k = FindBottomCentreIndex(cleaned);
                if (k > 0)
                    cleaned = Rotate(cleaned, k);
            }

            return cleaned;
        }

        /// <summary>
        /// Collapse consecutive points whose XZ distance is below <see cref="Epsilon"/>, then drop a
        /// trailing point coincident with the first (a visually-closed loop repeats its start vertex;
        /// the runtime closes last → first itself, so a stored duplicate would create a zero-length
        /// segment).
        /// </summary>
        static List<Vector3> DropDuplicates(List<Vector3> src)
        {
            var outList = new List<Vector3>(src.Count);
            for (int i = 0; i < src.Count; i++)
            {
                if (outList.Count == 0 || XzDistance(outList[outList.Count - 1], src[i]) >= Epsilon)
                    outList.Add(src[i]);
            }

            // Drop a trailing vertex coincident with the first (only when more than one point remains).
            while (outList.Count > 1 &&
                   XzDistance(outList[outList.Count - 1], outList[0]) < Epsilon)
            {
                outList.RemoveAt(outList.Count - 1);
            }

            return outList;
        }

        /// <summary>Midpoint of the XZ bounding box (matches the built-in shapes' bounds-centred origin).</summary>
        static Vector3 BoundsCentre(List<Vector3> pts)
        {
            int n = pts.Count;
            if (n == 0) return Vector3.zero;

            float minX = float.MaxValue, maxX = float.MinValue;
            float minZ = float.MaxValue, maxZ = float.MinValue;
            for (int i = 0; i < n; i++)
            {
                if (pts[i].x < minX) minX = pts[i].x;
                if (pts[i].x > maxX) maxX = pts[i].x;
                if (pts[i].z < minZ) minZ = pts[i].z;
                if (pts[i].z > maxZ) maxZ = pts[i].z;
            }
            return new Vector3((minX + maxX) * 0.5f, 0f, (minZ + maxZ) * 0.5f);
        }

        /// <summary>
        /// Signed area of the closed XZ polygon (shoelace, x→X, z→Z). The runtime winding
        /// (bottom-centre then +X first) yields a POSITIVE area, so a negative result means the
        /// authored line winds the opposite way and must be reversed.
        /// </summary>
        static float SignedArea(List<Vector3> p)
        {
            float s = 0f;
            int n = p.Count;
            for (int i = 0; i < n; i++)
            {
                Vector3 a = p[i];
                Vector3 b = p[(i + 1) % n];
                s += a.x * b.z - b.x * a.z;
            }
            return 0.5f * s;
        }

        /// <summary>
        /// If the bottom edge (the two consecutive vertices spanning the minimum Z) has no vertex near
        /// X = 0 but its endpoints straddle X = 0, insert a bottom-centre vertex at (0, 0, minZ) on that
        /// edge so <see cref="FindBottomCentreIndex"/> lands EntryT = 0 dead-centre — matching
        /// TrackPath's (0,0,-b) start. No-op when a centred bottom vertex already exists or the corners
        /// are on the same side of X = 0 (a slanted bottom edge has no meaningful centre to insert).
        /// </summary>
        static void EnsureBottomCentreVertex(List<Vector3> pts)
        {
            int n = pts.Count;
            if (n < 3) return;

            float minZ = float.MaxValue;
            for (int i = 0; i < n; i++)
                if (pts[i].z < minZ) minZ = pts[i].z;

            // Already have a vertex on (or very near) the bottom edge that is centred on X = 0.
            for (int i = 0; i < n; i++)
                if (pts[i].z - minZ <= Epsilon && Mathf.Abs(pts[i].x) <= Epsilon)
                    return;

            // Find a consecutive pair both on the bottom edge whose X values straddle 0; insert the
            // centre between them.
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                bool iBottom = pts[i].z - minZ <= Epsilon;
                bool jBottom = pts[j].z - minZ <= Epsilon;
                if (!iBottom || !jBottom) continue;
                if ((pts[i].x <= 0f) != (pts[j].x <= 0f)) // straddles X = 0
                {
                    // Insert after i so cyclic order is preserved (handles the wrap pair j = 0 too).
                    pts.Insert(i + 1, new Vector3(0f, 0f, minZ));
                    return;
                }
            }
        }

        /// <summary>
        /// Index of the bottom-centre vertex: minimum Z, with near-equal-minimum Z vertices grouped
        /// by <see cref="Epsilon"/> and tie-broken by smallest |X| (with |X| ties themselves grouped
        /// by <see cref="Epsilon"/> so near-symmetric loops resolve to the lowest original index for
        /// full determinism).
        /// </summary>
        static int FindBottomCentreIndex(List<Vector3> pts)
        {
            int n = pts.Count;
            if (n == 0) return 0;

            // First pass: find the minimum Z.
            float minZ = float.MaxValue;
            for (int i = 0; i < n; i++)
                if (pts[i].z < minZ) minZ = pts[i].z;

            // Second pass: among vertices within Epsilon of minZ, pick smallest |X| (then lowest index).
            int best = -1;
            float bestAbsX = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                if (pts[i].z - minZ > Epsilon) continue; // not in the bottom-edge group
                float absX = Mathf.Abs(pts[i].x);
                // Use the file-wide Epsilon (not Mathf.Epsilon, which is a sub-denormal no-op) so two
                // near-equal |X| vertices on a symmetric loop are a tie and keep the lowest index.
                if (best < 0 || absX < bestAbsX - Epsilon)
                {
                    best = i;
                    bestAbsX = absX;
                }
            }
            return best < 0 ? 0 : best;
        }

        /// <summary>Cyclically shift the list so element [k] becomes index 0, preserving order.</summary>
        static List<Vector3> Rotate(List<Vector3> pts, int k)
        {
            int n = pts.Count;
            var outList = new List<Vector3>(n);
            for (int i = 0; i < n; i++)
                outList.Add(pts[(k + i) % n]);
            return outList;
        }

        /// <summary>Distance between two points on the XZ plane (Y ignored).</summary>
        static float XzDistance(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x;
            float dz = a.z - b.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }
    }
}
#endif
