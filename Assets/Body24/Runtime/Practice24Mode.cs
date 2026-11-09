using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Body24
{
    // Separate puzzle/input state leaves the floor game's layout and rules intact.
    public sealed class Practice24Mode : MonoBehaviour
    {
        Body24App app;
        GameObject panel;
        RectTransform canvas;
        TMP_Text stats, hint;
        readonly PuzzleModel puzzle = new PuzzleModel();
        readonly System.Random random = new System.Random();
        readonly List<HoldTarget> targets = new List<HoldTarget>();
        readonly HoldTarget[] cards = new HoldTarget[4], operators = new HoldTarget[4];
        readonly CapsuleCollider[] fingers = new CapsuleCollider[2];
        readonly HoldTarget[] touching = new HoldTarget[2];
        Vector3 facing;
        int solved;
        float sessionTime, puzzleTime, solvedTime, nextDealAt;
        bool active;
        public void Begin(Body24App owner)
        {
            app = owner;
            if (!panel) Build();
            solved = 0; sessionTime = puzzleTime = solvedTime = nextDealAt = 0;
            touching[0] = touching[1] = null;
            active = true; panel.SetActive(true);
            facing = Forward(); Place(true);
            app.input.fingerSelectionPlane = panel.transform;
            app.cards[0].transform.parent.gameObject.SetActive(false);
            Deal();
        }
        public void End()
        {
            active = false;
            if (panel) panel.SetActive(false);
            foreach (var f in fingers) if (f) f.gameObject.SetActive(false);
            if (app)
            {
                app.input.fingerSelectionPlane = null;
                app.cards[0].transform.parent.gameObject.SetActive(true);
                app.guides.gameObject.SetActive(true);
            }
        }
        Vector3 Forward()
        {
            var forward = Vector3.ProjectOnPlane(app.input.head.transform.forward, Vector3.up);
            return forward.sqrMagnitude > .01f ? forward.normalized : (facing.sqrMagnitude > .01f ? facing : Vector3.forward);
        }
        void Place(bool immediately)
        {
            var forward = Forward();
            if (Vector3.Angle(facing, forward) > app.config.snapAngle) facing = forward;
            // Offset is world-vertical, independent of where the player looks.
            panel.transform.position = app.input.head.transform.position + Vector3.down * .2f + facing * .35f;
            var rotation = Quaternion.LookRotation(facing, Vector3.up);
            panel.transform.rotation = immediately ? rotation : Quaternion.Slerp(panel.transform.rotation, rotation, 1 - Mathf.Exp(-app.config.followSpeed * Time.unscaledDeltaTime));
        }
        void LateUpdate()
        {
            if (!active) return;
            if (!app.input.HeadValid)
            {
                foreach (var f in fingers) f.gameObject.SetActive(false);
                touching[0] = touching[1] = null;
                return;
            }
            Place(false);
            sessionTime += Time.unscaledDeltaTime;
            if (!puzzle.Solved) puzzleTime += Time.unscaledDeltaTime;
            if (nextDealAt > 0 && sessionTime >= nextDealAt) Deal();
            stats.text = "SOLVED  " + solved + "     SESSION  " + Clock(sessionTime) + "     AVG  " + (solved == 0 ? "—" : (solvedTime / solved).ToString("0.0") + "s");
            for (int hand = 0; hand < 2 && active; hand++)
            {
                var sample = app.input.Hands[hand];
                var finger = fingers[hand];
                finger.gameObject.SetActive(sample.valid);
                if (!sample.valid) { touching[hand] = null; continue; }
                finger.transform.position = sample.tip;
                HoldTarget hit = null;
                foreach (var target in targets)
                    if (target.Contains(sample, finger)) { hit = target; break; }
                var previous = touching[hand]; touching[hand] = hit;
                if (hit && hit != previous) hit.action?.Invoke(false);
            }
        }
        static string Clock(float seconds) => ((int)seconds / 60).ToString("00") + ":" + ((int)seconds % 60).ToString("00");
        void Deal()
        {
            puzzle.Load(Solver24.Generate(random, app.MaximumNumber));
            puzzleTime = nextDealAt = 0; Refresh();
        }
        void Number(int slot)
        {
            if (puzzle.Solved) return;
            if (!puzzle.SelectNumber(slot, out _)) return;
            if (puzzle.Solved) { solved++; solvedTime += puzzleTime; nextDealAt = sessionTime + .65f; }
            Refresh();
        }
        void Refresh()
        {
            for (int i = 0; i < 4; i++)
            {
                cards[i].gameObject.SetActive(puzzle.Cards[i].HasValue);
                if (puzzle.Cards[i].HasValue) cards[i].SetLabel(puzzle.Cards[i].Value.ToString());
                cards[i].Highlight(puzzle.First == i, app.config.mint);
                operators[i].Highlight(puzzle.Operator == (Operation)i, app.config.mint);
            }
            hint.text = puzzle.Solved ? "24!  Nicely done" : puzzle.First < 0 ? "Touch a number to start" : puzzle.Operator.HasValue ? "Touch the second number" : "Choose an operator";
        }
        TMP_Text Text(string name, string value, float y, float size, TMP_FontAsset font)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)); go.transform.SetParent(canvas, false);
            var text = go.GetComponent<TextMeshProUGUI>(); text.text = value; text.font = font; text.fontSize = size;
            text.alignment = TextAlignmentOptions.Center; text.color = Color.white; text.raycastTarget = false;
            text.rectTransform.sizeDelta = new Vector2(850, 65); text.rectTransform.anchoredPosition = new Vector2(0, y);
            return text;
        }
        HoldTarget Button(HoldTarget template, string label, Vector2 position, Vector2 size, Action action)
        {
            var button = Instantiate(template, canvas); button.name = "Practice " + label;
            button.gameObject.SetActive(true); button.transform.localRotation = Quaternion.identity; button.transform.localScale = Vector3.one;
            button.surface.localPosition = new Vector3(position.x, position.y, 0); button.SetSize(size);
            button.hitVolume.size = new Vector3(size.x, size.y, 16);
            button.activateOnPress = true; button.gameplay = false; button.action = _ => action(); button.SetLabel(label);button.Fill(0);
            button.label.fontSize = 38; button.label.enableAutoSizing = true;button.label.fontSizeMin = 18;button.label.fontSizeMax = 38;
            targets.Add(button); return button;
        }
        void Build()
        {
            panel = new GameObject("24 Practice", typeof(RectTransform), typeof(Canvas));
            canvas = panel.GetComponent<RectTransform>();panel.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            canvas.sizeDelta = new Vector2(940, 780);canvas.localScale = Vector3.one * .0005f;
            var background = new GameObject("Rounded Background", typeof(RectTransform), typeof(UnityEngine.UI.Image));background.transform.SetParent(canvas, false);
            var image = background.GetComponent<UnityEngine.UI.Image>();image.sprite = app.instructionBackground.sprite;image.type = UnityEngine.UI.Image.Type.Sliced;image.pixelsPerUnitMultiplier = 10;image.color = app.instructionBackground.color;image.raycastTarget = false;
            image.rectTransform.anchorMin = Vector2.zero;image.rectTransform.anchorMax = Vector2.one;image.rectTransform.offsetMin = image.rectTransform.offsetMax = Vector2.zero;
            var heading = app.actions[0].label.font;var body = app.actions[0].caption.font;
            Text("Title", "24 PRACTICE", 325, 45, heading);
            stats = Text("Session Stats", "", 260, 25, body);
            hint = Text("Instruction", "", 195, 28, body);
            for (int i = 0; i < 4; i++)
            {
                int index = i;
                cards[i] = Button(app.cards[i], "", new Vector2(i % 2 == 0 ? -80 : 80, i < 2 ? 80 : -80), new Vector2(125, 125), () => Number(index));
                operators[i] = Button(app.operators[i], new[]{"+", "−", "×", "÷"}[i], new Vector2(i < 2 ? -255 : 255, i % 2 == 0 ? 80 : -80), new Vector2(100, 100), () => { if (!puzzle.Solved) { puzzle.SelectOperator((Operation)index); Refresh(); } });
            }
            Button(app.reset, "EXIT", new Vector2(0, -185), new Vector2(200, 70), () => app.ExitPractice());
            Button(app.reset, "RESET", new Vector2(0, -280), new Vector2(200, 70), () => { if (!puzzle.Solved) { puzzle.Reset(); Refresh(); } });
            Button(app.actions[0], "SKIP", new Vector2(255, -280), new Vector2(200, 70), Deal);
            for (int i = 0; i < 2; i++)
            {
                var tip = GameObject.CreatePrimitive(PrimitiveType.Sphere);tip.name = i == 0 ? "Practice Left Index" : "Practice Right Index";
                Destroy(tip.GetComponent<SphereCollider>());tip.transform.localScale = Vector3.one * .012f;
                fingers[i] = tip.AddComponent<CapsuleCollider>();fingers[i].radius = .5f;fingers[i].height = 1;fingers[i].isTrigger = true;
                tip.SetActive(false);
            }
        }
        void OnDestroy()
        {
            End();if (panel) Destroy(panel);
            foreach (var f in fingers) if (f) Destroy(f.gameObject);
        }
    }
}
