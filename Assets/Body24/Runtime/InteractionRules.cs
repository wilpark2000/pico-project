using System;
namespace Body24
{
    // Pure state objects: headset adapters and Editor simulation share these rules.
    public sealed class ContinuousHold
    {
        public float Elapsed { get; private set; }
        public bool Latched { get; private set; }
        public bool Tick(bool contact, float delta, float duration)
        {
            if (!contact) { Elapsed = 0; Latched = false; return false; }
            if (Latched) return false;
            Elapsed += delta;
            if (Elapsed + 0.00001f < duration) return false;
            Latched = true; return true;
        }
        public void Clear() { Elapsed = 0; Latched = false; }
    }
    public sealed class LiftBonus
    {
        bool eligible, lifted;
        public void Begin(bool valid, bool supported) { eligible = valid && supported; lifted = false; }
        public void Tick(float elapsed, bool valid, bool supported, float window)
        {
            if (!valid) eligible = false;
            if (!eligible) return;
            if (!lifted && !supported) { if (elapsed <= window) lifted = true; else eligible = false; }
            if (lifted && supported) eligible = false;
            if (!lifted && elapsed > window) eligible = false;
        }
        public bool Qualified => eligible && lifted;
    }
    public sealed class AlternatingTaps
    {
        readonly bool[] inside = new bool[2];
        float lastTime = -100;
        int lastHand = -1;
        public int Count { get; private set; }
        public bool Tick(int hand, bool valid, bool near, float now, float cooldown)
        {
            if (!valid) { inside[hand] = true; return false; }
            if (!near) { inside[hand] = false; return false; }
            if (inside[hand]) return false;
            inside[hand] = true;
            if (hand == lastHand || now - lastTime < cooldown) return false;
            lastHand = hand; lastTime = now; Count++; return Count == 4;
        }
        public void Reset() { Count = 0; lastHand = -1; lastTime = -100; inside[0] = inside[1] = true; }
    }
}
