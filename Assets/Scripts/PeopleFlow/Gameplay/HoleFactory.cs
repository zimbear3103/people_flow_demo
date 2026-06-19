using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PeopleFlow
{
    /// <summary>
    /// Produces a <em>bundle</em> of holes at one track position, one at a time. The first hole in
    /// the bundle pops in; when a runner fills it, that hole fires <see cref="Hole.OnCompleted"/>,
    /// the factory retires it (unregisters it from the track and shrinks it away), then spawns the
    /// next hole in the bundle in its place. When the bundle is exhausted the factory goes idle.
    ///
    /// Added at runtime by <see cref="LevelManager"/> onto an instantiated factory prefab (or used
    /// from a prefab that already carries the component). A factory hole is a normal
    /// <see cref="Hole"/>, so colours, fills and section-7 specials all behave exactly as a
    /// standalone hole — the only difference is that several share one position over time.
    /// </summary>
    public class HoleFactory : MonoBehaviour
    {
        [SerializeField]
        private Transform m_holeHolder;
        [SerializeField]
        private Transform m_nextHolePreviewHolder;
        [SerializeField, Tooltip("Seconds the completed hole lingers (so the player sees it fill) before it shrinks away.")]
        float m_retireDelay = 0.18f;
        [SerializeField, Tooltip("Seconds the completed hole takes to shrink to nothing before the next one pops in.")]
        float m_retireDuration = 0.2f;

        GameObject m_holePrefab;
        MaterialLibrary m_mats;
        RunwayTrack m_track;
        
        [SerializeField] private List<HoleSetup> m_bundle = new List<HoleSetup>();
        float m_trackPosition;
        int m_next;          // index of the next bundle entry to produce
        Hole m_current;      // the live hole, or null while none is being produced
        Hole m_preview;      // inert preview of the next bundle entry, shown at m_nextHolePreviewHolder

        /// <summary>The hole currently presented by this factory, or null while idle/swapping.</summary>
        public Hole Current => m_current;

        /// <summary>True once every hole in the bundle has been produced and completed.</summary>
        public bool IsExhausted => m_next >= m_bundle.Count && m_current == null;

        /// <summary>True during the brief gap between a hole completing and its replacement popping in
        /// (a hole is still coming). The track uses this so it doesn't mistake the swap for a jam.</summary>
        public bool IsProducing => m_current == null && m_next < m_bundle.Count;
        public List<HoleSetup> Bundle => m_bundle;
        /// <summary>
        /// Configure the factory and produce its first hole. <paramref name="holePrefab"/> is the
        /// prefab every hole in the bundle is instantiated from (the same Hole prefab the level uses).
        /// Produced holes are parented to the factory (so they ride its conveyor and inherit its
        /// orientation), offset by <see cref="m_holeLocalOffset"/>.
        /// </summary>
        public void Setup(HoleFactorySetup setup, GameObject holePrefab, MaterialLibrary mats,
            RunwayTrack track)
        {
            m_holePrefab = holePrefab;
            m_mats = mats;
            m_track = track;
            m_trackPosition = setup.trackPosition;

            m_bundle.Clear();
            if (setup.bundle != null) m_bundle.AddRange(setup.bundle);

            if (m_holePrefab == null)
            {
                PFLog.Error($"HoleFactory '{name}': no hole prefab to produce from — bundle disabled.");
                return;
            }

            m_track.RegisterFactory(this);
            SpawnNext();
        }

        void SpawnNext()
        {
            m_current = null;
            if (m_next >= m_bundle.Count)
            {
                RefreshPreview(); // bundle exhausted — clear any lingering preview
                return;
            }

            HoleSetup src = m_bundle[m_next++];
            var setup = new HoleSetup
            {
                color = src.color,
                requiredCount = src.requiredCount,
                trackPosition = m_trackPosition,
                hidden = src.hidden,
                mechanic = src.mechanic,
                unlockAfterHolesCompleted = src.unlockAfterHolesCompleted,
            };

            // Spawn the hole at the holder slot on the factory's conveyor (rides its orientation).
            Transform parent = m_holeHolder != null ? m_holeHolder : transform;
            var go = Instantiate(m_holePrefab, parent);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.name = $"Hole_{setup.color}";

            var hole = go.GetComponent<Hole>();
            if (hole == null)
            {
                PFLog.Error($"HoleFactory '{name}': hole prefab '{m_holePrefab.name}' has no Hole component — skipping.");
                Destroy(go);
                SpawnNext(); // skip the bad slot so the rest of the bundle still plays
                return;
            }

            // Detect runners by where the hole actually sits (on the conveyor), not the factory's own
            // track position — otherwise runners peel off at the factory instead of at the hole.
            setup.trackPosition = m_track.ClosestT(go.transform.position);
            hole.Setup(setup, m_mats);
            hole.OnCompleted += HandleHoleCompleted;
            m_track.RegisterHole(hole);
            m_current = hole;

            RefreshPreview(); // show the now-next bundle entry (if any) at the preview slot
        }

        /// <summary>
        /// Show an inert preview of the next hole in the bundle (the entry at <see cref="m_next"/>) at
        /// <see cref="m_nextHolePreviewHolder"/>, replacing any existing preview. Clears the preview
        /// when no holder is wired or the bundle has no further holes to produce. The preview is purely
        /// visual: it is never registered with the track and never accepts runners.
        /// </summary>
        void RefreshPreview()
        {
            if (m_preview != null)
            {
                Destroy(m_preview.gameObject);
                m_preview = null;
            }

            if (m_nextHolePreviewHolder == null || m_holePrefab == null || m_next >= m_bundle.Count)
                return;

            HoleSetup src = m_bundle[m_next];
            var setup = new HoleSetup
            {
                color = src.color,
                requiredCount = src.requiredCount,
                trackPosition = m_trackPosition,
                hidden = src.hidden,
                mechanic = src.mechanic,
                unlockAfterHolesCompleted = src.unlockAfterHolesCompleted,
            };

            var go = Instantiate(m_holePrefab, m_nextHolePreviewHolder);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.name = $"HolePreview_{setup.color}";

            var hole = go.GetComponent<Hole>();
            if (hole == null)
            {
                // The missing-Hole-component case is already reported when the live hole is produced.
                Destroy(go);
                return;
            }

            hole.SetupPreview(setup, m_mats);
            m_preview = hole;
        }

        void HandleHoleCompleted(Hole hole)
        {
            hole.OnCompleted -= HandleHoleCompleted;
            if (m_current == hole) m_current = null;

            // Stop runners targeting it immediately; the visual lingers briefly, then shrinks away.
            m_track?.UnregisterHole(hole);
            StartCoroutine(RetireThenSpawn(hole));
        }

        IEnumerator RetireThenSpawn(Hole hole)
        {
            if (m_retireDelay > 0f) yield return new WaitForSeconds(m_retireDelay);

            yield return TweenUtil.ScaleOut(hole != null ? hole.transform : null, m_retireDuration, () =>
            {
                if (hole != null) Destroy(hole.gameObject);
            });

            SpawnNext();
        }

        void OnDestroy()
        {
            if (m_current != null) m_current.OnCompleted -= HandleHoleCompleted;
            if (m_track != null) m_track.UnregisterFactory(this);
        }
    }
}
