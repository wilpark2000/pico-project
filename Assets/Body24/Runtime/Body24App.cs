using System;
using System.Collections;
using TMPro;
using UnityEngine;

namespace Body24
{
    public enum AppState { Calibration, Placement, TutorialOffer, Menu, Settings, Modes, PlankStart, Tutorial1, Tutorial2, Classic, Paused, Results, Practice }
    [DefaultExecutionOrder(50)]
    public class Body24App : MonoBehaviour
    {
        // The floor-frame origin is the playzone's base, below the calibrated player.
        public const float GameplayCardScale = .8f;
        public const float PlayzoneFloorLift = .01f;
        const float OperatorOutwardOffset = .03f;
        public Body24Config config;
        public TrackedHands input;
        public HandContactController contact;
        public Transform frame;
        public PlayzoneView guides;
        public HoldTarget[] cards, operators, actions;
        public HoldTarget reset;
        public TMP_Text title, instruction, timerLabel, scoreLabel, tapLabel, footer, countdown, resultsLabel, reward;
        public UnityEngine.UI.Image instructionBackground;
        public GameObject instructionPanel;
        public TMP_Text floatingInstruction;
        public TMP_Text floatingCalibrationProgress;
        public UnityEngine.UI.Image okProgress;
        public GameObject okPrompt;
        public AppState State { get; private set; }
        public PuzzleModel Puzzle { get; } = new PuzzleModel();
        public int MaximumNumber { get; private set; } = 10;
        public int TotalScore { get; private set; }
        public int SolvedCount { get; private set; }
        public float SessionSeconds { get; private set; }
        public float PuzzleSeconds { get; private set; }
        public float CalibrationConfidence { get; private set; }
        public float StandingHeight { get; private set; } = 1.7f;
        public float ForwardReach { get; private set; } = .55f;
        public float LaneHalfWidth { get; private set; } = .26f;
        public float OperatorReach { get; private set; } = .30f;
        readonly System.Random random = new System.Random();
        readonly AlternatingTaps taps = new AlternatingTaps();
        readonly ContinuousHold okHold = new ContinuousHold();
        readonly Vector3[] cardLocations = new Vector3[4];
        readonly bool[] expectedIntermediate = new bool[4];
        readonly bool[] pausedCards = new bool[4];
        bool pausedReward;
        int displayedScore, pendingPoints;
        AppState resumeState, requestedPlay;
        bool busy, startAnnounced;
        float stateTime, stableSeconds, sampleHeight, sampleReach, sampleWidth, outsideSeconds, pauseLeft, solvedSeconds;
        Vector3 lastLeft, lastRight, plankHead;
        Vector3 gameplayCentre;
        int sampleCount, tutorialStep;
        readonly int[] tutorialActions = { 0, 4, 1, 2, 7, 3, 0, 6, 2 };
        public bool Playing => State == AppState.Classic || State == AppState.Tutorial1 || State == AppState.Tutorial2;
        bool Tutorial => State == AppState.Tutorial1 || State == AppState.Tutorial2;
        void Start()
        {
            MaximumNumber = Mathf.Clamp(PlayerPrefs.GetInt("BODY24.Max", 10), 10, 13);
            float savedReach = PlayerPrefs.GetFloat("BODY24.Reach", config.operatorReach);
            // Older layouts stored a 36–52 cm reach. Start those at the new default.
            OperatorReach = savedReach > .34f ? config.operatorReach : Mathf.Clamp(savedReach, .24f, .34f);
            LayoutGameplay();
            for (int i = 0; i < 4; i++) { int slot = i; cards[i].action = earned => Number(slot, earned); cards[i].gameplay = true; cardLocations[i] = cards[i].transform.localPosition; }
            for (int i = 0; i < 4; i++) { int op = i; operators[i].action = earned => Operator(op, earned); operators[i].gameplay = true; }
            reset.activateOnPress = true;
            reset.action = _ => ResetPuzzle();
            Enter(AppState.Calibration);
        }
        void Update()
        {
            float dt = Time.unscaledDeltaTime; stateTime += dt;
            if (input.ExitPressed)
            {
                StopAllCoroutines(); busy = false;
                if (State == AppState.Classic || (State == AppState.Paused && resumeState == AppState.Classic)) Enter(AppState.Results);
                else Enter(AppState.Menu);
                return;
            }
            if (State == AppState.Calibration) Calibrate(dt);
            else if (State == AppState.Placement)
            {
                UpdatePlacement();
                TickOK(dt, ConfirmPlacement);
            }
            else if (State == AppState.PlankStart) StartPlank(dt);
            else if (Playing)
            {
                bool present = Presence(false);
                outsideSeconds = present ? 0 : outsideSeconds + dt;
                if (outsideSeconds >= config.presenceGrace) { resumeState = State; pauseLeft = Tutorial ? 3 : 15; Enter(AppState.Paused); return; }
                // Grace avoids transient tracking loss, but lost tracking never advances input or timers.
                contact.suspended = busy || !input.HeadValid;
                if (input.HeadValid) { if (!Puzzle.Solved) PuzzleSeconds += dt; if (State == AppState.Classic) SessionSeconds += dt; }
                UpdateHUD();
                if (!busy && !Tutorial && Puzzle.Combinations == 0 && !contact.Holding) UpdateTaps();
            }
            else if (State == AppState.Paused)
            {
                if (Presence(true)) { Resume(); return; }
                pauseLeft -= dt; countdown.text = Mathf.CeilToInt(pauseLeft).ToString();
                TickOK(dt, EndPaused);
                if (pauseLeft <= 0) EndPaused();
            }
        }
        void Enter(AppState state)
        {
            if (State == AppState.Practice && state != AppState.Practice)
                GetComponent<Practice24Mode>()?.End();
            if (state == AppState.Paused)
            { for (int i = 0; i < 4; i++) pausedCards[i] = cards[i].gameObject.activeSelf; pausedReward = reward.gameObject.activeSelf; }
            State = state; stateTime = 0; okHold.Clear(); outsideSeconds = 0;
            contact.Cancel(); contact.suspended = true;
            foreach (var card in cards) card.gameObject.SetActive(false);
            foreach (var op in operators) op.gameObject.SetActive(false);
            foreach (var button in actions) { button.gameObject.SetActive(false); button.action = null; }
            reset.gameObject.SetActive(false); timerLabel.text = scoreLabel.text = tapLabel.text = countdown.text = resultsLabel.text = "";
            reward.gameObject.SetActive(false);
            okPrompt.SetActive(state == AppState.Placement || state == AppState.Paused);
            okProgress.fillAmount = 0;
            instructionPanel.SetActive(state == AppState.Calibration || state == AppState.Placement);
            footer.text = input.Simulation ? "EDITOR  ·  Right-drag: look  /  WASD: move  /  R F: up/down  /  H: reset view\nHold left mouse: touch  /  Space: plank  /  O: OK  /  Q E: taps  /  B: lift  /  Esc: exit" : "Hold to select  ·  Lift bonus optional  ·  Stop whenever you need";
            title.text = "BODY <color=#64FFC0>24</color>";
            switch (state)
            {
                case AppState.Calibration:
                    input.placed = false;
                    CalibrationConfidence = 0;
                    sampleCount = 0; stableSeconds = sampleHeight = sampleReach = sampleWidth = 0;
                    guides.gameObject.SetActive(false);
                    instruction.text = "01  /  FIND YOUR REACH";
                    floatingInstruction.text = "Stand tall\nReach both arms forward\nOpen palms facing up";
                    floatingCalibrationProgress.text = "CALIBRATING  0%\nHEIGHT -- m  ·  REACH -- m";
                    break;
                case AppState.Placement:
                    guides.gameObject.SetActive(true); guides.ShowLanes(false);
                    instruction.text = "02  /  MAKE ROOM";
                    floatingInstruction.text = "Look down for clear floor\nHold an OK sign for " + config.holdSeconds.ToString("0.#") + " seconds";
                    floatingCalibrationProgress.text = "CALIBRATED  100%\nHEIGHT " + StandingHeight.ToString("0.00") + " m  ·  REACH " + ForwardReach.ToString("0.00") + " m";
                    break;
                case AppState.TutorialOffer:
                    instruction.text = "A little maths. A stronger core.\nTry a guided round first?";
                    Button(0, "START TUTORIAL", "Learn the rhythm", () => Begin(AppState.Tutorial1));
                    Button(1, "SKIP", "Straight to the menu", () => { SeenTutorial(); Enter(AppState.Menu); });
                    break;
                case AppState.Menu:
                    instruction.text = "MIND IN FOCUS. BODY IN BALANCE.";
                    Button(0, "PLAY", "Make 24. Hold your plank.", () => Enter(AppState.Modes));
                    Button(1, "TUTORIAL", "Two guided rounds", () => Begin(AppState.Tutorial1));
                    Button(2, "SETTINGS", "Reach · numbers · playzone", () => Enter(AppState.Settings));
                    break;
                case AppState.Modes:
                    instruction.text = "CHOOSE YOUR PACE";
                    Button(0, "CLASSIC", "Endless solvable puzzles", () => Begin(AppState.Classic));
                    Button(1, "24 PRACTICE", "Relaxed fingertip play", StartPractice);
                    Button(2, "BACK", "", () => Enter(AppState.Menu));
                    break;
                case AppState.Practice:
                    contact.suspended = true;
                    guides.gameObject.SetActive(false);
                    break;
                case AppState.Settings:
                    instruction.text = "MAXIMUM NUMBER   /   " + MaximumNumber + "\nOPERATOR REACH   /   " + (OperatorReach + OperatorOutwardOffset).ToString("0.00") + " m";
                    for (int i = 0; i < 4; i++) { int n = i + 10; Button(i, n.ToString(), n == MaximumNumber ? "SELECTED" : "Maximum number", () => { MaximumNumber = n; PlayerPrefs.SetInt("BODY24.Max", n); PlayerPrefs.Save(); Enter(AppState.Settings); }); }
                    Button(4, "REACH −", "Bring operators closer", () => AdjustReach(-.02f));
                    Button(5, "REACH +", "Only within comfortable reach", () => AdjustReach(.02f));
                    Button(6, "RECALIBRATE", "Measure a new playzone", () => Enter(AppState.Calibration));
                    Button(7, "BACK", "Save and return", () => Enter(AppState.Menu));
                    break;
                case AppState.PlankStart:
                    guides.ShowLanes(true); stableSeconds = 0; startAnnounced = false;
                    instruction.text = "Hands in the lanes\nEnter a comfortable high plank";
                    Button(7, "CANCEL", "", () => Enter(AppState.Menu));
                    break;
                case AppState.Tutorial1:
                    tutorialStep = 0; Load(new[] { 3, 5, 6, 2 }); break;
                case AppState.Tutorial2:
                    Load(new[] { 3, 5, 8, 4 }); break;
                case AppState.Classic:
                    displayedScore = TotalScore = SolvedCount = 0; solvedSeconds = SessionSeconds = 0; Load(Solver24.Generate(random, MaximumNumber)); break;
                case AppState.Paused:
                    instruction.text = resumeState == AppState.Classic ? "PAUSED\nReturn to resume · Hold OK to end" : "TUTORIAL PAUSED\nReturn to continue · Hold OK to exit";
                    Button(7, "END SESSION", "Rest whenever you need", EndPaused);
                    break;
                case AppState.Results:
                    instruction.text = "SESSION COMPLETE";
                    resultsLabel.text = "ACTIVE TIME   " + Clock(SessionSeconds) + "\n\nSCORE   " + TotalScore + "\n\nPUZZLES   " + SolvedCount + "\n\nAVERAGE SOLVE   " + (SolvedCount == 0 ? "—" : (solvedSeconds / SolvedCount).ToString("0.0") + " s");
                    Button(7, "BACK TO MENU", "Take a breath", () => Enter(AppState.Menu));
                    break;
            }
            LayoutPresentation();
            contact.suspended = state == AppState.Calibration || state == AppState.Placement || state == AppState.Practice;
        }
        public void StartPractice()
        {
            Enter(AppState.Practice);
            var mode = GetComponent<Practice24Mode>();
            if (!mode) mode = gameObject.AddComponent<Practice24Mode>();
            mode.Begin(this);
        }
        public void ExitPractice() { Enter(AppState.Menu); guides.gameObject.SetActive(true); }
        void AnchorUnderHead()
        {
            Vector3 forward = Vector3.ProjectOnPlane(input.head.transform.forward, Vector3.up).normalized;
            if (forward.sqrMagnitude < .1f) forward = Vector3.ProjectOnPlane(input.head.transform.up, Vector3.up).normalized;
            if (forward.sqrMagnitude < .1f) forward = frame.forward;
            frame.SetPositionAndRotation(new Vector3(input.head.transform.position.x, config.floorHeight + PlayzoneFloorLift, input.head.transform.position.z), Quaternion.LookRotation(forward));
        }
        void ConfirmPlacement()
        {
            AnchorUnderHead();
            input.LockPlayFrame();
            guides.ShowLanes(true);
            PlayerPrefs.Save();
            Enter(PlayerPrefs.GetInt("BODY24.TutorialSeen", 0) == 0 ? AppState.TutorialOffer : AppState.Menu);
        }
        void Button(int index, string text, string sub, Action action)
        {
            var button = actions[index]; button.gameObject.SetActive(true); button.SetLabel(text, sub); button.action = _ => action();
            // Menus use the playzone centre, independently of the in-plank game position.
            Vector3 p;
            if (State == AppState.Settings)
                p = index < 4 ? new Vector3((index - 1.5f) * 205, 150, 0) : new Vector3(index % 2 == 0 ? -210 : 210, -((index - 4) / 2) * 150, 0);
            else if (State == AppState.Menu) p = new Vector3(0, 170 - index * 170, 0);
            else if (State == AppState.TutorialOffer || State == AppState.Modes) p = new Vector3(0, 85 - index * 170, 0);
            else p = Vector3.zero;
            button.transform.localPosition = p;
            button.SetSize(State == AppState.Settings ? new Vector2(index < 4 ? 185 : 390, 115) : new Vector2(570, 120));
        }
        void AdjustReach(float delta)
        {
            OperatorReach = Mathf.Clamp(OperatorReach + delta, .24f, .34f);
            PlayerPrefs.SetFloat("BODY24.Reach", OperatorReach); PlayerPrefs.Save(); LayoutOperators(); Enter(AppState.Settings);
        }
        void Calibrate(float dt)
        {
            // Setup is a live preview until the player confirms its pose with OK.
            if (!input.placed && input.HeadValid) AnchorUnderHead();
            var l = input.Hands[0]; var r = input.Hands[1];
            bool stable = input.HeadValid && l.valid && r.valid && l.openPalmUp && r.openPalmUp &&
                (sampleCount == 0 || (Vector3.Distance(l.palm, lastLeft) < config.maximumSampleMotion && Vector3.Distance(r.palm, lastRight) < config.maximumSampleMotion));
            lastLeft = l.palm; lastRight = r.palm;
            if (!stable)
            {
                stableSeconds = sampleHeight = sampleReach = sampleWidth = 0; sampleCount = 0;
                CalibrationConfidence = 0;
                floatingCalibrationProgress.text = input.HeadValid ? "CALIBRATING  0%\nKeep both palms up and steady" : "WAITING FOR HEAD TRACKING";
                return;
            }
            float h = input.head.transform.position.y - config.floorHeight;
            if (h < config.minimumHeight || h > config.maximumHeight) { floatingCalibrationProgress.text = "Stand upright above your calibrated floor"; return; }
            stableSeconds += Mathf.Min(dt, .1f); sampleCount++;
            sampleHeight += h; sampleWidth += Vector3.Distance(l.palm, r.palm);
            var forward = Vector3.ProjectOnPlane(input.head.transform.forward, Vector3.up).normalized;
            sampleReach += Vector3.Dot((l.palm + r.palm) * .5f - input.head.transform.position, forward);
            CalibrationConfidence = Mathf.Clamp01(stableSeconds / config.sampleSeconds);
            floatingCalibrationProgress.text = "CALIBRATING  " + Mathf.RoundToInt(CalibrationConfidence * 100) + "%\nHEIGHT " + (sampleHeight / sampleCount).ToString("0.00") + " m  ·  REACH " + (sampleReach / sampleCount).ToString("0.00") + " m";
            if (stableSeconds < config.sampleSeconds || sampleCount < config.minimumSampleCount) return;
            StandingHeight = sampleHeight / sampleCount; ForwardReach = Mathf.Clamp(sampleReach / sampleCount, .3f, .85f);
            LaneHalfWidth = Mathf.Clamp(sampleWidth / sampleCount * .5f, config.minimumLaneHalfWidth, config.maximumLaneHalfWidth);
            OperatorReach = Mathf.Clamp(OperatorReach, .24f, Mathf.Min(.34f, ForwardReach));
            input.laneWidth = LaneHalfWidth;
            CentreMenuCanvas();
            LayoutOperators(); Enter(AppState.Placement);
        }
        void UpdatePlacement()
        {
            if (!input.placed && input.HeadValid) AnchorUnderHead();
            guides.transform.localPosition = Vector3.zero;
            guides.Layout(StandingHeight, Mathf.Max(OperatorReach + .12f, .57f), LaneHalfWidth);
        }
        public void LayoutGameplay()
        {
            var canvas = (RectTransform)cards[0].transform.parent;
            canvas.pivot = new Vector2(.5f, .5f);
            var framePosition = frame.position; framePosition.y = config.floorHeight + PlayzoneFloorLift; frame.position = framePosition;
            CentreMenuCanvas();
            gameplayCentre = Vector3.zero;
            // Use a true central canvas origin; eliminate the old 650-pixel offset.
            for (int i = 0; i < canvas.childCount; i++)
                if (canvas.GetChild(i) is RectTransform rect)
                    rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
            guides.transform.localPosition = Vector3.zero;
            guides.Layout(StandingHeight, Mathf.Max(OperatorReach + .12f, .57f), LaneHalfWidth);
            for (int i = 0; i < cards.Length; i++)
            {
                cardLocations[i] = new Vector3(i % 2 == 0 ? -82 : 82, i < 2 ? 85 : -85, 0);
                cards[i].transform.localPosition = cardLocations[i];
                cards[i].transform.localScale = Vector3.one * GameplayCardScale;
                cards[i].SetSize(cards[i].surface.sizeDelta);
            }
            foreach (var op in operators) { op.transform.localScale = Vector3.one * GameplayCardScale; op.SetSize(op.surface.sizeDelta); }
            foreach (var probe in input.probeVisuals) if (probe) probe.localScale = Vector3.one * (config.probeRadius * 2);
            input.ConfigureProbeColliders();
            reset.transform.localPosition = new Vector3(0, 290, 0);
            okPrompt.transform.localPosition = Vector3.zero;
            countdown.rectTransform.localPosition = new Vector3(0, -StandingHeight * .5f * 1000, 0);
            LayoutOperators();
            StyleInstructions();
            LayoutPresentation();
        }
        void LayoutOperators()
        {
            for (int i = 0; i < operators.Length; i++) operators[i].transform.localPosition = gameplayCentre + new Vector3((i < 2 ? -1 : 1) * (OperatorReach + OperatorOutwardOffset) * 1000, i % 2 == 0 ? 85 : -85, 0);
        }
        void CentreMenuCanvas()
        {
            cards[0].transform.parent.localPosition = new Vector3(0, 0, StandingHeight * .5f);
        }
        void PlaceGameplayUnderHead()
        {
            // This pose comes only from the completed hands-on-lines countdown.
            // The initial standing calibration and OK sign do not position the game.
            var canvas = cards[0].transform.parent;
            var position = canvas.InverseTransformPoint(new Vector3(input.head.transform.position.x,
                config.floorHeight + PlayzoneFloorLift, input.head.transform.position.z));
            var centre = new Vector3(position.x, position.y, 0);
            var delta = centre - gameplayCentre;
            gameplayCentre = centre;
            for (int i = 0; i < cards.Length; i++)
            {
                cardLocations[i] += delta;
                cards[i].transform.localPosition = cardLocations[i];
            }
            LayoutOperators();
            reset.transform.localPosition = gameplayCentre + new Vector3(0, 290, 0);
        }
        void StyleInstructions()
        {
            instruction.fontSize = 54;
            instruction.fontStyle = FontStyles.Bold;
            instruction.color = new Color(.88f, 1, .96f);
            instruction.rectTransform.sizeDelta = new Vector2(1050, 220);
            instruction.margin = new Vector4(16, 12, 16, 12);
            if (!instructionBackground)
            {
                var existing = instruction.transform.parent.Find("InstructionHighlight");
                if (existing) instructionBackground = existing.GetComponent<UnityEngine.UI.Image>();
                else
                {
                    var panel = new GameObject("InstructionHighlight", typeof(RectTransform), typeof(UnityEngine.UI.Image));
                    panel.transform.SetParent(instruction.transform.parent, false);
                    instructionBackground = panel.GetComponent<UnityEngine.UI.Image>();
                }
            }
            instructionBackground.raycastTarget = false;
            instructionBackground.color = new Color(.035f, .22f, .18f, .96f);
            var backgroundRect = instructionBackground.rectTransform;
            backgroundRect.anchorMin = backgroundRect.anchorMax = new Vector2(.5f, .5f);
            backgroundRect.sizeDelta = new Vector2(1086, 248);
            instructionBackground.transform.SetSiblingIndex(instruction.transform.GetSiblingIndex());
            instruction.transform.SetSiblingIndex(instructionBackground.transform.GetSiblingIndex() + 1);
            floatingInstruction.fontSize = 42;
            var panelRect = (RectTransform)instructionPanel.transform;
            panelRect.sizeDelta = new Vector2(680, 430);
            floatingInstruction.rectTransform.anchorMin = floatingInstruction.rectTransform.anchorMax = new Vector2(.5f, .5f);
            floatingInstruction.rectTransform.anchoredPosition = new Vector2(0, 85);
            floatingInstruction.rectTransform.sizeDelta = new Vector2(620, 200);
            if (!floatingCalibrationProgress)
            {
                var existing = instructionPanel.transform.Find("CalibrationProgress");
                if (existing) floatingCalibrationProgress = existing.GetComponent<TMP_Text>();
                else
                {
                    var progressObject = new GameObject("CalibrationProgress", typeof(RectTransform), typeof(TextMeshProUGUI));
                    progressObject.transform.SetParent(instructionPanel.transform, false);
                    floatingCalibrationProgress = progressObject.GetComponent<TextMeshProUGUI>();
                }
            }
            floatingCalibrationProgress.font = floatingInstruction.font;
            floatingCalibrationProgress.fontSize = 30;
            floatingCalibrationProgress.fontStyle = FontStyles.Bold;
            floatingCalibrationProgress.alignment = TextAlignmentOptions.Center;
            floatingCalibrationProgress.color = config.mint;
            floatingCalibrationProgress.raycastTarget = false;
            floatingCalibrationProgress.rectTransform.anchorMin = floatingCalibrationProgress.rectTransform.anchorMax = new Vector2(.5f, .5f);
            floatingCalibrationProgress.rectTransform.anchoredPosition = new Vector2(0, -65);
            floatingCalibrationProgress.rectTransform.sizeDelta = new Vector2(620, 90);
            if (string.IsNullOrEmpty(floatingCalibrationProgress.text)) floatingCalibrationProgress.text = "CALIBRATING  0%\nHEIGHT -- m  ·  REACH -- m";
            var floatingBackground = instructionPanel.transform.Find("Panel");
            if (floatingBackground)
            {
                floatingBackground.GetComponent<UnityEngine.UI.Image>().color = instructionBackground.color;
                var rect = (RectTransform)floatingBackground;
                rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
                rect.anchoredPosition = Vector2.zero; rect.sizeDelta = panelRect.sizeDelta;
            }
            foreach (var name in new[] { "LeftHandOutline", "RightHandOutline" })
            {
                var icon = instructionPanel.transform.Find(name) as RectTransform;
                if (!icon) continue;
                icon.anchorMin = icon.anchorMax = new Vector2(.5f, .5f);
                icon.anchoredPosition = new Vector2(name == "LeftHandOutline" ? -70 : 70, -160);
                icon.sizeDelta = new Vector2(75, 75);
            }
            scoreLabel.rectTransform.sizeDelta = timerLabel.rectTransform.sizeDelta = new Vector2(500, 65);
        }
        void LateUpdate() { LayoutPresentation(); }
        void LayoutPresentation()
        {
            // Anchor the tilted stats group beyond the calibrated playzone's far edge.
            var statsUI = title.transform.parent;
            if (statsUI && statsUI.name == "Stats UI")
                statsUI.position = frame.TransformPoint(new Vector3(0, 0, StandingHeight + .05f));
            float cursor = (StandingHeight - cards[0].transform.parent.localPosition.z) * 1000 + 50;
            const float gap = 28;
            void Pack(TMP_Text text, float padding = 0)
            {
                if (!text.gameObject.activeInHierarchy || string.IsNullOrEmpty(text.text)) return;
                float height = text.rectTransform.sizeDelta.y + padding;
                text.rectTransform.localPosition = new Vector3(0, cursor + height * .5f, 0);
                cursor += height + gap;
            }
            Pack(instruction, 28);
            if (instructionBackground)
            {
                instructionBackground.enabled = instruction.gameObject.activeInHierarchy && !string.IsNullOrEmpty(instruction.text);
                instructionBackground.rectTransform.localPosition = instruction.rectTransform.localPosition;
            }
            Pack(tapLabel);
            if (State == AppState.PlankStart)
                countdown.rectTransform.position = new Vector3(input.head.transform.position.x, config.floorHeight + PlayzoneFloorLift, input.head.transform.position.z);
            else Pack(countdown);
            Pack(resultsLabel);
            bool scoreVisible = !string.IsNullOrEmpty(scoreLabel.text), timerVisible = !string.IsNullOrEmpty(timerLabel.text);
            if (scoreVisible || timerVisible)
            {
                scoreLabel.rectTransform.localPosition = new Vector3(timerVisible ? -260 : 0, cursor + 32.5f, 0);
                timerLabel.rectTransform.localPosition = new Vector3(scoreVisible ? 260 : 0, cursor + 32.5f, 0);
                cursor += 65 + gap;
            }
            Pack(title);
            var eyebrow = title.transform.parent.Find("Eyebrow") as RectTransform;
            if (eyebrow) Pack(eyebrow.GetComponent<TMP_Text>());
            Pack(footer);
        }
        void TickOK(float dt, Action action)
        {
            bool valid = input.HeadValid && ((input.Hands[0].valid && input.Hands[0].ok) || (input.Hands[1].valid && input.Hands[1].ok));
            bool done = okHold.Tick(valid, dt, config.holdSeconds); okProgress.fillAmount = okHold.Elapsed / config.holdSeconds;
            if (done) action();
        }
        void Begin(AppState mode) { requestedPlay = mode; if (mode == AppState.Tutorial1) SeenTutorial(); Enter(AppState.PlankStart); }
        void StartPlank(float dt)
        {
            bool valid = input.HeadValid;
            for (int i = 0; i < 2; i++)
            {
                var h = input.Hands[i]; var local = frame.InverseTransformPoint(h.palm);
                float laneDepth = local.z;
                valid &= h.valid && Mathf.Abs(local.x - (i == 0 ? -LaneHalfWidth : LaneHalfWidth)) < .11f && local.y < config.supportHeight && local.y > -.06f && laneDepth > .12f && laneDepth < StandingHeight - .1f;
            }
            if (!valid) { stableSeconds = 0; startAnnounced = false; countdown.text = "PLACE HANDS"; return; }
            if (stableSeconds == 0) plankHead = frame.InverseTransformPoint(input.head.transform.position);
            stableSeconds += dt;
            if (stableSeconds < 3) countdown.text = Mathf.CeilToInt(3 - stableSeconds).ToString();
            else { countdown.text = "START"; startAnnounced = true; }
            if (startAnnounced && stableSeconds >= 4)
            {
                PlaceGameplayUnderHead();
                Enter(requestedPlay);
                plankHead = frame.InverseTransformPoint(input.head.transform.position);
            }
        }
        bool Presence(bool returning)
        {
            if (!input.HeadValid) return false;
            Vector3 p = frame.InverseTransformPoint(input.head.transform.position) - plankHead;
            float margin = returning ? -config.presenceHysteresis : 0;
            return p.y < config.headRiseLimit + margin && p.y > -config.headDropLimit - margin && p.z > -config.backwardLimit - margin && Mathf.Abs(p.x) < config.lateralLimit + margin && p.z < .65f + margin;
        }
        void Resume()
        {
            // Restore presentation without re-entering the state, which would reset the puzzle.
            foreach (var button in actions) { button.gameObject.SetActive(false); button.action = null; }
            State = resumeState; countdown.text = ""; okPrompt.SetActive(false); okHold.Clear();
            outsideSeconds = 0; contact.Cancel();
            if (busy)
            {
                for (int i = 0; i < 4; i++) { cards[i].gameObject.SetActive(pausedCards[i]); operators[i].gameObject.SetActive(true); }
                reward.gameObject.SetActive(pausedReward); reset.gameObject.SetActive(true); contact.suspended = true; UpdateHints();
            }
            else ShowPuzzle();
        }
        void EndPaused() { StopAllCoroutines(); busy = false; Enter(resumeState == AppState.Classic ? AppState.Results : AppState.Menu); }
        void SeenTutorial() { PlayerPrefs.SetInt("BODY24.TutorialSeen", 1); PlayerPrefs.Save(); }
        void Load(int[] values) { Puzzle.Load(values); PuzzleSeconds = 0; tutorialStep = 0; taps.Reset(); busy = false; ShowPuzzle(); }
        void ShowPuzzle()
        {
            reset.gameObject.SetActive(true); reset.SetLabel("RESET", "Restore these four numbers");
            RefreshCards(); UpdateHints(); contact.suspended = busy;
        }
        void RefreshCards()
        {
            for (int i = 0; i < 4; i++)
            {
                cards[i].transform.localPosition = cardLocations[i]; cards[i].transform.localScale = Vector3.one * GameplayCardScale;
                cards[i].gameObject.SetActive(Puzzle.Cards[i].HasValue);
                if (!Puzzle.Cards[i].HasValue) continue;
                cards[i].SetLabel(Puzzle.Cards[i].Value.ToString(), Puzzle.Intermediate[i] ? "COMBINED" : "NUMBER");
                cards[i].background.color = Puzzle.Solved ? config.gold : Puzzle.Intermediate[i] ? new Color(.07f, .28f, .25f) : config.ink;
                cards[i].label.color = Puzzle.Solved ? config.ink : Color.white;
                cards[i].Highlight(Puzzle.First == i, config.mint);
                cards[i].Fill(0);
            }
            for (int i = 0; i < 4; i++) { operators[i].gameObject.SetActive(true); operators[i].Highlight(Puzzle.Operator == (Operation)i, config.mint); operators[i].Fill(0); }
        }
        bool Allowed(int code) => State != AppState.Tutorial1 || (tutorialStep < tutorialActions.Length && tutorialActions[tutorialStep] == code);
        void Number(int slot, bool earned)
        {
            if (!Playing || busy || !Allowed(slot)) return;
            string movingText = cards[slot].label.text;
            if (!Puzzle.SelectNumber(slot, out int destination)) { instruction.text = "Choose a different card · division by zero is unavailable"; return; }
            if (earned) Puzzle.Bonuses++;
            if (Puzzle.Solved && State == AppState.Classic)
            { pendingPoints = Solver24.Score(Puzzle.Bonuses, PuzzleSeconds); TotalScore += pendingPoints; SolvedCount++; solvedSeconds += PuzzleSeconds; }
            if (State == AppState.Tutorial1) tutorialStep++;
            if (destination >= 0) StartCoroutine(Merge(slot, destination, movingText));
            else { RefreshCards(); UpdateHints(); }
        }
        void Operator(int index, bool earned)
        {
            if (!Playing || busy || !Allowed(index + 4)) return;
            if (!Puzzle.SelectOperator((Operation)index)) { instruction.text = "Choose a number first"; return; }
            if (earned) Puzzle.Bonuses++;
            if (State == AppState.Tutorial1) tutorialStep++;
            RefreshCards(); UpdateHints();
        }
        IEnumerator Merge(int source, int destination, string text)
        {
            busy = true; contact.suspended = true; contact.Cancel();
            cards[source].gameObject.SetActive(true); cards[source].SetLabel(text);
            Vector3 start = cardLocations[source], end = cardLocations[destination];
            float time = 0;
            while (time < config.mergeSeconds) { if (State == AppState.Paused) { yield return null; continue; } time += Time.unscaledDeltaTime; float t = Mathf.SmoothStep(0, 1, time / config.mergeSeconds); cards[source].transform.localPosition = Vector3.Lerp(start, end, t); cards[source].transform.localScale = Vector3.one * GameplayCardScale * Mathf.Lerp(1, .7f, t); yield return null; }
            RefreshCards();
            if (Puzzle.Solved) { yield return CompletePuzzle(); }
            busy = false;
            // Completion can enter the menu, which already configured its input.
            // Do not overwrite that state's interaction settings from this old animation.
            if (!Playing) yield break;
            contact.Cancel(); contact.suspended = false; UpdateHints();
        }
        IEnumerator CompletePuzzle()
        {
            yield return ActiveWait(.5f);
            if (State == AppState.Tutorial1) { Enter(AppState.Tutorial2); yield break; }
            if (State == AppState.Tutorial2) { SeenTutorial(); Enter(AppState.Menu); yield break; }
            int points = pendingPoints; int oldScore = TotalScore - points;
            Vector3 destination = scoreLabel.transform.position;
            foreach (var card in cards) if (card.gameObject.activeSelf) reward.transform.position = card.transform.position;
            Vector3 start = reward.transform.position; reward.gameObject.SetActive(true); reward.text = "+" + points;
            float time = 0;
            while (time < .65f) { if (State == AppState.Paused) { yield return null; continue; } time += Time.unscaledDeltaTime; reward.transform.position = Vector3.Lerp(start, destination, Mathf.SmoothStep(0, 1, time / .65f)); yield return null; }
            reward.gameObject.SetActive(false); time = 0;
            while (time < 1) { if (State == AppState.Paused) { yield return null; continue; } time += Time.unscaledDeltaTime; displayedScore = oldScore + Mathf.RoundToInt(points * Mathf.Clamp01(time)); UpdateHUD(); yield return null; }
            displayedScore = TotalScore; Load(Solver24.Generate(random, MaximumNumber));
        }
        IEnumerator ActiveWait(float duration) { float t = 0; while (t < duration) { if (State != AppState.Paused) t += Time.unscaledDeltaTime; yield return null; } }
        void ResetPuzzle()
        {
            if (!Playing || busy) return;
            Puzzle.Reset(); tutorialStep = 0; taps.Reset(); contact.Cancel(); RefreshCards(); UpdateHints();
        }
        void UpdateTaps()
        {
            for (int i = 0; i < 2; i++)
            {
                var h = input.Hands[i];
                if (taps.Tick(i, h.valid && input.HeadValid, Vector3.Distance(h.palm, input.head.transform.position) < config.shoulderDistance, Time.unscaledTime, config.shoulderCooldown))
                { StartCoroutine(Reroll()); break; }
            }
            tapLabel.text = "SHOULDER TAPS  " + taps.Count + " / 4";
        }
        IEnumerator Reroll()
        {
            int slot, replacement;
            if (!Solver24.Reroll(Puzzle.Original, MaximumNumber, random, out slot, out replacement))
            { instruction.text = "No solvable replacement · RESET and try again"; Debug.LogWarning("BODY24: no valid reroll candidate."); taps.Reset(); yield break; }
            busy = true; contact.suspended = true; contact.Cancel();
            for (float t = 0; t < .22f; t += Time.unscaledDeltaTime) { if (State == AppState.Paused) { yield return null; continue; } cards[slot].label.alpha = 1 - t / .22f; yield return null; }
            Puzzle.Replace(slot, replacement); RefreshCards();
            for (float t = 0; t < .22f; t += Time.unscaledDeltaTime) { if (State == AppState.Paused) { yield return null; continue; } cards[slot].label.alpha = t / .22f; yield return null; }
            cards[slot].label.alpha = 1; taps.Reset(); busy = false; contact.Cancel(); contact.suspended = !Playing; UpdateHints();
        }
        void UpdateHints()
        {
            if (!Playing) return;
            if (State == AppState.Tutorial1)
            {
                string[] hints = { "Place hand on 3", "Choose +", "Choose 5 · watch it move into 3", "Choose 6 · optional: lift your OTHER hand within 0.5 s and hold it up", "Choose ÷", "Choose 2", "Choose the combined 8", "Choose ×", "Choose the combined 3" };
                instruction.text = tutorialStep < hints.Length ? hints[tutorialStep] : "24 · beautifully done";
                if (tutorialStep < tutorialActions.Length) { int code = tutorialActions[tutorialStep]; if (code < 4) cards[code].Highlight(true, config.gold); else operators[code - 4].Highlight(true, config.gold); }
            }
            else if (State == AppState.Tutorial2) instruction.text = "Now make 24 · combine pairs in any order";
            else instruction.text = Puzzle.First < 0 ? "NUMBER  →  OPERATOR  →  NUMBER" : !Puzzle.Operator.HasValue ? "CHOOSE AN OPERATOR" : "CHOOSE THE SECOND NUMBER";
            if (Tutorial) tapLabel.text = "";
            else tapLabel.text = Puzzle.Combinations == 0 ? "SHOULDER TAPS  " + taps.Count + " / 4" : "RESET TO REROLL";
        }
        void UpdateHUD()
        {
            timerLabel.text = "ACTIVE  " + Clock(State == AppState.Classic ? SessionSeconds : PuzzleSeconds);
            scoreLabel.text = "SCORE  " + displayedScore + "    /    BONUS ×" + Math.Pow(1.2, Puzzle.Bonuses).ToString("0.00");
        }
        static string Clock(float seconds) => ((int)seconds / 60).ToString("00") + ":" + ((int)seconds % 60).ToString("00");
    }
}
