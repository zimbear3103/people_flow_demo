using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

namespace PeopleFlow
{
    /// <summary>
    /// Drop this on a single GameObject in the MainMenu scene. Builds the menu UI in code:
    /// title, Play, a level-select grid (locks levels you haven't unlocked) and audio toggles.
    /// </summary>
    public class MainMenuController : MonoBehaviour
    {
        Font m_font;
        Text m_musicLabel, m_sfxLabel;

        void Awake()
        {
            m_font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            EnsureEventSystem();
            EnsureCameraBackground();
            BuildUI();
        }

        void BuildUI()
        {
            var canvasGo = new GameObject("Menu_Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(540f, 960f);
            scaler.matchWidthOrHeight = 0.5f;
            Transform root = canvasGo.transform;

            var title = MakeText(root, "Title", "PEOPLE FLOW", 60, TextAnchor.MiddleCenter, Color.white);
            Anchor(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -160f), new Vector2(520f, 100f));

            var subtitle = MakeText(root, "Subtitle", "Fill every hole before the loop jams!", 24,
                TextAnchor.MiddleCenter, new Color(1f, 1f, 1f, 0.85f));
            Anchor(subtitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -230f), new Vector2(520f, 50f));

            // Big Play button — plays the next un-cleared level (or level 1).
            MakeButton(root, "PlayButton", "PLAY", new Color(0.4f, 0.82f, 0.5f),
                () => Play(Mathf.Min(SaveManager.HighestUnlocked, DefaultLevels.Count - 1)),
                new Vector2(0.5f, 0.5f), new Vector2(0f, 150f), new Vector2(360f, 110f), 44);

            // Level-select grid.
            var gridLabel = MakeText(root, "SelectLabel", "SELECT LEVEL", 26, TextAnchor.MiddleCenter,
                new Color(1f, 1f, 1f, 0.9f));
            Anchor(gridLabel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 40f), new Vector2(400f, 40f));

            int perRow = 5;
            float btn = 84f, gap = 14f;
            float startX = -((perRow - 1) * (btn + gap)) * 0.5f;
            for (int i = 0; i < DefaultLevels.Count; i++)
            {
                int idx = i;
                bool unlocked = idx <= SaveManager.HighestUnlocked;
                int row = i / perRow, col = i % perRow;
                Color c = unlocked ? new Color(0.5f, 0.62f, 0.95f) : new Color(0.6f, 0.6f, 0.65f);
                var b = MakeButton(root, "Lv" + (i + 1), (i + 1).ToString(), c,
                    () => Play(idx),
                    new Vector2(0.5f, 0.5f),
                    new Vector2(startX + col * (btn + gap), -40f - row * (btn + gap)),
                    new Vector2(btn, btn), 34);
                b.interactable = unlocked;
            }

            // Audio toggles.
            m_musicLabel = AddToggle(root, "Music", -260f, () =>
            {
                AudioManager.Instance?.SetMusicOn(!SaveManager.MusicOn);
                if (AudioManager.Instance == null) SaveManager.MusicOn = !SaveManager.MusicOn;
                RefreshToggleLabels();
            });
            m_sfxLabel = AddToggle(root, "SFX", -330f, () =>
            {
                SaveManager.SfxOn = !SaveManager.SfxOn;
                RefreshToggleLabels();
            });
            RefreshToggleLabels();
        }

        Text AddToggle(Transform root, string name, float y, UnityAction onClick)
        {
            var b = MakeButton(root, name + "Toggle", name, new Color(0.3f, 0.32f, 0.4f), onClick,
                new Vector2(0.5f, 0f), new Vector2(0f, -y), new Vector2(300f, 64f), 28);
            return b.GetComponentInChildren<Text>();
        }

        void RefreshToggleLabels()
        {
            if (m_musicLabel != null) m_musicLabel.text = "Music: " + (SaveManager.MusicOn ? "ON" : "OFF");
            if (m_sfxLabel != null) m_sfxLabel.text = "SFX: " + (SaveManager.SfxOn ? "ON" : "OFF");
        }

        void Play(int levelIndex)
        {
            AudioManager.Instance?.PlayClick();
            GameSession.PlayLevel(levelIndex);
            if (Application.CanStreamedLevelBeLoaded(GameSession.GameScene))
                SceneManager.LoadScene(GameSession.GameScene);
            else
                PFLog.Warn($"Add a scene named '{GameSession.GameScene}' to Build Settings to start playing.");
        }

        void EnsureCameraBackground()
        {
            var cam = Camera.main;
            if (cam == null)
            {
                var go = new GameObject("Main Camera");
                go.tag = "MainCamera";
                cam = go.AddComponent<Camera>();
                go.AddComponent<AudioListener>();
            }
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.36f, 0.55f, 0.78f);
        }

        // ---- small UI factory (self-contained) -----------------------------

        static RectTransform NewUI(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go.GetComponent<RectTransform>();
        }

        Text MakeText(Transform parent, string name, string content, int size, TextAnchor anchor, Color color)
        {
            var rt = NewUI(name, parent);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = m_font;
            t.text = content;
            t.fontSize = size;
            t.alignment = anchor;
            t.color = color;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            return t;
        }

        Button MakeButton(Transform parent, string name, string label, Color bg, UnityAction onClick,
            Vector2 anchor, Vector2 pos, Vector2 size, int fontSize)
        {
            var rt = NewUI(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = bg;
            rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;

            var btn = rt.gameObject.AddComponent<Button>();
            btn.onClick.AddListener(onClick);

            var label2 = MakeText(rt, "Label", label, fontSize, TextAnchor.MiddleCenter, Color.white);
            label2.rectTransform.anchorMin = Vector2.zero;
            label2.rectTransform.anchorMax = Vector2.one;
            label2.rectTransform.offsetMin = Vector2.zero;
            label2.rectTransform.offsetMax = Vector2.zero;
            return btn;
        }

        static void Anchor(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;
        }

        void EnsureEventSystem()
        {
            if (FindFirstObjectByType<EventSystem>() != null) return;
            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
            go.AddComponent<InputSystemUIInputModule>();
#else
            go.AddComponent<StandaloneInputModule>();
#endif
        }
    }
}
