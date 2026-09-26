using System;
using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace LucidCatsBestiary
{
    internal class BestiaryMenu : MonoBehaviour
    {
        private sealed class Row
        {
            public MonsterInfo Info;
            public Image Background;
            public Color BaseColor;
            public TMP_Text Name;
            public TMP_Text Value;
        }

        private readonly List<Row> rows = new List<Row>();

        private Component bestiaryMenu;
        private Component statsMenu;
        private Component settingsMenu;
        private CanvasGroup panelGroup;

        private MonsterViewer viewer;
        private RawImage viewerImage;
        private TMP_Text lockedMark;
        private TMP_Text counterText;
        private TMP_Text nameText;
        private TMP_Text tierText;
        private TMP_Text descriptionText;

        private int selected;
        private int hovered = -1;
        private Color tierTextBaseColor = Color.white;

        private const float HoverTint = 0.15f;
        private const float SelectedTint = 0.3f;

        private static Color TierColor(int tier)
        {
            switch (tier)
            {
                case 1: return new Color(1f, 0.85f, 0.25f);
                case 2: return new Color(1f, 0.5f, 0.05f);
                default: return new Color(1f, 0.15f, 0.15f);
            }
        }

        public static void Create(Scene scene)
        {
            var go = new GameObject("Bestiary (mod)");
            SceneManager.MoveGameObjectToScene(go, scene);
            go.AddComponent<BestiaryMenu>();
        }

        private void Start()
        {
            try
            {
                Build();
                BestiaryPlugin.Log.LogInfo("Bestiary added to the main menu.");
            }
            catch (Exception e)
            {
                BestiaryPlugin.Log.LogError($"Could not build the bestiary menu: {e}");
            }
        }

        private void Update()
        {
            bool open = panelGroup != null && panelGroup.alpha > 0.01f;
            if (viewer != null)
                viewer.SetRendering(open && viewerImage != null && viewerImage.enabled);

            float blend = 1f - Mathf.Exp(-Time.unscaledDeltaTime * 20f);
            for (int i = 0; i < rows.Count; i++)
            {
                Image background = rows[i].Background;
                if (background != null)
                    background.color = Color.Lerp(background.color, RowColor(i), blend);
            }
        }

        private void Build()
        {
            Transform root = FindMenuRoot();
            if (root == null)
                throw new Exception("Could not find 'Canvas/4x3' in the main menu.");

            Transform statsButton = Require(root, "Margins/grid/UIButton (stats)");
            Transform settingsButton = root.Find("Margins/grid/UIButton (settings)");
            Transform statsPanel = Require(root, "Stats menu");
            Transform settingsPanel = root.Find("Settings Menu");

            UiSounds.CaptureFrom(statsButton.gameObject);

            statsMenu = FindMenuComponent(statsPanel.gameObject);
            settingsMenu = settingsPanel != null ? FindMenuComponent(settingsPanel.gameObject) : null;

            BuildButton(statsButton, settingsButton);
            BuildPanel(statsPanel);
            Refresh();
        }

        private Transform FindMenuRoot()
        {
            foreach (GameObject go in gameObject.scene.GetRootGameObjects())
            {
                if (go.name != "Canvas")
                    continue;
                Transform found = go.transform.Find("4x3");
                if (found != null)
                    return found;
            }
            return null;
        }

        private void BuildButton(Transform statsButton, Transform settingsButton)
        {
            GameObject button = Instantiate(statsButton.gameObject, statsButton.parent, false);
            button.name = "UIButton (bestiary)";
            button.transform.SetSiblingIndex(statsButton.GetSiblingIndex() + 1);

            TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
            if (label != null)
                label.text = "Bestiary";

            UnityEvent click = FindClickEvent(button);
            if (click == null)
                throw new Exception("Could not find the click event of the Bestiary button.");

            for (int i = 0; i < click.GetPersistentEventCount(); i++)
                click.SetPersistentListenerState(i, UnityEventCallState.Off);
            click.AddListener(OnBestiaryClicked);

            FindClickEvent(statsButton.gameObject)?.AddListener(CloseBestiary);
            if (settingsButton != null)
                FindClickEvent(settingsButton.gameObject)?.AddListener(CloseBestiary);
        }

        private void BuildPanel(Transform statsPanel)
        {
            var holder = new GameObject("Bestiary Holder");
            holder.SetActive(false);

            GameObject panel = Instantiate(statsPanel.gameObject, holder.transform, false);
            panel.name = "Bestiary menu";
            foreach (Game.UI.LifetimeStatsDisplay stats in panel.GetComponentsInChildren<Game.UI.LifetimeStatsDisplay>(true))
                DestroyImmediate(stats);

            Transform title = panel.transform.Find("Text (TMP)");
            Transform grid = Require(panel.transform, "grid");
            Transform headerTemplate = Require(grid, "Text name");
            Transform rowTemplate = Require(grid, "stat display");
            Transform rowTextTemplate = Require(rowTemplate, "Text name");

            if (title != null && title.GetComponent<TMP_Text>() != null)
                title.GetComponent<TMP_Text>().text = "Bestiary";

            var oldChildren = new List<GameObject>();
            foreach (Transform child in grid)
                oldChildren.Add(child.gameObject);

            GameObject header = Instantiate(headerTemplate.gameObject, grid, false);
            header.name = "Counter";
            counterText = header.GetComponent<TMP_Text>();

            for (int i = 0; i < MonsterInfo.All.Length; i++)
                rows.Add(CreateRow(rowTemplate, grid, i));

            var gridRect = (RectTransform)grid;
            Vector2 gridOffsetMin = gridRect.offsetMin;
            Vector2 gridOffsetMax = gridRect.offsetMax;
            gridRect.anchorMax = new Vector2(0.42f, gridRect.anchorMax.y);
            gridRect.offsetMin = gridOffsetMin;
            gridRect.offsetMax = gridOffsetMax;

            RectTransform details = NewRect("Details", panel.transform);
            details.anchorMin = new Vector2(0.45f, 0f);
            details.anchorMax = new Vector2(1f, 1f);
            details.offsetMin = new Vector2(0f, gridOffsetMin.y);
            details.offsetMax = new Vector2(gridOffsetMax.x, gridOffsetMax.y);

            RectTransform viewerArea = NewRect("Viewer Area", details);
            Stretch(viewerArea, 0f, 0.40f, 1f, 1f);

            RectTransform viewerRect = NewRect("Viewer", viewerArea);
            Stretch(viewerRect, 0f, 0f, 1f, 1f);
            viewerRect.gameObject.AddComponent<CanvasRenderer>();
            viewerImage = viewerRect.gameObject.AddComponent<RawImage>();
            viewerImage.raycastTarget = true;
            AspectRatioFitter fitter = viewerRect.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fitter.aspectRatio = 1f;

            viewer = MonsterViewer.Create(gameObject.scene);
            viewerImage.texture = viewer.Texture;
            viewerRect.gameObject.AddComponent<ViewerDragHandler>().Viewer = viewer;

            lockedMark = CloneText(headerTemplate, viewerArea, "Locked Mark");
            Stretch(lockedMark.rectTransform, 0f, 0f, 1f, 1f);
            lockedMark.text = "?";
            lockedMark.alignment = TextAlignmentOptions.Center;
            lockedMark.enableAutoSizing = false;
            lockedMark.fontSize *= 5f;

            nameText = CloneText(headerTemplate, details, "Monster Name");
            Stretch(nameText.rectTransform, 0f, 0.30f, 1f, 0.40f);
            nameText.alignment = TextAlignmentOptions.Left;

            tierText = CloneText(rowTextTemplate, details, "Monster Tier");
            Stretch(tierText.rectTransform, 0f, 0.24f, 1f, 0.30f);
            tierText.alignment = TextAlignmentOptions.Left;
            tierTextBaseColor = tierText.color;

            descriptionText = CloneText(rowTextTemplate, details, "Monster Description");
            Stretch(descriptionText.rectTransform, 0f, 0f, 1f, 0.24f);
            descriptionText.alignment = TextAlignmentOptions.TopLeft;
            descriptionText.textWrappingMode = TextWrappingModes.Normal;
            descriptionText.overflowMode = TextOverflowModes.Overflow;
            descriptionText.enableAutoSizing = false;

            foreach (GameObject old in oldChildren)
                DestroyImmediate(old);

            panel.transform.SetParent(statsPanel.parent, false);
            panel.transform.SetSiblingIndex(statsPanel.GetSiblingIndex() + 1);
            Destroy(holder);

            panelGroup = panel.GetComponent<CanvasGroup>();
            bestiaryMenu = FindMenuComponent(panel);
            if (bestiaryMenu == null)
                throw new Exception("The cloned panel has no Menu component.");
        }

        private Row CreateRow(Transform template, Transform parent, int index)
        {
            GameObject go = Instantiate(template.gameObject, parent, false);
            MonsterInfo info = MonsterInfo.All[index];
            go.name = "Row " + info.Id;

            var row = new Row
            {
                Info = info,
                Background = go.GetComponent<Image>(),
                Name = FindText(go.transform, "Text name"),
                Value = FindText(go.transform, "Text value"),
            };

            if (row.Background != null)
            {
                row.BaseColor = row.Background.color;
                row.Background.raycastTarget = true;
            }

            Button button = go.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            if (row.Background != null)
                button.targetGraphic = row.Background;
            button.onClick.AddListener(() =>
            {
                UiSounds.PlayClick();
                Select(index);
            });

            RowHover hover = go.AddComponent<RowHover>();
            hover.Entered = () =>
            {
                hovered = index;
                UiSounds.PlayHover();
            };
            hover.Exited = () =>
            {
                if (hovered == index)
                    hovered = -1;
            };

            return row;
        }


        private void OnBestiaryClicked()
        {
            hovered = -1;
            Refresh();
            CallMenu(statsMenu, "Close");
            CallMenu(settingsMenu, "Close");
            CallMenu(bestiaryMenu, "Toggle");
        }

        private void CloseBestiary()
        {
            CallMenu(bestiaryMenu, "Close");
        }

        private void Select(int index)
        {
            selected = index;
            UpdateDetails();
        }

        private void Refresh()
        {
            int unlocked = 0;
            foreach (Row row in rows)
            {
                bool isUnlocked = BestiaryData.IsUnlocked(row.Info.Id);
                if (isUnlocked)
                    unlocked++;

                if (row.Name != null)
                    row.Name.text = isUnlocked ? row.Info.DisplayName : "???";
                if (row.Value != null)
                {
                    row.Value.text = isUnlocked ? $"Tier {row.Info.Tier}" : string.Empty;
                    row.Value.color = TierColor(row.Info.Tier);
                }
            }

            if (counterText != null)
                counterText.text = $"MONSTERS {unlocked}/{rows.Count}";

            UpdateDetails();
            UpdateRowHighlights();
        }

        private void UpdateDetails()
        {
            if (rows.Count == 0)
                return;

            MonsterInfo info = rows[selected].Info;
            bool isUnlocked = BestiaryData.IsUnlocked(info.Id);

            nameText.text = isUnlocked ? info.DisplayName.ToUpperInvariant() : "???";
            tierText.text = isUnlocked ? $"Tier {info.Tier}" : "Not encountered yet";
            tierText.color = isUnlocked ? TierColor(info.Tier) : tierTextBaseColor;
            descriptionText.text = isUnlocked
                ? info.Description
                : "You haven't seen this monster yet. Keep dreaming...";

            lockedMark.gameObject.SetActive(!isUnlocked);

            bool showModel = false;
            if (viewer != null)
            {
                if (isUnlocked)
                    showModel = viewer.Show(info.PrefabName);
                else
                    viewer.Hide();
            }
            viewerImage.enabled = showModel;
        }

        private void UpdateRowHighlights()
        {
            for (int i = 0; i < rows.Count; i++)
                if (rows[i].Background != null)
                    rows[i].Background.color = RowColor(i);
        }

        private Color RowColor(int index)
        {
            Color c = rows[index].BaseColor;
            if (index == selected)
                return Tint(c, SelectedTint, 0.6f);
            if (index == hovered)
                return Tint(c, HoverTint, 0.45f);
            return c;
        }

        private static Color Tint(Color c, float amount, float minAlpha)
        {
            return new Color(
                Mathf.Lerp(c.r, 1f, amount),
                Mathf.Lerp(c.g, 1f, amount),
                Mathf.Lerp(c.b, 1f, amount),
                Mathf.Max(c.a, minAlpha));
        }


        private static Transform Require(Transform parent, string path)
        {
            Transform found = parent.Find(path);
            if (found == null)
                throw new Exception($"Could not find '{path}' under '{parent.name}'.");
            return found;
        }

        private static TMP_Text FindText(Transform parent, string childName)
        {
            Transform child = parent.Find(childName);
            return child != null ? child.GetComponent<TMP_Text>() : null;
        }

        private static TMP_Text CloneText(Transform template, Transform parent, string newName)
        {
            GameObject go = Instantiate(template.gameObject, parent, false);
            go.name = newName;
            return go.GetComponent<TMP_Text>();
        }

        private static RectTransform NewRect(string rectName, Transform parent)
        {
            var go = new GameObject(rectName, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        private static void Stretch(RectTransform rect, float minX, float minY, float maxX, float maxY)
        {
            rect.anchorMin = new Vector2(minX, minY);
            rect.anchorMax = new Vector2(maxX, maxY);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static Component FindMenuComponent(GameObject go)
        {
            foreach (MonoBehaviour mb in go.GetComponents<MonoBehaviour>())
            {
                if (mb == null)
                    continue;
                for (Type t = mb.GetType(); t != null; t = t.BaseType)
                    if (t.FullName == "HaniUtils.UI.Menu")
                        return mb;
            }
            return null;
        }

        private static UnityEvent FindClickEvent(GameObject go)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            foreach (MonoBehaviour mb in go.GetComponents<MonoBehaviour>())
            {
                if (mb == null)
                    continue;
                for (Type t = mb.GetType(); t != null && t != typeof(MonoBehaviour); t = t.BaseType)
                {
                    FieldInfo field = t.GetField("onClick", flags | BindingFlags.DeclaredOnly);
                    if (field != null && field.GetValue(mb) is UnityEvent unityEvent)
                        return unityEvent;
                }
            }
            return null;
        }

        private static void CallMenu(Component menu, string methodName)
        {
            if (menu == null)
                return;

            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            try
            {
                for (Type t = menu.GetType(); t != null; t = t.BaseType)
                {
                    MethodInfo method = t.GetMethod(methodName, flags | BindingFlags.DeclaredOnly, null, Type.EmptyTypes, null);
                    if (method != null)
                    {
                        method.Invoke(menu, null);
                        return;
                    }
                }
                BestiaryPlugin.Log.LogWarning($"Menu method '{methodName}' not found on {menu.GetType().Name}.");
            }
            catch (Exception e)
            {
                BestiaryPlugin.Log.LogWarning($"Could not call {methodName} on {menu.GetType().Name}: {e.InnerException?.Message ?? e.Message}");
            }
        }
    }

    internal class RowHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public Action Entered;
        public Action Exited;

        public void OnPointerEnter(PointerEventData eventData) => Entered?.Invoke();

        public void OnPointerExit(PointerEventData eventData) => Exited?.Invoke();
    }
}
