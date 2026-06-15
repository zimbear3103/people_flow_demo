using UnityEngine;

namespace PeopleFlow
{
    /// <summary>
    /// A coloured hole around the loop. Needs <c>requiredCount</c> matching runners to complete.
    /// Reserves a slot per incoming runner (so two runners never over-fill the last slot) and
    /// supports the section-7 specials: hidden colour, frozen, and gate.
    /// </summary>
    public class Hole : MonoBehaviour
    {
        public PeopleColor Color { get; private set; }
        public int Required { get; private set; }
        public int Filled { get; private set; }
        public int Reserved { get; private set; }
        public float TrackT { get; private set; }
        public bool IsComplete { get; private set; }

        bool m_hidden;
        bool m_revealed;
        HoleMechanic m_mechanic;
        int m_unlockAfter;
        int m_observedCompleted;

        MaterialLibrary m_mats;
        Renderer m_ring;
        Renderer m_innerDisc;
        Renderer[] m_pips;
        GameObject m_lockOverlay;
        ParticleSystem m_burst;

        /// <summary>Locked = a Frozen/Gate hole whose unlock condition has not been met yet.</summary>
        public bool IsLocked => m_mechanic != HoleMechanic.None && m_observedCompleted < m_unlockAfter;

        // ---- setup ----------------------------------------------------------

        public void Setup(HoleSetup setup, MaterialLibrary mats)
        {
            m_mats = mats;
            Color = setup.color;
            Required = Mathf.Max(1, setup.requiredCount);
            TrackT = setup.trackPosition;
            m_hidden = setup.hidden;
            m_mechanic = setup.mechanic;
            m_unlockAfter = setup.unlockAfterHolesCompleted;

            BuildVisuals();
            RefreshLockVisual();

            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnHoleProgress += OnHoleProgress;
                m_observedCompleted = GameManager.Instance.CompletedHoles;
            }
        }

        void OnDestroy()
        {
            if (GameManager.Instance != null)
                GameManager.Instance.OnHoleProgress -= OnHoleProgress;
        }

        // ---- acceptance / reservation --------------------------------------

        public bool CanAccept(PeopleColor c)
            => !IsComplete && !IsLocked && c == Color && (Required - Filled - Reserved) > 0;

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
            UpdatePips();

            AudioManager.Instance?.PlayFill();
            Haptics.Light();

            if (Filled >= Required && !IsComplete)
                Complete();
        }

        void Complete()
        {
            IsComplete = true;
            if (m_innerDisc != null) m_innerDisc.sharedMaterial = m_mats.Colored(Color);
            if (m_burst != null) { m_burst.transform.localScale = Vector3.one; m_burst.Play(); }

            AudioManager.Instance?.PlayHoleComplete();
            Haptics.Success();
            GameManager.Instance?.ReportHoleCompleted(this);
        }

        /// <summary>Reveal a hidden colour (called by a runner that gets close).</summary>
        public void RevealIfHidden()
        {
            if (!m_hidden || m_revealed) return;
            m_revealed = true;
            UpdateRingColor();
            UpdatePips();
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
        }

        // ---- visuals --------------------------------------------------------

        void BuildVisuals()
        {
            // Coloured rim (flat cylinder) + dark inner disc that reads as the actual hole.
            var ringGo = Prim.Create(PrimitiveType.Cylinder, "Ring", transform,
                new Vector3(0f, 0.03f, 0f), new Vector3(1.35f, 0.06f, 1.35f), RingMaterial());
            m_ring = ringGo.GetComponent<Renderer>();

            var inner = Prim.Create(PrimitiveType.Cylinder, "Inner", transform,
                new Vector3(0f, 0.05f, 0f), new Vector3(1.0f, 0.06f, 1.0f), m_mats.Dark);
            m_innerDisc = inner.GetComponent<Renderer>();

            // Progress pips in a row floating above the hole.
            m_pips = new Renderer[Required];
            float spacing = 0.26f;
            float startX = -(Required - 1) * 0.5f * spacing;
            for (int i = 0; i < Required; i++)
            {
                var pip = Prim.Create(PrimitiveType.Sphere, "Pip" + i, transform,
                    new Vector3(startX + i * spacing, 0.85f, 0f), Vector3.one * 0.16f, m_mats.DimPip);
                m_pips[i] = pip.GetComponent<Renderer>();
            }

            // Lock overlay: an ice dome (Frozen) or barred cubes (Gate).
            if (m_mechanic == HoleMechanic.Frozen)
            {
                m_lockOverlay = Prim.Create(PrimitiveType.Sphere, "Ice", transform,
                    new Vector3(0f, 0.25f, 0f), new Vector3(1.5f, 0.9f, 1.5f), m_mats.Ice);
            }
            else if (m_mechanic == HoleMechanic.Gate)
            {
                m_lockOverlay = new GameObject("Gate");
                m_lockOverlay.transform.SetParent(transform, false);
                for (int i = -1; i <= 1; i++)
                {
                    Prim.Create(PrimitiveType.Cube, "Bar", m_lockOverlay.transform,
                        new Vector3(i * 0.4f, 0.35f, 0f), new Vector3(0.12f, 0.7f, 1.5f), m_mats.Dark);
                }
            }

            m_burst = Prim.CreateBurst(transform, Color.ToColor());
            UpdatePips();
        }

        Material RingMaterial()
            => (m_hidden && !m_revealed) ? m_mats.Hidden : m_mats.Colored(Color);

        void UpdateRingColor()
        {
            if (m_ring != null) m_ring.sharedMaterial = RingMaterial();
        }

        void UpdatePips()
        {
            if (m_pips == null) return;
            Material filledMat = (m_hidden && !m_revealed) ? m_mats.Hidden : m_mats.Colored(Color);
            for (int i = 0; i < m_pips.Length; i++)
            {
                if (m_pips[i] != null)
                    m_pips[i].sharedMaterial = i < Filled ? filledMat : m_mats.DimPip;
            }
        }
    }
}
