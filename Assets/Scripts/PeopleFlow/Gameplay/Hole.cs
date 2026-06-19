using System;
using UnityEngine;

namespace PeopleFlow
{
    /// <summary>
    /// A coloured hole around the loop. Needs <c>requiredCount</c> matching runners to complete.
    /// Reserves a slot per incoming runner (so two runners never over-fill the last slot) and
    /// supports the section-7 specials: hidden colour, frozen, and gate.
    ///
    /// A hole can stand alone (placed directly by <see cref="LevelManager"/>) or be one item in a
    /// <see cref="HoleFactory"/>'s bundle. The factory listens to <see cref="OnCompleted"/> to know
    /// when to retire this hole and produce the next one in its bundle.
    /// </summary>
    public class Hole : MonoBehaviour
    {
        [SerializeField]
        private GameObject m_holeGameObj; // optional reference to a single renderer to tint for the hole colour (vs. tinting all renderers in the prefab)
        [SerializeField]
        private GameObject m_holeInGameObj;  // optional reference to a single renderer to tint for the pip colour (vs. tinting all renderers in the prefab)
        [SerializeField]
        private PeopleColor m_colorHole = PeopleColor.Red;
        [SerializeField]
        private int m_required = 1;
        [SerializeField]
        private float m_trackT = 0.5f;

        public PeopleColor Color => m_colorHole;
        public int Required => Mathf.Max(1, m_required);
        public int Filled { get; private set; }
        public int Reserved { get; private set; }
        public float TrackT { get; private set; }
        public bool IsComplete { get; private set; }

        /// <summary>World position a runner should hop into: the actual hole opening
        /// (<see cref="m_holeGameObj"/>) if one is assigned, otherwise the hole root. Lets a runner
        /// land in the hole's mouth even when the hole sits offset on a factory conveyor.</summary>
        public Vector3 JumpTarget => m_holeGameObj != null ? m_holeGameObj.transform.position : transform.position;

        /// <summary>Fired once when the hole fills to its required count. The owning
        /// <see cref="HoleFactory"/> (if any) uses this to retire the hole and spawn the next.</summary>
        public event Action<Hole> OnCompleted;

        bool m_hidden;
        bool m_revealed;
        bool m_isPreview;   // a non-interactive "next hole" preview shown by a HoleFactory
        HoleMechanic m_mechanic;
        int m_unlockAfter;
        int m_observedCompleted;

        MaterialLibrary m_mats;
        Renderer[] m_renderers;   // the prefab's body renderers, tinted to the hole's colour
        GameObject m_lockOverlay; // optional named child shown while Frozen/Gate is locked
        ParticleSystem m_burst;

        /// <summary>Locked = a Frozen/Gate hole whose unlock condition has not been met yet.</summary>
        public bool IsLocked => m_mechanic != HoleMechanic.None && m_observedCompleted < m_unlockAfter;

        // ---- setup ----------------------------------------------------------

        public void Setup(HoleSetup setup, MaterialLibrary mats)
        {
            m_mats = mats;
            m_colorHole = setup.color;
            m_required = Mathf.Max(1, setup.requiredCount);
            TrackT = setup.trackPosition;
            m_hidden = setup.hidden;
            m_mechanic = setup.mechanic;
            m_unlockAfter = setup.unlockAfterHolesCompleted;

            if (GamePlayController.Instance != null)
            {
                GamePlayController.Instance.OnHoleProgress += OnHoleProgress;
                m_observedCompleted = GamePlayController.Instance.CompletedHoles;
            }

            BindVisuals();
            RefreshLockVisual();
        }

        /// <summary>
        /// Configure this hole as a non-interactive <em>preview</em> of an upcoming hole: it shows the
        /// colour, fill target and special (hidden / locked) state, but never registers with the track,
        /// accepts runners, fires <see cref="OnCompleted"/>, or reports progress. Used by
        /// <see cref="HoleFactory"/> to show the next hole in its bundle at the preview slot.
        /// </summary>
        public void SetupPreview(HoleSetup setup, MaterialLibrary mats)
        {
            m_mats = mats;
            m_colorHole = setup.color;
            m_required = Mathf.Max(1, setup.requiredCount);
            TrackT = setup.trackPosition;
            m_hidden = setup.hidden;
            m_mechanic = setup.mechanic;
            m_unlockAfter = setup.unlockAfterHolesCompleted;
            m_isPreview = true;

            // Snapshot the completed-hole count so a still-locked upcoming hole previews as locked. We
            // don't subscribe to progress — the preview is replaced by a real hole when it goes live.
            if (GamePlayController.Instance != null)
                m_observedCompleted = GamePlayController.Instance.CompletedHoles;

            BindVisuals(withBurst: false);
            RefreshLockVisual();
        }

        void OnDestroy()
        {
            if (GamePlayController.Instance != null)
                GamePlayController.Instance.OnHoleProgress -= OnHoleProgress;
        }

        // ---- acceptance / reservation --------------------------------------

        public bool CanAccept(PeopleColor c)
            => !m_isPreview && !IsComplete && !IsLocked && c == Color && (Required - Filled - Reserved) > 0;

        /// <summary>Reserve one slot for an incoming runner. Returns false if it cannot be served.</summary>
        public bool TryReserve(PeopleColor c)
        {
            if (!CanAccept(c)) return false;
            Reserved++;
            return true;
        }

        public void CancelReserve()
        {
            if (Reserved > 0) Reserved--;
        }

        /// <summary>A reserved runner has arrived: convert the reservation into a fill.</summary>
        public void Commit()
        {
            if (Reserved > 0) Reserved--;
            Filled = Mathf.Min(Required, Filled + 1);
            ApplyColor();

            AudioManager.Instance?.PlayFill();
            Haptics.Light();

            if (Filled >= Required && !IsComplete)
                Complete();
        }

        void Complete()
        {
            IsComplete = true;
            ApplyColor();
            if (m_burst != null) { m_burst.transform.localScale = Vector3.one; m_burst.Play(); }

            AudioManager.Instance?.PlayHoleComplete();
            Haptics.Success();
            GamePlayController.Instance?.ReportHoleCompleted(this);
            OnCompleted?.Invoke(this);
        }

        /// <summary>Reveal a hidden colour (called by a runner that gets close).</summary>
        public void RevealIfHidden()
        {
            if (!m_hidden || m_revealed) return;
            m_revealed = true;
            ApplyColor();
        }

        // ---- specials -------------------------------------------------------

        void OnHoleProgress(int completed, int total)
        {
            bool wasLocked = IsLocked;
            m_observedCompleted = completed;
            if (wasLocked && !IsLocked)
            {
                RefreshLockVisual();
                StartCoroutine(TweenUtil.ScalePop(transform, transform.localScale, 0.2f));
            }
        }

        void RefreshLockVisual()
        {
            if (m_lockOverlay != null) m_lockOverlay.SetActive(IsLocked);
            ApplyColor();
        }

        void BindVisuals(bool withBurst = true)
        {
            m_renderers = Prim.CollectTintable(gameObject);
            if (m_renderers.Length == 0)
                PFLog.Warn($"Hole prefab '{name}' has no tintable renderers — colour/progress state won't show.");

            var overlay = Prim.FindDescendant(transform, "Ice", "Frozen", "Gate", "Lock");
            if (overlay != null) m_lockOverlay = overlay.gameObject;

            // A preview never completes, so it skips the completion burst.
            if (withBurst) m_burst = Prim.CreateBurst(transform, Color.ToColor());
            ApplyColor();
        }

        void ApplyColor() => Prim.Tint(m_renderers, CurrentMaterial());

        /// <summary>The tint for the current state: ice when locked, grey while a hidden colour is
        /// concealed, the designer-assigned material when one is mapped for this colour, otherwise
        /// the generated hole colour brightening from dim → full as it fills.</summary>
        Material CurrentMaterial()
        {
            if (IsLocked) return m_mats.Ice;
            if (m_hidden && !m_revealed) return m_mats.Hidden;

            if (m_mats.HasColorOverride(Color)) return m_mats.Colored(Color);

            UnityEngine.Color full = Color.ToColor();
            float t = Required > 0 ? (float)Filled / Required : 1f;
            UnityEngine.Color dim = UnityEngine.Color.Lerp(full, ColorPalette.Neutral, 0.55f);
            return m_mats.Solid(UnityEngine.Color.Lerp(dim, full, t));
        }
    }
}
