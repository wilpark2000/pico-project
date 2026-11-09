using System;
using System.Collections.Generic;
using System.Linq;

namespace Body24
{
    [Serializable]
    public struct Rational : IEquatable<Rational>
    {
        public long numerator, denominator;
        public Rational(long n, long d = 1)
        {
            if (d == 0) throw new DivideByZeroException();
            if (d < 0) { n = -n; d = -d; }
            long a = Math.Abs(n), b = d;
            while (b != 0) { long t = a % b; a = b; b = t; }
            numerator = n / a; denominator = d / a;
        }
        public bool Equals(Rational other) => numerator == other.numerator && denominator == other.denominator;
        public override bool Equals(object other) => other is Rational r && Equals(r);
        public override int GetHashCode() => numerator.GetHashCode() ^ denominator.GetHashCode();
        public override string ToString() => denominator == 1 ? numerator.ToString() : numerator + "/" + denominator;
        public static Rational Apply(Rational a, Operation op, Rational b)
        {
            checked
            {
                switch (op)
                {
                    case Operation.Add: return new Rational(a.numerator * b.denominator + b.numerator * a.denominator, a.denominator * b.denominator);
                    case Operation.Subtract: return new Rational(a.numerator * b.denominator - b.numerator * a.denominator, a.denominator * b.denominator);
                    case Operation.Multiply: return new Rational(a.numerator * b.numerator, a.denominator * b.denominator);
                    default: return new Rational(a.numerator * b.denominator, a.denominator * b.numerator);
                }
            }
        }
    }
    public enum Operation { Add, Subtract, Multiply, Divide }
    public static class Solver24
    {
        public static bool Solvable(IEnumerable<int> numbers) => Solve(numbers.Select(x => new Rational(x)).ToList());
        static bool Solve(List<Rational> values)
        {
            if (values.Count == 1) return values[0].Equals(new Rational(24));
            for (int i = 0; i < values.Count; i++)
                for (int j = i + 1; j < values.Count; j++)
                {
                    var rest = values.Where((v, k) => k != i && k != j).ToList();
                    var seen = new HashSet<Rational>();
                    for (int direction = 0; direction < 2; direction++)
                        foreach (Operation op in Enum.GetValues(typeof(Operation)))
                        {
                            var a = values[direction == 0 ? i : j]; var b = values[direction == 0 ? j : i];
                            if (op == Operation.Divide && b.numerator == 0) continue;
                            var result = Rational.Apply(a, op, b);
                            if (!seen.Add(result)) continue;
                            rest.Add(result);
                            if (Solve(rest)) return true;
                            rest.RemoveAt(rest.Count - 1);
                        }
                }
            return false;
        }
        public static int[] Generate(Random rng, int maximum)
        {
            ValidateGenerator(rng, maximum);
            var numbers = Enumerable.Range(0, 4).Select(_ => rng.Next(1, maximum + 1)).ToArray();
            return EnsureSolvable(numbers, rng, maximum);
        }
        // Repair before dealing cards, so an unsolvable set is never displayed.
        public static int[] EnsureSolvable(int[] numbers, Random rng, int maximum)
        {
            ValidateGenerator(rng, maximum);
            if (numbers == null || numbers.Length != 4 || numbers.Any(n => n < 1 || n > maximum))
                throw new ArgumentException("Expected four numbers in the configured range.", nameof(numbers));
            var candidate = (int[])numbers.Clone();
            for (int attempt = 0; attempt < 64; attempt++)
            {
                if (Solvable(candidate)) return candidate;
                int slot = rng.Next(4);
                // Draw from every value except the current one: each attempt changes a card.
                int replacement = rng.Next(1, maximum);
                if (replacement >= candidate[slot]) replacement++;
                candidate[slot] = replacement;
            }
            if (Solvable(candidate)) return candidate;
            // Guaranteed solution even if repeated replacements make no progress: (3*3-1)*3.
            return new[] { 1, 3, 3, 3 };
        }
        static void ValidateGenerator(Random rng, int maximum)
        {
            if (rng == null) throw new ArgumentNullException(nameof(rng));
            // The game's settings use 10–13; the solver is bounded to small card values.
            if (maximum < 3 || maximum > 13) throw new ArgumentOutOfRangeException(nameof(maximum), "Maximum must be between 3 and 13.");
        }
        public static int ReplacementIndex(int[] numbers)
        {
            for (int i = 0; i < 4; i++) if (numbers.Count(v => v == numbers[i]) > 1) return i;
            return Array.IndexOf(numbers, numbers.Max());
        }
        public static bool Reroll(int[] numbers, int maximum, Random rng, out int slot, out int replacement)
        {
            slot = ReplacementIndex(numbers); replacement = numbers[slot];
            var candidates = new List<int>();
            for (int n = 1; n <= maximum; n++)
            {
                if (n == numbers[slot]) continue;
                var trial = (int[])numbers.Clone(); trial[slot] = n;
                if (Solvable(trial)) candidates.Add(n);
            }
            if (candidates.Count == 0) return false;
            replacement = candidates[rng.Next(candidates.Count)]; return true;
        }
        public static int Score(int count, float activeSeconds) => (int)Math.Round(100 * Math.Pow(1.2, count) * (activeSeconds <= 15 ? 1.5 : 1), MidpointRounding.AwayFromZero);
    }
    public sealed class PuzzleModel
    {
        public int[] Original { get; private set; }
        public Rational?[] Cards { get; private set; }
        public bool[] Intermediate { get; private set; }
        public int First { get; private set; } = -1;
        public Operation? Operator { get; private set; }
        public int Bonuses { get; set; }
        public int Combinations { get; private set; }
        public bool Solved => Cards.Count(v => v.HasValue) == 1 && Cards.Any(v => v.HasValue && v.Value.Equals(new Rational(24)));
        public void Load(int[] values) { Original = (int[])values.Clone(); Reset(); }
        public void Reset()
        {
            Cards = Original.Select(v => (Rational?)new Rational(v)).ToArray(); Intermediate = new bool[4];
            First = -1; Operator = null; Bonuses = 0; Combinations = 0;
        }
        public bool SelectOperator(Operation op) { if (First < 0) return false; Operator = op; return true; }
        public bool SelectNumber(int slot, out int destination)
        {
            destination = -1;
            if (slot < 0 || slot > 3 || !Cards[slot].HasValue) return false;
            if (First < 0 || !Operator.HasValue) { First = slot; return true; }
            if (First == slot) return false;
            if (Operator == Operation.Divide && Cards[slot].Value.numerator == 0) return false;
            destination = First;
            Cards[First] = Rational.Apply(Cards[First].Value, Operator.Value, Cards[slot].Value);
            Intermediate[First] = true; Cards[slot] = null;
            First = -1; Operator = null; Combinations++; return true;
        }
        public void Replace(int slot, int value)
        {
            if (Combinations != 0) throw new InvalidOperationException("Reroll requires four original cards.");
            Original[slot] = value; Reset();
        }
    }
}
