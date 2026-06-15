using System.Collections.Generic;
using UnityEngine;

namespace PeopleFlow
{
    /// <summary>
    /// A waiting queue of coloured characters at the bottom of the screen. While the player holds
    /// on this lane, one character is pushed onto the runway every <c>releaseInterval</c> seconds
    /// (subject to runway capacity, spacing and an optional barrier).
    /// </summary>
    public class Lane : MonoBehaviour
    {
        const int MaxPreview = 4;
        const float MinEntrySpacing = 0.95f; // don't release if a runner is still this close to entry

        [SerializeField] float m_releaseInterval = 0.22f;

        readonly Queue<PeopleColor> m_queue = new Queue<PeopleColor>();
        RunwayTrack m_track;
        MaterialLibrary m_mats;
        Transform m_runnersRoot;
        float m_runSpeed;
        Vector3 m_entryPos;

        float m_timer;
        bool m_wasHeld;

        // Barrier (section 7)
        bool m_barrier;
        int m_unlockAfter;
        int m_observedCompleted;
        GameObject m_barrierVisual;

        Renderer[] m_previewSlots;

        /// <summary>Set by <see cref="InputManager"/> every frame: is the player holding this lane?</summary>
        public bool IsHeld { get; set; }

        public bool Barriered => m_barrier && m_observedCompleted < m_unlockAfter;
        public int RemainingCount => m_queue.Count;

        // ---- setup ----------------------------------------------------------

        public void Setup(LaneSetup setup, RunwayTrack track, MaterialLibrary mats,
            Transform runnersRoot, float runSpeed, Vector3 padPosition)
        {
            m_track = track;
            m_mats = mats;
            m_runnersRoot = runnersRoot;
            m_runSpeed = runSpeed;
            m_entryPos = track.Evaluate(RunwayTrack.EntryT);

            if (setup.characters != null)
                foreach (var c in setup.characters) m_queue.Enqueue(c);

            m_barrier = setup.barrier;
            m_unlockAfter = setup.unlockAfterHolesCompleted;

            transform.position = padPosition;
            BuildVisuals();
            UpdatePreview();

            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnHoleProgress += OnHoleProgress;
                m_observedCompleted = GameManager.Instance.CompletedHoles;
                RefreshBarrierVisual();
            }
        }

        void OnDestroy()
        {
            if (GameManager.Instance != null)
                GameManager.Instance.OnHoleProgress -= OnHoleProgress;
        }

        // ---- release loop ---------------------------------------------------

        void Update()
        {
            if (GameManager.Instance == null || GameManager.Instance.State != GameState.Playing)
            {
                m_wasHeld = false;
                return;
            }

            if (IsHeld && !m_wasHeld) m_timer = 0f; // first push fires instantly
            m_wasHeld = IsHeld;

            if (!IsHeld) return;
            m_timer -= Time.deltaTime;
            if (m_timer > 0f) return;

            if (CanRelease())
            {
                ReleaseOne();
                m_timer = m_releaseInterval;
            }
        }

        bool CanRelease()
        {
            return m_queue.Count > 0
                   && !Barriered
                   && !m_track.IsFull
                   && !m_track.HasRunnerNear(m_entryPos, MinEntrySpacing);
        }

        void ReleaseOne()
        {
            var color = m_queue.Dequeue();
            CharacterController_People.Spawn(color, m_track, m_mats, RunwayTrack.EntryT, m_runSpeed, m_runnersRoot);
            AudioManager.Instance?.PlayPush();
            UpdatePreview();
        }

        // ---- barrier --------------------------------------------------------

        void OnHoleProgress(int completed, int total)
        {
            m_observedCompleted = completed;
            RefreshBarrierVisual();
        }

        void RefreshBarrierVisual()
        {
            if (m_barrierVisual != null) m_barrierVisual.SetActive(Barriered);
        }

        // ---- visuals --------------------------------------------------------

        void BuildVisuals()
        {
            // Tap pad (this one keeps its collider so InputManager can raycast it).
            Prim.Create(PrimitiveType.Cube, "Pad", transform,
                new Vector3(0f, 0.05f, 0f), new Vector3(1.1f, 0.1f, 1.1f), m_mats.Neutral, withCollider: true);

            // Preview slots: upcoming colours stacked behind the pad (toward the runway).
            m_previewSlots = new Renderer[MaxPreview];
            for (int i = 0; i < MaxPreview; i++)
            {
                var slot = Prim.Create(PrimitiveType.Cube, "Preview" + i, transform,
                    new Vector3(0f, 0.35f, 0.55f + i * 0.42f), Vector3.one * 0.34f, m_mats.DimPip);
                m_previewSlots[i] = slot.GetComponent<Renderer>();
            }

            if (m_barrier)
            {
                m_barrierVisual = Prim.Create(PrimitiveType.Cube, "Barrier", transform,
                    new Vector3(0f, 0.45f, 0.3f), new Vector3(1.3f, 0.9f, 0.18f), m_mats.Dark);
            }
        }

        void UpdatePreview()
        {
            if (m_previewSlots == null) return;
            var arr = m_queue.ToArray();
            for (int i = 0; i < m_previewSlots.Length; i++)
            {
                bool has = i < arr.Length;
                m_previewSlots[i].gameObject.SetActive(has);
                if (has) m_previewSlots[i].sharedMaterial = m_mats.Colored(arr[i]);
            }
        }
    }
}
