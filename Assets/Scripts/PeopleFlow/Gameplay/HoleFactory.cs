using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace PeopleFlow
{
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
        [SerializeField]
        private TextMeshPro m_bundleRemainTxt;
        GameObject m_holePrefab;
        MaterialLibrary m_mats;
        RunwayTrack m_track;
        
        [SerializeField] private List<HoleSetup> m_bundle = new List<HoleSetup>();
        float m_trackPosition;
        int m_next;          // index of the next bundle entry to produce
        Hole m_current;      // the live hole, or null while none is being produced
        Hole m_preview;      // inert preview of the next bundle entry, shown at m_nextHolePreviewHolder
        bool m_showBundleCount;

        public Hole Current => m_current;

        public bool IsExhausted => m_next >= m_bundle.Count && m_current == null;

        public bool IsProducing => m_current == null && m_next < m_bundle.Count;
        public List<HoleSetup> Bundle => m_bundle;
        public void Setup(HoleFactorySetup setup, GameObject holePrefab, MaterialLibrary mats,
            RunwayTrack track)
        {
            m_holePrefab = holePrefab;
            m_mats = mats;
            m_track = track;
            m_trackPosition = setup.trackPosition;

            m_bundle.Clear();
            if (setup.bundle != null) m_bundle.AddRange(setup.bundle);
            m_showBundleCount = m_bundle.Count > 1;

            if (m_holePrefab == null)
            {
                Debug.LogError($"HoleFactory '{name}': no hole prefab to produce from — bundle disabled.");
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
                UpdateBundleRemainTxt();
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
                Debug.LogError($"HoleFactory '{name}': hole prefab '{m_holePrefab.name}' has no Hole component — skipping.");
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
            UpdateBundleRemainTxt();
        }

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
            UpdateBundleRemainTxt();
            StartCoroutine(RetireThenSpawn(hole));
        }

        IEnumerator RetireThenSpawn(Hole hole)
        {
            if (m_retireDelay > 0f) yield return new WaitForSeconds(m_retireDelay);

            yield return Tweener.ScaleOut(hole != null ? hole.transform : null, m_retireDuration, () =>
            {
                if (hole != null) Destroy(hole.gameObject);
            });

            SpawnNext();
        }

        void UpdateBundleRemainTxt()
        {
            if (m_bundleRemainTxt == null) return;

            if (!m_showBundleCount)
            {
                m_bundleRemainTxt.text = "";
                return;
            }

            int remain = m_bundle.Count - m_next + (m_current != null ? 1 : 0);
            m_bundleRemainTxt.text = remain > 0 ? remain.ToString() : "";
        }

        void OnDestroy()
        {
            if (m_current != null) m_current.OnCompleted -= HandleHoleCompleted;
            if (m_track != null) m_track.UnregisterFactory(this);
        }
    }
}
