using System.Collections.Generic;

namespace Rubezh.Gameplay
{
    // Выноска у контроллера (NG-32, docs/user-flow.md §3, §4.0): одна на двоих, три вида сообщений
    // по приоритету — замечание о мелком нарушении > фраза подсказки FR-26 > онбординг. Здесь только
    // очередь и время, без движка; рисует ControllerCallouts в XR.
    // Онбординг — каждая выноска один раз за сессию (до выхода из приложения, Restart не сбрасывает).
    public sealed class Callouts
    {
        public enum Kind { None, Onboarding, Hint, Remark }

        // Показанные выноски онбординга — на всю сессию.
        private static readonly HashSet<string> s_onboardingShown = new HashSet<string>();

        private readonly double _remarkSeconds;
        private readonly double _onboardingSeconds;

        private string _remark; private Hand _remarkHand; private double _remarkUntil;
        private string _hint; private Hand _hintHand;
        private string _onbId, _onb; private Hand _onbHand; private double _onbUntil;

        public Callouts(double remarkSeconds, double onboardingSeconds)
        {
            _remarkSeconds = remarkSeconds;
            _onboardingSeconds = onboardingSeconds;
        }

        public Kind Current { get; private set; }
        public string Text { get; private set; }
        public Hand Hand { get; private set; }
        public string OnboardingId => Current == Kind.Onboarding ? _onbId : null;

        public static bool WasShown(string onboardingId) => s_onboardingShown.Contains(onboardingId);

        // Для тестов: начать «новую сессию» приложения.
        public static void ForgetSession() => s_onboardingShown.Clear();

        public void Remark(string text, Hand hand, double now)
        {
            _remark = text; _remarkHand = hand; _remarkUntil = now + _remarkSeconds;
            Resolve(now);
        }

        public void Hint(string text, Hand hand, double now)
        {
            _hint = text; _hintHand = hand;
            Resolve(now);
        }

        public void ClearHint(double now)
        {
            _hint = null;
            Resolve(now);
        }

        // Показать выноску онбординга, если её ещё не было за сессию. true — показана сейчас.
        // Пока висит другая выноска онбординга, новая не показывается и не считается показанной:
        // её повод повторится (наведение, захват), и она выйдет следом.
        public bool Onboarding(string id, string text, Hand hand, double now)
        {
            Resolve(now);
            if (string.IsNullOrEmpty(text) || s_onboardingShown.Contains(id)) return false;
            if (_onb != null && _onbId != id) return false;
            s_onboardingShown.Add(id);
            _onbId = id; _onb = text; _onbHand = hand; _onbUntil = now + _onboardingSeconds;
            Resolve(now);
            return true;
        }

        // Действие, о котором говорит выноска, сделано — она снимается.
        public void DoneOnboarding(string id, double now)
        {
            if (_onbId != id) return;
            _onb = null; _onbId = null;
            Resolve(now);
        }

        // На «Повторить»: замечание и подсказка уходят, онбординг не возвращается.
        public void Reset(double now)
        {
            _remark = null; _hint = null; _onb = null; _onbId = null;
            Resolve(now);
        }

        public void Tick(double now) => Resolve(now);

        private void Resolve(double now)
        {
            if (_remark != null && now >= _remarkUntil) _remark = null;
            if (_onb != null && now >= _onbUntil) { _onb = null; _onbId = null; }
            if (_remark != null) Set(Kind.Remark, _remark, _remarkHand);
            else if (_hint != null) Set(Kind.Hint, _hint, _hintHand);
            else if (_onb != null) Set(Kind.Onboarding, _onb, _onbHand);
            else Set(Kind.None, null, Hand.Right);
        }

        private void Set(Kind kind, string text, Hand hand)
        {
            Current = kind;
            Text = text;
            Hand = hand;
        }
    }
}
