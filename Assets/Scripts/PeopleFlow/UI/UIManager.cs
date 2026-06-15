using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

namespace PeopleFlow
{
    /// <summary>
    /// Builds the whole in-game UI in code (so there is nothing to lay out by hand) and keeps it
    /// in sync with the game via events: HUD (level / time / hole progress / runway fill / pause)
    /// plus Win, Lose and Pause popups. Uses the built-in LegacyRuntime font — no TMP import needed.
    /// </summary>
    public class UIManager : MonoBehaviour
    {
        public static UIManager Instance { get; private set; }

        Font m_font;
        Text m_levelText, m_timeText, m_progressText, m_loseReasonText;
        Image m_timeFill, m_runwayFill;
        GameObject m_pausePanel, m_winPanel, m_losePanel;

        Timer m_timer;
        RunwayTrack m_track;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            m_font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            EnsureEventSystem();
            BuildUI();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            Unsubscribe();
        }

        // ---- binding --------------------------------------------------------

        public void Bind(LevelData level, Timer timer, RunwayTrack track)
        {
            Unsubscribe();
            m_timer = timer;
            m_track = track;

            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnHoleProgress += OnHoleProgress;
                GameManager.Instance.OnLevelWin += OnWin;
                GameManager.Instance.OnLevelLose += OnLose;
            }
            if (m_timer != null) m_timer.OnTick += OnTick;
            if (m_track != null) m_track.OnFillChanged += OnFill;

            m_levelText.text = "LEVEL " + level.levelNumber;
            OnHoleProgress(0, level.TotalHoles);
            OnTick(level.timeLimit, level.timeLimit);
            OnFill(0f);
            m_pausePanel.SetActive(false);
            m_winPanel.SetActive(false);
            m_losePanel.SetActive(false);
        }

        void Unsubscribe()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnHoleProgress -= OnHoleProgress;
                GameManager.Instance.OnLevelWin -= OnWin;
                GameManager.Instance.OnLevelLose -= OnLose;
            }
            if (m_timer != null) m_timer.OnTick -= OnTick;
            if (m_track != null) m_track.OnFillChanged -= OnFill;
        }

        // ---- event handlers -------------------------------------------------

        void OnHoleProgress(int completed, int total)
        {
            if (m_progressText != null) m_progressText.text = $"HOLES  {completed}/{total}";
        }

        void OnTick(float remaining, float total)
        {
            int secs = Mathf.CeilToInt(remaining);
            if (m_timeText != null) m_timeText.text = $"{secs / 60}:{secs % 60:00}";
            if (m_timeFill != null)
            {
                float k = total > 0f ? remaining / total : 0f;
                SetHorizontalFill(m_timeFill.rectTransform, k);
                m_timeFill.color = Color.Lerp(new Color(0.95f, 0.4f, 0.4f), new Color(0.4f, 0.85f, 0.5f), k);
            }
        }

        void OnFill(float fill)
        {
            if (m_runwayFill == null) return;
            SetVerticalFill(m_runwayFill.rectTransform, fill);
            m_runwayFill.color = Color.Lerp(new Color(0.45f, 0.8f, 0.95f), new Color(0.95f, 0.35f, 0.35f), fill);
        }

        void OnWin() => m_winPanel.SetActive(true);

        void OnLose(LoseReason reason)
        {
            if (m_loseReasonText != null)
                m_loseReasonText.text = reason == LoseReason.TimeOut ? "Out of time!" : "The runway jammed!";
            m_losePanel.SetActive(true);
        }

        // ---- button actions -------------------------------------------------

        void OnPause()
        {
            AudioManager.Instance?.PlayClick();
            GameManager.Instance.Pause();
            m_pausePanel.SetActive(true);
        }

        void OnResume()
        {
            AudioManager.Instance?.PlayClick();
            m_pausePanel.SetActive(false);
            GameManager.Instance.Resume();
        }

        // ---- UI construction ------------------------------------------------

        void BuildUI()
        {
            var canvasGo = new GameObject("PF_Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(540f, 960f);
            scaler.matchWidthOrHeight = 0.5f;
            Transform root = canvasGo.transform;

            // ---- HUD ----
            m_levelText = MakeText(root, "LevelText", "LEVEL 1", 34, TextAnchor.UpperCenter);
            Anchor(m_levelText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -70f), new Vector2(400f, 60f));

            m_progressText = MakeText(root, "ProgressText", "HOLES 0/0", 30, TextAnchor.UpperLeft);
            Anchor(m_progressText.rectTransform, new Vector2(0f, 1f), new Vector2(30f, -130f), new Vector2(300f, 50f), new Vector2(0f, 1f));

            m_timeText = MakeText(root, "TimeText", "0:00", 30, TextAnchor.UpperRight);
            Anchor(m_timeText.rectTransform, new Vector2(1f, 1f), new Vector2(-30f, -130f), new Vector2(220f, 50f), new Vector2(1f, 1f));

            // Horizontal time bar under the header.
            var timeBg = MakeImage(root, "TimeBarBg", new Color(0f, 0f, 0f, 0.18f));
            Anchor(timeBg.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -185f), new Vector2(460f, 18f));
            m_timeFill = MakeImage(timeBg.transform, "TimeBarFill", new Color(0.4f, 0.85f, 0.5f));
            StretchFull(m_timeFill.rectTransform);

            // Vertical runway-fill bar on the right edge.
            var fillBg = MakeImage(root, "RunwayBarBg", new Color(0f, 0f, 0f, 0.18f));
            Anchor(fillBg.rectTransform, new Vector2(1f, 0.5f), new Vector2(-26f, 40f), new Vector2(34f, 360f), new Vector2(1f, 0.5f));
            m_runwayFill = MakeImage(fillBg.transform, "RunwayBarFill", new Color(0.45f, 0.8f, 0.95f));
            StretchFull(m_runwayFill.rectTransform);
            var fillLabel = MakeText(fillBg.transform, "FillLabel", "FULL", 18, TextAnchor.UpperCenter);
            Anchor(fillLabel.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, 30f), new Vector2(120f, 30f));

            // Pause button (top-right corner).
            MakeButton(root, "PauseButton", "II", new Color(0.2f, 0.2f, 0.25f, 0.85f), OnPause,
                new Vector2(1f, 1f), new Vector2(-46f, -46f), new Vector2(72f, 72f));

            // ---- popups ----
            m_pausePanel = MakePopup(root, "PausePanel", "PAUSED", out _);
            AddPopupButton(m_pausePanel.transform, "Resume", new Color(0.4f, 0.8f, 0.5f), -10f, OnResume);
            AddPopupButton(m_pausePanel.transform, "Restart", new Color(0.95f, 0.7f, 0.3f), -110f,
                () => { AudioManager.Instance?.PlayClick(); GameManager.Instance.Restart(); });
            AddPopupButton(m_pausePanel.transform, "Menu", new Color(0.5f, 0.6f, 0.95f), -210f,
                () => { AudioManager.Instance?.PlayClick(); GameManager.Instance.GoToMenu(); });

            m_winPanel = MakePopup(root, "WinPanel", "LEVEL COMPLETE!", out _);
            AddPopupButton(m_winPanel.transform, "Next", new Color(0.4f, 0.8f, 0.5f), -20f,
                () => { AudioManager.Instance?.PlayClick(); GameManager.Instance.GoToNextLevel(); });
            AddPopupButton(m_winPanel.transform, "Menu", new Color(0.5f, 0.6f, 0.95f), -120f,
                () => { AudioManager.Instance?.PlayClick(); GameManager.Instance.GoToMenu(); });

            m_losePanel = MakePopup(root, "LosePanel", "LEVEL FAILED", out m_loseReasonText);
            AddPopupButton(m_losePanel.transform, "Retry", new Color(0.95f, 0.7f, 0.3f), -20f,
                () => { AudioManager.Instance?.PlayClick(); GameManager.Instance.Restart(); });
            AddPopupButton(m_losePanel.transform, "Menu", new Color(0.5f, 0.6f, 0.95f), -120f,
                () => { AudioManager.Instance?.PlayClick(); GameManager.Instance.GoToMenu(); });

            m_pausePanel.SetActive(false);
            m_winPanel.SetActive(false);
            m_losePanel.SetActive(false);
        }

        GameObject MakePopup(Transform parent, string name, string title, out Text subtitle)
        {
            var dim = MakeImage(parent, name, new Color(0f, 0f, 0f, 0.55f));
            StretchFull(dim.rectTransform);

            var card = MakeImage(dim.transform, "Card", new Color(0.98f, 0.98f, 1f));
            Anchor(card.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(420f, 520f));

            var titleText = MakeText(card.transform, "Title", title, 40, TextAnchor.UpperCenter, Color.black);
            Anchor(titleText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -50f), new Vector2(380f, 70f));

            subtitle = MakeText(card.transform, "Subtitle", "", 26, TextAnchor.UpperCenter, new Color(0.3f, 0.3f, 0.35f));
            Anchor(subtitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -120f), new Vector2(360f, 50f));

            return dim.gameObject;
        }

        void AddPopupButton(Transform popupRoot, string label, Color color, float yFromBottom, UnityAction onClick)
        {
            // Buttons are parented to the card (the popup's first child).
            var card = popupRoot.GetChild(0);
            MakeButton(card, label + "Button", label, color, onClick,
                new Vector2(0.5f, 0f), new Vector2(0f, 120f + yFromBottom + 100f), new Vector2(300f, 84f),
                Color.white, 32);
        }

        // ---- low-level UI factory ------------------------------------------

        static RectTransform NewUI(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go.GetComponent<RectTransform>();
        }

        Text MakeText(Transform parent, string name, string content, int size, TextAnchor anchor,
            Color? color = null)
        {
            var rt = NewUI(name, parent);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = m_font;
            t.text = content;
            t.fontSize = size;
            t.alignment = anchor;
            t.color = color ?? Color.white;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            return t;
        }

        static Image MakeImage(Transform parent, string name, Color color)
        {
            var rt = NewUI(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            return img;
        }

        Button MakeButton(Transform parent, string name, string label, Color bg, UnityAction onClick,
            Vector2 anchor, Vector2 anchoredPos, Vector2 size, Color? textColor = null, int fontSize = 30)
        {
            var img = MakeImage(parent, name, bg);
            Anchor(img.rectTransform, anchor, anchoredPos, size, anchor);
            var btn = img.gameObject.AddComponent<Button>();
            btn.onClick.AddListener(onClick);

            var t = MakeText(img.transform, "Label", label, fontSize, TextAnchor.MiddleCenter, textColor ?? Color.white);
            StretchFull(t.rectTransform);
            return btn;
        }

        // ---- RectTransform helpers -----------------------------------------

        static void Anchor(RectTransform rt, Vector2 anchorPivot, Vector2 anchoredPos, Vector2 size)
            => Anchor(rt, anchorPivot, anchoredPos, size, anchorPivot);

        static void Anchor(RectTransform rt, Vector2 anchor, Vector2 anchoredPos, Vector2 size, Vector2 pivot)
        {
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.sizeDelta = size;
            rt.anchoredPosition = anchoredPos;
        }

        static void StretchFull(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        static void SetHorizontalFill(RectTransform rt, float k)
        {
            k = Mathf.Clamp01(k);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = new Vector2(k, 1f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        static void SetVerticalFill(RectTransform rt, float k)
        {
            k = Mathf.Clamp01(k);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = new Vector2(1f, k);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        void EnsureEventSystem()
        {
            if (FindFirstObjectByType<EventSystem>() != null) return;
            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
            // A module added at runtime has no actions until we assign the defaults, or UI clicks
            // silently do nothing.
            go.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();
#else
            go.AddComponent<StandaloneInputModule>();
#endif
        }
    }
}
