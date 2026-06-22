using System;
using TMPro;
using UnityEngine;

namespace PeopleFlow
{
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
        [SerializeField]
        private TextMeshPro m_requiredTxt;
        public PeopleColor Color => m_colorHole;
        public int Required => Mathf.Max(1, m_required);
        public int Filled { get; private set; }
        public int Reserved { get; private set; }
        public float TrackT { get; private set; }
        public bool IsComplete { get; private set; }

        public Vector3 JumpTarget => m_holeGameObj != null ? m_holeGameObj.transform.position : transform.position;

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

            if (m_requiredTxt != null)
            {
                m_requiredTxt.gameObject.SetActive(!m_hidden);
            }

            if (GamePlayController.Instance != null)
            {
                GamePlayController.Instance.OnHoleProgress += OnHoleProgress;
                m_observedCompleted = GamePlayController.Instance.CompletedHoles;
            }

            BindVisuals();
            RefreshLockVisual();
            UpdateRequiredText();
        }

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

            if (m_requiredTxt != null)
            {
                m_requiredTxt.gameObject.SetActive(false);
            }

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

        public void Commit()
        {
            if (Reserved > 0) Reserved--;
            Filled = Mathf.Min(Required, Filled + 1);
            ApplyColor();
            UpdateRequiredText();

            Haptics.Light();

            if (Filled >= Required && !IsComplete)
                Complete();
        }

        void Complete()
        {
            IsComplete = true;
            if (m_requiredTxt != null)
            {
                m_requiredTxt.gameObject.SetActive(false);
            }
            ApplyColor();
            if (m_burst != null) { m_burst.transform.localScale = Vector3.one; m_burst.Play(); }

            Haptics.Success();
            GamePlayController.Instance?.ReportHoleCompleted(this);
            OnCompleted?.Invoke(this);
        }

        public void RevealIfHidden()
        {
            if (!m_hidden || m_revealed) return;
            m_revealed = true;
            ApplyColor();
            if (m_requiredTxt != null && !m_isPreview)
            {
                m_requiredTxt.gameObject.SetActive(true);
            }
        }

        void UpdateRequiredText()
        {
            if (m_requiredTxt != null)
            {
                int remaining = Required - Filled;
                m_requiredTxt.text = remaining > 0 ? remaining.ToString() : "";
            }
        }

        // ---- specials -------------------------------------------------------

        void OnHoleProgress(int completed, int total)
        {
            bool wasLocked = IsLocked;
            m_observedCompleted = completed;
            if (wasLocked && !IsLocked)
            {
                RefreshLockVisual();
                StartCoroutine(Tweener.ScalePop(transform, transform.localScale, 0.2f));
            }
        }

        void RefreshLockVisual()
        {
            if (m_lockOverlay != null) m_lockOverlay.SetActive(IsLocked);
            ApplyColor();
        }

        void BindVisuals(bool withBurst = true)
        {
            if (m_holeGameObj != null)
            {
                m_renderers = m_holeGameObj.GetComponentsInChildren<Renderer>(true);
            }
            else
            {
                m_renderers = new Renderer[0];
            }

            if (m_renderers.Length == 0)
                Debug.LogWarning($"Hole prefab '{name}' has no tintable renderers — colour/progress state won't show.");

            var overlay = Prim.FindDescendant(transform, "Ice", "Frozen", "Gate", "Lock");
            if (overlay != null) m_lockOverlay = overlay.gameObject;

            // A preview never completes, so it skips the completion burst.
            if (withBurst) m_burst = Prim.CreateBurst(transform, Color.ToColor());
            ApplyColor();
        }

        void ApplyColor() => Prim.Tint(m_renderers, CurrentMaterial());

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
