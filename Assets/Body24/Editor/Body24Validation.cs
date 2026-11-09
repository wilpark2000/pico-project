using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Body24.Editor
{
    public static class Body24Validation
    {
        static readonly List<string> checks = new List<string>();
        sealed class RepeatingRandom : System.Random
        {
            public int Calls;
            public override int Next(int maxValue) { Calls++; return 0; }
            public override int Next(int minValue, int maxValue) { Calls++; return minValue; }
        }
        static void Check(bool condition, string message)
        { if (!condition) throw new Exception("FAIL: " + message); checks.Add("PASS: " + message); }
        [MenuItem("BODY 24/2. Validate rules")]
        public static void RunRules()
        {
            checks.Clear();
            try
            {
                Check(Solver24.Solvable(new[] { 3, 5, 6, 2 }), "Tutorial 1 is solvable");
                Check(!Solver24.Solvable(new[] { 3, 5, 8, 10 }), "Tutorial 2 original is unsolvable");
                Check(Solver24.Solvable(new[] { 3, 5, 8, 4 }), "Tutorial 2 replacement is solvable");
                Check(Rational.Apply(new Rational(3), Operation.Subtract, new Rational(5)).Equals(new Rational(-2)), "Subtraction preserves operand order and negatives");
                Check(Rational.Apply(new Rational(3), Operation.Divide, new Rational(5)).Equals(new Rational(3, 5)), "Division preserves operand order and fractions");
                Check(Rational.Apply(new Rational(1, 3), Operation.Add, new Rational(2, 3)).Equals(new Rational(1)), "Exact rational normalization");
                bool zeroRejected = false; try { Rational.Apply(new Rational(3), Operation.Divide, new Rational(0)); } catch (DivideByZeroException) { zeroRejected = true; }
                Check(zeroRejected, "Division by zero is rejected");
                var puzzle = new PuzzleModel(); puzzle.Load(new[] { 3, 5, 6, 2 });
                Check(!puzzle.SelectOperator(Operation.Add), "Operator before number is prohibited");
                puzzle.SelectNumber(0, out _); puzzle.SelectNumber(1, out int replacementDestination);
                Check(puzzle.First == 1 && replacementDestination == -1 && puzzle.Combinations == 0, "Second number before operator replaces first selection");
                puzzle.Reset(); puzzle.SelectNumber(0, out _); puzzle.SelectOperator(Operation.Add);
                Check(!puzzle.SelectNumber(0, out _), "Same card cannot supply both operands");
                puzzle.SelectNumber(1, out int destination);
                Check(destination == 0 && !puzzle.Cards[1].HasValue && puzzle.Cards[0].Value.Equals(new Rational(8)), "Second card merges into first slot");
                Check(puzzle.First == -1 && !puzzle.Operator.HasValue && puzzle.Intermediate[0], "Merged result is intermediate and unselected");
                puzzle.SelectNumber(2, out _); puzzle.SelectOperator(Operation.Divide); puzzle.SelectNumber(3, out _);
                puzzle.SelectNumber(0, out _); puzzle.SelectOperator(Operation.Multiply); puzzle.SelectNumber(2, out _);
                Check(puzzle.Solved, "Full guided sequence makes exactly 24");
                puzzle.Bonuses = 5; puzzle.Reset();
                Check(puzzle.Bonuses == 0 && puzzle.Combinations == 0 && puzzle.Cards.All(v => v.HasValue), "Reset restores original cards, selection and bonus");
                var random = new System.Random(2401);
                var unsolvable = new[] { 3, 5, 8, 10 };
                var repaired = Solver24.EnsureSolvable(unsolvable, random, 10);
                Check(Solver24.Solvable(repaired) && repaired.All(n => n >= 1 && n <= 10), "Unsolvable deal is repaired within range");
                Check(unsolvable.SequenceEqual(new[] { 3, 5, 8, 10 }), "Repair preserves its input array");
                var solvedDeal = new[] { 3, 5, 6, 2 };
                Check(Solver24.EnsureSolvable(solvedDeal, random, 10).SequenceEqual(solvedDeal), "Solvable deal needs no replacement");
                var repeating = new RepeatingRandom();
                var fallback = Solver24.EnsureSolvable(new[] { 1, 1, 1, 1 }, repeating, 3);
                Check(repeating.Calls == 128 && Solver24.Solvable(fallback) && fallback.All(n => n >= 1 && n <= 3), "Repeated failed replacements stop after 64 attempts with a solvable fallback");
                Check(Solver24.Solvable(new[] { 3, 3, 8, 8 }), "Solver accepts a solution requiring fractional intermediates");
                foreach (int max in new[] { 10, 11, 12, 13 })
                    for (int sample = 0; sample < 12; sample++)
                    {
                        var numbers = Solver24.Generate(random, max);
                        Check(numbers.All(n => n >= 1 && n <= max) && Solver24.Solvable(numbers), "Generated solvable puzzle, range " + max + ", sample " + sample);
                        Check(Solver24.Reroll(numbers, max, random, out int slot, out int value), "Reroll candidate exists");
                        int previous = numbers[slot]; numbers[slot] = value;
                        Check(previous != value && Solver24.Solvable(numbers), "Reroll changes value and preserves solvability");
                    }
                Check(Solver24.ReplacementIndex(new[] { 3, 9, 3, 12 }) == 0, "Duplicate-first stable replacement");
                Check(Solver24.ReplacementIndex(new[] { 3, 5, 8, 10 }) == 3, "Otherwise replace highest");
                var hold = new ContinuousHold();
                Check(!hold.Tick(true, .49f, .5f) && hold.Tick(true, .01f, .5f) && !hold.Tick(true, 2, .5f), "Hold confirms exactly once at half a second");
                hold.Tick(false, .1f, .5f);
                Check(!hold.Tick(true, .3f, .5f) && !hold.Tick(false, .1f, .5f) && !hold.Tick(true, .3f, .5f), "Early release resets progress");
                Check(hold.Tick(true, .2f, .5f), "Exit rearms hold");
                var bonus = new LiftBonus(); bonus.Begin(true, true); bonus.Tick(.5f, true, false, .5f); bonus.Tick(1, true, false, .5f);
                Check(bonus.Qualified, "Opposite hand lift at 0.5 seconds qualifies through confirmation");
                bonus.Begin(true, true); bonus.Tick(.501f, true, false, .5f); Check(!bonus.Qualified, "Late lift fails");
                bonus.Begin(true, false); bonus.Tick(.2f, true, false, .5f); Check(!bonus.Qualified, "Hand already raised fails");
                bonus.Begin(true, true); bonus.Tick(.2f, true, false, .5f); bonus.Tick(.9f, true, true, .5f); Check(!bonus.Qualified, "Returning support cancels bonus");
                bonus.Begin(true, true); bonus.Tick(.2f, true, false, .5f); bonus.Tick(.9f, false, false, .5f); Check(!bonus.Qualified, "Tracking loss cancels bonus");
                Check(Solver24.Score(4, 16) == 207 && Solver24.Score(4, 15) == 311, "Multiplicative score and inclusive speed threshold");
                Check(Solver24.Score(0, 15.001f) == 100 && Solver24.Score(0, 15) == 150, "Speed factor expires after 15 seconds");
                var taps = new AlternatingTaps(); taps.Reset();
                taps.Tick(0, true, false, 0, .35f); taps.Tick(1, true, false, 0, .35f);
                taps.Tick(1, true, true, 1, .35f); taps.Tick(1, true, false, 1.1f, .35f); taps.Tick(1, true, true, 2, .35f);
                Check(taps.Count == 1, "Either hand starts; same hand cannot count consecutively");
                taps.Tick(0, true, true, 3, .35f); taps.Tick(0, true, true, 4, .35f);
                Check(taps.Count == 2, "Tap must leave before rearming");
                taps.Tick(1, false, true, 4, .35f); Check(taps.Count == 2, "Missing tracking never counts taps");
                taps.Tick(1, true, false, 4.1f, .35f); taps.Tick(1, true, true, 5, .35f); taps.Tick(0, true, false, 5.1f, .35f);
                Check(taps.Tick(0, true, true, 6, .35f) && taps.Count == 4, "Exactly four alternating taps complete reroll");
                Write("Rules", "PASS — " + checks.Count + " assertions");
            }
            catch (Exception e) { checks.Add(e.ToString()); Write("Rules", "FAILED"); throw; }
        }
        static void Write(string name, string status)
        {
            Directory.CreateDirectory("Assets/Body24/Reports");
            File.WriteAllText("Assets/Body24/Reports/" + name + ".txt", status + "\n" + DateTime.UtcNow.ToString("O") + "\n" + string.Join("\n", checks));
            Debug.Log("BODY24 " + name + ": " + status);
        }
        static IEnumerator live;
        static readonly Stack<IEnumerator> routines = new Stack<IEnumerator>();
        static Body24App app;
        static int savedSeen;
        static int savedMaximum;
        static bool hadSeen;
        [MenuItem("BODY 24/3. Validate live Play Mode")]
        public static void StartLive()
        {
            if (!EditorApplication.isPlaying) throw new InvalidOperationException("Enter Play Mode first.");
            app = UnityEngine.Object.FindAnyObjectByType<Body24App>();
            if (!app) throw new InvalidOperationException("Open the BODY24 scene.");
            if (live != null) throw new InvalidOperationException("Live validation already running.");
            checks.Clear(); hadSeen = PlayerPrefs.HasKey("BODY24.TutorialSeen"); savedSeen = PlayerPrefs.GetInt("BODY24.TutorialSeen");
            savedMaximum = PlayerPrefs.GetInt("BODY24.Max", 10);
            if (File.Exists("Assets/Body24/Reports/LivePlayMode.txt")) File.Delete("Assets/Body24/Reports/LivePlayMode.txt");
            PlayerPrefs.SetInt("BODY24.TutorialSeen", 0);
            live = Live(); routines.Clear(); routines.Push(live); EditorApplication.update += StepLive;
        }
        static void StepLive()
        {
            try
            {
                if (routines.Count > 0)
                {
                    var current = routines.Peek();
                    if (!current.MoveNext()) routines.Pop();
                    else if (current.Current is IEnumerator nested) routines.Push(nested);
                    EditorApplication.QueuePlayerLoopUpdate(); return;
                }
                FinishLive(true);
            }
            catch (Exception e) { checks.Add(e.ToString()); FinishLive(false); }
        }
        static void FinishLive(bool success)
        {
            EditorApplication.update -= StepLive; live = null;
            if (hadSeen) PlayerPrefs.SetInt("BODY24.TutorialSeen", savedSeen); else PlayerPrefs.DeleteKey("BODY24.TutorialSeen");
            PlayerPrefs.SetInt("BODY24.Max", savedMaximum);
            PlayerPrefs.Save(); app.input.useInjectedInput = false;
            Write("LivePlayMode", success ? "PASS — " + checks.Count + " assertions" : "FAILED");
        }
        static void Call(string name, params object[] args) => typeof(Body24App).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(app, args);
        static HandSample Sample(Vector3 p, bool ok = false) => new HandSample { valid = true, openPalmUp = true, palm = p, tip = p, ok = ok };
        static void Support()
        {
            if (app.State == AppState.Settings)
            {
                // Settings span the support lanes. Park the simulated hands clear of all controls between holds.
                app.input.Inject(Sample(app.frame.TransformPoint(new Vector3(-.7f, .04f, .12f))), Sample(app.frame.TransformPoint(new Vector3(.7f, .04f, .12f))), true);
                return;
            }
            app.input.Inject(Sample(app.frame.TransformPoint(new Vector3(-app.LaneHalfWidth, .04f, .44f))), Sample(app.frame.TransformPoint(new Vector3(app.LaneHalfWidth, .04f, .44f))), true);
        }
        static IEnumerator Wait(float seconds) { double until = EditorApplication.timeSinceStartup + seconds; while (EditorApplication.timeSinceStartup < until) yield return null; }
        static IEnumerator Hold(HoldTarget target, float seconds = 1.12f)
        {
            Support(); yield return Wait(.10f);
            Vector3 p = target.transform.position + Vector3.up * .01f;
            var other = app.input.Hands[1];
            app.input.Inject(Sample(p), other, true);
            Physics.SyncTransforms(); yield return Wait(seconds); Support(); yield return Wait(.12f);
        }
        static IEnumerator Live()
        {
            UnityEngine.Application.runInBackground = true;
            Call("Enter", AppState.Calibration);
            app.input.head.transform.SetPositionAndRotation(new Vector3(0, 1.7f, 0), Quaternion.Euler(40, 0, 0));
            app.input.Inject(Sample(new Vector3(-.26f, 1.25f, .55f)), Sample(new Vector3(.26f, 1.25f, .55f)), true);
            yield return Wait(1.9f);
            Check(app.State == AppState.Placement, "Standing calibration advances after stable valid samples");
            app.input.Inject(Sample(new Vector3(-.26f, 1.25f, .55f), true), Sample(new Vector3(.26f, 1.25f, .55f)), true);
            yield return Wait(1.2f);
            Check(app.State == AppState.TutorialOffer, "OK hold confirms placement; first launch offers tutorial");
            app.input.head.transform.SetPositionAndRotation(app.frame.TransformPoint(new Vector3(0, .65f, .1f)), app.frame.rotation * Quaternion.Euler(62, 0, 0));
            Support(); yield return Hold(app.actions[0]);
            Check(app.State == AppState.PlankStart, "Ground menu uses shared hand hold");
            yield return Wait(4.3f); Check(app.State == AppState.Tutorial1, "Shared 3-second countdown plus 1-second START");
            yield return Hold(app.cards[1]); Check(app.Puzzle.First == -1, "Unexpected tutorial input cannot break guidance");
            yield return Hold(app.cards[0], .45f); Check(app.Puzzle.First == -1, "Live early exit cancels hold");
            yield return Hold(app.cards[0]); Check(app.Puzzle.First == 0, "Live one-second card contact selects");
            yield return Hold(app.operators[0]); yield return Hold(app.cards[1]); yield return Wait(.5f);
            Check(app.Puzzle.Cards[0].Value.Equals(new Rational(8)) && !app.Puzzle.Cards[1].HasValue && app.Puzzle.First == -1, "Live second-to-first merge leaves result unselected");
            yield return Hold(app.cards[2]); yield return Hold(app.operators[3]); yield return Hold(app.cards[3]); yield return Wait(.5f);
            yield return Hold(app.cards[0]); yield return Hold(app.operators[2]); yield return Hold(app.cards[2]); yield return Wait(1.2f);
            Check(app.State == AppState.Tutorial2 && app.Puzzle.Original[3] == 10, "Guided math advances to unsolvable tutorial stage");
            for (int i = 0; i < 4; i++)
            {
                Support(); yield return Wait(.15f); int hand = i % 2;
                var near = Sample(app.input.head.transform.position + Vector3.right * .12f);
                app.input.Inject(hand == 0 ? near : app.input.Hands[0], hand == 1 ? near : app.input.Hands[1], true);
                yield return Wait(.45f);
            }
            Support(); yield return Wait(.8f);
            Check(app.Puzzle.Original[3] == 4 && Solver24.Solvable(app.Puzzle.Original), "Four alternating gestures reroll tutorial 10 to 4");
            yield return Hold(app.cards[2]); yield return Hold(app.operators[2]); yield return Hold(app.cards[3]); yield return Wait(.5f);
            yield return Hold(app.cards[0]); yield return Hold(app.operators[0]); yield return Hold(app.cards[1]); yield return Wait(.5f);
            yield return Hold(app.cards[2]); yield return Hold(app.operators[1]); yield return Hold(app.cards[0]); yield return Wait(1.2f);
            Check(app.State == AppState.Menu, "Tutorial free solve returns to menu");
            Call("Begin", AppState.Classic); Support(); yield return Wait(4.3f);
            Check(app.State == AppState.Classic && Solver24.Solvable(app.Puzzle.Original), "Classic starts with a solver-validated puzzle");
            app.Puzzle.Load(new[] { 3, 5, 6, 2 }); Call("ShowPuzzle");
            Support(); yield return Wait(.1f);
            app.input.Inject(app.input.Hands[0], Sample(app.cards[1].transform.position), true); yield return Wait(.3f);
            app.input.Inject(Sample(app.cards[0].transform.position), app.input.Hands[1], true); yield return Wait(.3f);
            Check(app.Puzzle.First == 1, "Earlier-entered right hand wins concurrent card contact");
            yield return Wait(.6f); Check(app.Puzzle.First == 0, "Other hand replaces first number while first hand remains on its card");
            yield return Wait(.7f); Check(app.Puzzle.First == 0, "Stationary confirmed hands do not repeat or bounce selection");
            app.input.Inject(Sample(app.operators[0].transform.position), app.input.Hands[1], true); yield return Wait(.65f);
            Check(app.Puzzle.First == 0 && app.Puzzle.Operator == Operation.Add, "Same hand can slide directly to another button while opposite hand stays on its card");
            Support(); yield return Wait(.15f); Call("ResetPuzzle"); yield return Wait(.15f);
            app.input.Inject(Sample(app.cards[0].transform.position), app.input.Hands[1], true); yield return Wait(.25f);
            var lifted = app.input.Hands[1]; lifted.palm.y += .25f; lifted.tip = lifted.palm;
            app.input.Inject(app.input.Hands[0], lifted, true); yield return Wait(.9f);
            Check(app.Puzzle.First == 0 && app.Puzzle.Bonuses == 1, "Live lift within 0.5 seconds awards exactly one bonus");
            yield return Wait(.4f); Check(app.Puzzle.Bonuses == 1, "Lift bonus is not awarded per frame");
            Support(); yield return Wait(.15f); Call("ResetPuzzle"); yield return Wait(.15f);
            app.input.Inject(Sample(app.cards[0].transform.position), lifted, true); yield return Wait(1.2f);
            Check(app.Puzzle.First == 0 && app.Puzzle.Bonuses == 0, "Already lifted opposite hand permits normal selection without bonus");
            Support(); yield return Wait(.15f); Call("ResetPuzzle"); yield return Wait(.15f);
            app.input.Inject(Sample(app.cards[0].transform.position), app.input.Hands[1], true); yield return Wait(.25f);
            app.input.Inject(default, app.input.Hands[1], true); yield return Wait(.75f);
            Check(app.Puzzle.First == -1, "Live hand-tracking loss cannot complete a hold");
            Support(); yield return Wait(.15f);
            yield return Hold(app.cards[0]); yield return Hold(app.operators[0]); yield return Hold(app.cards[1]); yield return Wait(.5f);
            yield return Hold(app.cards[2]); yield return Hold(app.operators[3]); yield return Hold(app.cards[3]); yield return Wait(.5f);
            yield return Hold(app.cards[0]); yield return Hold(app.operators[2]); yield return Hold(app.cards[2]); yield return Wait(2.7f);
            Check(app.SolvedCount == 1 && app.TotalScore > 0 && app.State == AppState.Classic && app.Puzzle.Combinations == 0, "Classic reward increments score and starts the next puzzle");
            string before = string.Join(",", app.Puzzle.Original);
            app.input.head.transform.position += Vector3.up;
            yield return Wait(.9f); Check(app.State == AppState.Paused, "Leaving presence volume pauses");
            float frozen = app.SessionSeconds; yield return Wait(.8f);
            Check(Mathf.Abs(app.SessionSeconds - frozen) < .01f, "Active timer excludes paused time");
            app.input.head.transform.position -= Vector3.up; yield return Wait(.3f);
            Check(app.State == AppState.Classic && string.Join(",", app.Puzzle.Original) == before, "Returning resumes the exact puzzle");
            app.input.head.transform.position += Vector3.up; yield return Wait(.9f);
            app.input.Inject(Sample(app.input.Hands[0].palm, true), app.input.Hands[1], true); yield return Wait(1.2f);
            Check(app.State == AppState.Results && !string.IsNullOrEmpty(app.resultsLabel.text), "OK hold ends paused session with statistics");
            app.input.head.transform.position -= Vector3.up; Support();
            Call("Begin", AppState.Tutorial1); yield return Wait(4.3f);
            app.input.head.transform.position += Vector3.up; yield return Wait(.9f);
            Check(app.State == AppState.Paused, "Tutorial presence loss starts its exit countdown");
            yield return Wait(3.3f); Check(app.State == AppState.Menu, "Tutorial exits after 3 seconds outside");
            app.input.head.transform.position -= Vector3.up; Support();
            Call("Begin", AppState.Classic); yield return Wait(4.3f);
            app.input.head.transform.position += Vector3.up; yield return Wait(.9f);
            yield return Wait(15.3f);
            Check(app.State == AppState.Results && app.SolvedCount == 0 && app.resultsLabel.text.Contains("—"), "15-second pause timeout ends session; zero-solve average has placeholder");
            app.input.head.transform.position -= Vector3.up; Support(); Call("Enter", AppState.Settings);
            yield return Hold(app.actions[3]); Check(app.MaximumNumber == 13 && PlayerPrefs.GetInt("BODY24.Max") == 13, "Maximum-number selection is mutually exclusive and persisted");
            yield return Hold(app.actions[6]); Check(app.State == AppState.Calibration, "Recalibration reuses the existing floor frame");
            Check(UnityEngine.Object.FindObjectsByType<PlayzoneView>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length == 1, "Recalibration creates no duplicate playzones");
            Call("Enter", AppState.Menu);
        }
    }
}
