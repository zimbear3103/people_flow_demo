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
        [SerializeField, Tooltip("Ice block that encases an ice factory while it's locked; hidden (after a " +
            "short melt) once enough other holes are completed. See HoleFactorySetup.iceFactory.")]
        private GameObject m_iceFactory;
        [SerializeField, Tooltip("Seconds the ice block takes to shrink away when the factory unlocks.")]
        float m_iceMeltDuration = 0.35f;
        [SerializeField]
        private TextMeshPro m_bundleRemainTxt;
        [SerializeField, Tooltip("Shows how many more holes must be completed before this ice factory unlocks.")]
        private TextMeshPro m_iceUnlockRemainTxt;
        GameObject m_holePrefab;
        MaterialLibrary m_mats;
        RunwayTrack m_track;
        
        [SerializeField] private List<HoleSetup> m_bundle = new List<HoleSetup>();
        
        float m_trackPosition;
        int m_next;          // index of the next bundle entry to produce
        Hole m_current;      // the live hole, or null while none is being produced
        Hole m_preview;      // inert preview of the next bundle entry, shown at m_nextHolePreviewHolder
        bool m_showBundleCount;

        // ---- ice (locked factory) -------------------------------------------
        bool m_isIceFactory;       // this factory starts frozen and holds its bundle until it melts
        int m_iceUnlockAfter;      // OTHER holes that must complete before the ice melts
        int m_iceUnlockRemain;     // live remaining count shown on m_iceUnlockRemainTxt
        int m_observedCompleted;   // global completed-hole count, tracked via OnHoleProgress
        bool m_iceMelted;          // set once the melt has played, so the visual isn't re-toggled
        GameObject m_iceCounterRoot; // the counter badge to show/hide with the ice (the text's badge)

        public Hole Current => m_current;

        // Frozen over: an ice factory whose unlock threshold hasn't been reached yet.
        public bool IsIced => m_isIceFactory && !m_iceMelted && m_observedCompleted < m_iceUnlockAfter;

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

            // Ice (locked factory): starts encased and holds its bundle until enough OTHER holes are
            // completed (mirrors Lane's barrier / Hole's Frozen-Gate unlock, all driven by OnHoleProgress).
            m_isIceFactory = setup.iceFactory && setup.iceUnlockAfterHolesCompleted > 0;
            m_iceUnlockAfter = Mathf.Max(0, setup.iceUnlockAfterHolesCompleted);
            m_iceMelted = false;

            if (m_holePrefab == null)
            {
                Debug.LogError($"HoleFactory '{name}': no hole prefab to produce from — bundle disabled.");
                return;
            }

            m_track.RegisterFactory(this);

            if (GamePlayController.Instance != null)
            {
                GamePlayController.Instance.OnHoleProgress += OnHoleProgress;
                m_observedCompleted = GamePlayController.Instance.CompletedHoles;
            }
            // A level always begins at 0 completed (BeginLevel resets it right after the build); ignore any
            // stale cross-level counter so an ice factory reliably starts iced and never produces early.
            if (m_isIceFactory) m_observedCompleted = 0;

            ResolveIceCounterRoot();
            RefreshIceVisual();

            // Iced factories hold their bundle until the ice melts (see MeltIce); everyone else produces now.
            if (!IsIced) SpawnNext();
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

        // ---- ice (locked factory) -------------------------------------------

        void OnHoleProgress(int completed, int total)
        {
            bool wasIced = IsIced;
            m_observedCompleted = completed;

            if (wasIced && !IsIced)
                MeltIce();          // threshold just reached — melt the ice and start producing
            else
                RefreshIceVisual(); // still iced (or never iced): just refresh the remaining counter
        }

        // Shows the ice block + remaining counter while iced; hides them otherwise. Used for the steady
        // state at setup and on each progress tick; the unlock transition itself goes through MeltIce.
        void RefreshIceVisual()
        {
            if (m_iceFactory != null && !m_iceMelted) m_iceFactory.SetActive(IsIced);
            UpdateIceUnlockRemainTxt();
        }

        void MeltIce()
        {
            m_iceMelted = true;
            UpdateIceUnlockRemainTxt(); // clears/hides the counter (nothing left to unlock)
            Haptics.Light();

            // Shrink the ice block away for a short melt beat, then hide it.
            if (m_iceFactory != null)
                StartCoroutine(Tweener.ScaleOut(m_iceFactory.transform, m_iceMeltDuration, () =>
                {
                    if (m_iceFactory != null) m_iceFactory.SetActive(false);
                }));

            // The factory was holding its bundle while iced — start producing the first hole now.
            if (m_current == null && m_next < m_bundle.Count) SpawnNext();
        }

        // The remain counter lives on a small badge prop. Hide the whole badge (the text's parent) so a
        // non-ice factory built from the same prefab doesn't show an empty badge — but never the factory
        // root itself, so a flat hierarchy falls back to just toggling the text object.
        void ResolveIceCounterRoot()
        {
            if (m_iceUnlockRemainTxt == null) { m_iceCounterRoot = null; return; }
            Transform t = m_iceUnlockRemainTxt.transform;
            Transform parent = t.parent;
            m_iceCounterRoot = (parent != null && parent.GetComponent<HoleFactory>() == null)
                ? parent.gameObject : t.gameObject;
        }

        void UpdateIceUnlockRemainTxt()
        {
            if (m_iceUnlockRemainTxt == null) return;

            bool show = IsIced;
            m_iceUnlockRemain = show ? Mathf.Max(0, m_iceUnlockAfter - m_observedCompleted) : 0;
            m_iceUnlockRemainTxt.text = show ? m_iceUnlockRemain.ToString() : "";

            var root = m_iceCounterRoot != null ? m_iceCounterRoot : m_iceUnlockRemainTxt.gameObject;
            if (root.activeSelf != show) root.SetActive(show);
        }

        void OnDestroy()
        {
            if (m_current != null) m_current.OnCompleted -= HandleHoleCompleted;
            if (m_track != null) m_track.UnregisterFactory(this);
            if (GamePlayController.Instance != null)
                GamePlayController.Instance.OnHoleProgress -= OnHoleProgress;
        }
    }
}
