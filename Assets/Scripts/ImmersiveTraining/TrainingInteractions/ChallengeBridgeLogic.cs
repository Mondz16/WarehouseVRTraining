using System;
using System.Collections.Generic;

namespace ImmersiveTraining.TrainingInteractions
{
    // Plain C# (no UnityEngine) decision logic behind the Order Picking bridge scripts, kept separate so
    // it can be unit-tested outside Unity: dotnet run --project Tests/OrderPickingLogic

    /// <summary>
    /// Latches a bridge's completion only once its challenge has accepted it.
    ///
    /// ChallengeTutorialTrigger ignores completions while its step is not active and does not remember
    /// them, so a bridge that latched on its first attempt could never complete the step later (the
    /// trainee acted early → soft-lock). Bridges offer the completion whenever their condition changes
    /// and again when the challenge arms (<c>ChallengeTutorialTrigger.Armed</c>).
    /// </summary>
    public sealed class ArmedLatch
    {
        /// <summary>True once a completion has been accepted.</summary>
        public bool IsLatched { get; private set; }

        /// <summary>
        /// Offers the completion if <paramref name="conditionMet"/>. <paramref name="tryComplete"/> returns
        /// whether the challenge accepted it (null = nothing to accept). Returns true only for the call
        /// that latched.
        /// </summary>
        public bool Offer(bool conditionMet, Func<bool> tryComplete)
        {
            if (IsLatched || !conditionMet) return false;

            // Latch before completing: accepting can re-enter the bridge (the framework arms the next
            // challenge and its Armed handler offers again), which must not complete twice.
            IsLatched = true;
            if (tryComplete == null || tryComplete()) return true;

            IsLatched = false;
            return false;
        }

        public void Reset() => IsLatched = false;
    }

    /// <summary>What is currently inside a tote, keyed by item instance id.</summary>
    public sealed class ToteTally
    {
        private readonly Dictionary<int, string> _inside = new Dictionary<int, string>();
        private readonly HashSet<string> _ignoredSkus = new HashSet<string>();

        /// <summary>SKU being picked. Empty accepts any SKU.</summary>
        public string RequiredSku { get; set; }

        public int RequiredCount { get; set; } = 1;

        /// <summary>If true, the target is only met while no other (non-ignored) item is inside.</summary>
        public bool RequireNoOtherItems { get; set; }

        public int MatchingCount
        {
            get
            {
                int count = 0;
                foreach (string sku in _inside.Values)
                    if (Matches(sku)) count++;
                return count;
            }
        }

        /// <summary>Items that neither match nor are ignored (e.g. decoys).</summary>
        public int OtherCount
        {
            get
            {
                int count = 0;
                foreach (string sku in _inside.Values)
                    if (!Matches(sku) && !IsIgnored(sku)) count++;
                return count;
            }
        }

        public bool IsTargetMet => MatchingCount >= RequiredCount && (!RequireNoOtherItems || OtherCount == 0);

        public void SetIgnoredSkus(IEnumerable<string> skus)
        {
            _ignoredSkus.Clear();
            if (skus == null) return;

            foreach (string sku in skus)
                if (!string.IsNullOrEmpty(sku)) _ignoredSkus.Add(sku);
        }

        public bool Matches(string sku) => string.IsNullOrEmpty(RequiredSku) || sku == RequiredSku;

        public bool IsIgnored(string sku) => sku != null && _ignoredSkus.Contains(sku);

        public bool Contains(int id) => _inside.ContainsKey(id);

        public void Enter(int id, string sku) => _inside[id] = sku;

        public bool Exit(int id, out string sku)
        {
            if (!_inside.TryGetValue(id, out sku)) return false;
            _inside.Remove(id);
            return true;
        }

        public void Clear() => _inside.Clear();
    }

    /// <summary>
    /// How many completion sources are satisfied right now (multi-line order, M1_06). Sources report
    /// their live state, so a line that stops being met (items taken back out) stops counting.
    /// </summary>
    public sealed class MultiSourceGate
    {
        private readonly HashSet<object> _metSources = new HashSet<object>();
        private int _notifications;

        /// <summary>Met sources plus one-off notifications.</summary>
        public int Count => _metSources.Count + _notifications;

        /// <summary>Records a source's current state. Returns true if that changed the count.</summary>
        public bool SetSourceMet(object source, bool met)
        {
            if (source == null) return false;
            return met ? _metSources.Add(source) : _metSources.Remove(source);
        }

        /// <summary>Counts a one-off completion (sources wired through a UnityEvent only report success).</summary>
        public void Notify() => _notifications++;

        public void Reset()
        {
            _metSources.Clear();
            _notifications = 0;
        }
    }

    /// <summary>
    /// Mis-pick scenario state (M1_07): the wrong item must have been in the tote and be out of it now;
    /// the correct item must be in it.
    /// </summary>
    public sealed class MisPickTracker
    {
        private readonly HashSet<int> _wrongInside = new HashSet<int>();
        private readonly HashSet<int> _correctInside = new HashSet<int>();

        public string WrongSku { get; set; }
        public string CorrectSku { get; set; }

        /// <summary>True once a wrong item has been inside the tote at least once.</summary>
        public bool WrongSeen { get; private set; }

        public bool WrongItemRemoved => WrongSeen && _wrongInside.Count == 0;

        public bool CorrectItemInside => _correctInside.Count > 0;

        public void Enter(int id, string sku)
        {
            if (!string.IsNullOrEmpty(WrongSku) && sku == WrongSku)
            {
                _wrongInside.Add(id);
                WrongSeen = true;
            }
            else if (!string.IsNullOrEmpty(CorrectSku) && sku == CorrectSku)
            {
                _correctInside.Add(id);
            }
        }

        public void Exit(int id)
        {
            _wrongInside.Remove(id);
            _correctInside.Remove(id);
        }
    }
}
