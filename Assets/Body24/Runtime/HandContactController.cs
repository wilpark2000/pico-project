using UnityEngine;
namespace Body24
{
    public class HandContactController : MonoBehaviour
    {
        public Body24Config config;
        public TrackedHands input;
        public HoldTarget[] targets;
        public bool suspended;
        readonly HoldTarget[] contacts = new HoldTarget[2];
        readonly float[] entered = new float[2];
        readonly bool[] supported = new bool[2];
        readonly bool[] blockedUntilExit = new bool[2];
        readonly HoldTarget[] confirmedContacts = new HoldTarget[2];
        readonly LiftBonus bonus = new LiftBonus();
        readonly ContinuousHold hold = new ContinuousHold();
        HoldTarget active;
        int owner = -1;
        public bool Holding => active && hold.Elapsed > 0 && !hold.Latched;
        public void Cancel(bool requireExit = true)
        {
            if (active) active.Fill(0);
            active = null; owner = -1; hold.Clear();
            if (requireExit) blockedUntilExit[0] = blockedUntilExit[1] = true;
        }
        void Update()
        {
            bool began = false;
            for (int i = 0; i < 2; i++)
            {
                var hand = input.Hands[i];
                if (!hand.valid) supported[i] = false;
                else if (hand.palm.y - config.floorHeight < config.supportHeight) supported[i] = true;
                else if (hand.palm.y - config.floorHeight > config.liftHeight) supported[i] = false;
                HoldTarget next = null;
                if (!suspended && input.HeadValid && hand.valid)
                    foreach (var target in targets) if (target && target.Contains(hand, input.GetProbeCollider(i))) { next = target; break; }
                if (next != contacts[i]) { contacts[i] = next; entered[i] = Time.unscaledTime; }
                if (next != confirmedContacts[i]) confirmedContacts[i] = null;
                if (!next) blockedUntilExit[i] = false;
            }
            if (suspended || !input.HeadValid) { Cancel(); return; }
            if (active && (contacts[owner] != active || !active.Interactable)) Cancel(false);
            if (!active)
            {
                int candidate = -1;
                for (int i = 0; i < 2; i++)
                    if (contacts[i] && !blockedUntilExit[i] && contacts[i] != confirmedContacts[i] && (candidate < 0 || entered[i] < entered[candidate])) candidate = i;
                if (candidate < 0) return;
                owner = candidate; active = contacts[owner]; hold.Clear();
                began = true;
                bonus.Begin(input.Hands[1 - owner].valid, supported[1 - owner]);
            }
            float step = began ? 0 : Time.unscaledDeltaTime;
            bonus.Tick(hold.Elapsed + step, input.Hands[1 - owner].valid, supported[1 - owner], config.bonusWindow);
            bool confirmed = active.activateOnPress || hold.Tick(true, step, config.holdSeconds);
            active.Fill(active.activateOnPress ? 0 : Mathf.Clamp01(hold.Elapsed / config.holdSeconds));
            if (confirmed)
            {
                var target = active; bool earned = target.gameplay && bonus.Qualified;
                // Prevent repeats on this button without blocking another hand or button.
                for (int i = 0; i < 2; i++) if (contacts[i] == target) confirmedContacts[i] = target;
                // Release ownership before invoking callbacks, which may cancel or suspend input.
                Cancel(false);
                target.action?.Invoke(earned);
            }
        }
    }
}
