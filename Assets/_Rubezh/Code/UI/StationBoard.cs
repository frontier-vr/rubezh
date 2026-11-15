using System.Text;
using Rubezh.Core;
using Rubezh.Gameplay;
using TMPro;
using UnityEngine;

namespace Rubezh.UI
{
    // Табло над столом или стойкой: что делать на текущем шаге и как — по пунктам, с кнопками
    // контроллера. Висит над каждым местом (шкаф, полка СИЗ, стол снаряжения, рубеж), чтобы прочитать
    // порядок действий, подняв голову, и не оборачиваться на табло команд O-31.
    // Табло места, где идёт текущий шаг, показывает заголовок и пункты и горит меткой; остальные —
    // какой шаг идёт и куда за ним идти. Тексты — из Data/Hints/script.ru.json (docs/script.md §6),
    // кнопки {Eject} и {Safety} — по руке с пистолетом. Где висят табло — docs/layout.md §4.1.
    public sealed class StationBoard : MonoBehaviour
    {
        private const string DimColor = "#A3AAB0";    // вторичный текст, docs/palette.md

        [SerializeField] private ExerciseRunner runner;
        [SerializeField] private TextAsset script;
        [Tooltip("Рука с пистолетом выбирает буквы кнопок; пусто — правая")]
        [SerializeField] private WeaponController weapon;
        [Tooltip("Над каким местом висит табло")]
        [SerializeField] private Station station = Station.Cabinet;
        [Header("Панель")]
        [SerializeField] private TMP_Text title;
        [SerializeField] private TMP_Text body;
        [Tooltip("Полоса-метка: включена, пока текущий шаг выполняется у этого места")]
        [SerializeField] private GameObject hereMark;

        private InstructorScript _script;

        public Station Station => station;
        // Текущий шаг выполняется у этого места.
        public bool IsHere { get; private set; }
        public string Title => title != null ? title.text : string.Empty;
        public string Body => body != null ? body.text : string.Empty;

        public void Configure(ExerciseRunner exerciseRunner, TextAsset scriptAsset, WeaponController weaponController, Station place)
        {
            Unsubscribe();
            runner = exerciseRunner; script = scriptAsset; weapon = weaponController; station = place;
            _script = null;
            Subscribe();
            Refresh();
        }

        private void OnEnable()
        {
            Subscribe();
            Refresh();
        }

        private void OnDisable() => Unsubscribe();

        private void Subscribe()
        {
            Unsubscribe();
            if (runner != null)
            {
                runner.PhaseChanged += OnPhaseChanged;
                runner.Restarted += Refresh;
            }
            if (weapon != null) weapon.HeldChanged += OnHeldChanged;
        }

        private void Unsubscribe()
        {
            if (runner != null)
            {
                runner.PhaseChanged -= OnPhaseChanged;
                runner.Restarted -= Refresh;
            }
            if (weapon != null) weapon.HeldChanged -= OnHeldChanged;
        }

        private void OnPhaseChanged(Phase from, Phase to) => Refresh();
        private void OnHeldChanged(bool held, Hand hand) => Refresh();    // пистолет в другой руке — другие буквы кнопок

        public void Refresh()
        {
            if (runner == null || runner.Director == null) return;
            _script ??= InstructorScript.From(script);
            if (_script == null) return;

            var phase = runner.Phase;
            var card = _script.StationFor(phase);
            if (card == null)
            {
                // Для фазы табло не написано: остаётся команда инструктора.
                Show(false, _script.Command(phase), string.Empty);
                return;
            }

            bool here = card.Station == station || card.Station == Station.Any;
            if (here) Show(true, card.Title, Steps(card));
            else Show(false, Dim(_script.BoardNow(card.Title)), Dim(_script.BoardElsewhere(card.Station)));
        }

        private void Show(bool here, string titleText, string bodyText)
        {
            IsHere = here;
            if (title != null) title.text = titleText;
            if (body != null) body.text = bodyText;
            if (hereMark != null && hereMark.activeSelf != here) hereMark.SetActive(here);
        }

        private string Steps(InstructorScript.StationCard card)
        {
            var hand = weapon != null ? weapon.PrimaryHand : Hand.Right;
            var sb = new StringBuilder();
            for (int i = 0; i < card.Steps.Count; i++)
            {
                if (i > 0) sb.Append('\n');
                sb.Append(i + 1).Append(". ").Append(InstructorScript.WithButtons(card.Steps[i], hand));
            }
            return sb.ToString();
        }

        private static string Dim(string text) => "<color=" + DimColor + ">" + text + "</color>";
    }
}
