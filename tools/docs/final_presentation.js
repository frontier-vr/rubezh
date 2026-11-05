// Финальная презентация «Рубежа» (YG-15, защита 18.12): docs/presentation/rubezh-final.pptx.
// Слайды про модели — Маша (MM-16, docs/defense/models.md), про детекторы — Влад (VP-18,
// docs/defense/detectors.md). Картинки — из docs/img и docs/renders, цвета — docs/palette.md.
// Сборка: node tools/docs/final_presentation.js  (нужен пакет pptxgenjs)
const path = require("path");
const pptxgen = require("pptxgenjs");

const ROOT = path.resolve(__dirname, "..", "..");
const IMG = (p) => path.join(ROOT, "docs", p);
const OUT = path.join(ROOT, "docs", "presentation", "rubezh-final.pptx");

// Палитра проекта (docs/palette.md): фон экранов, графит, бетон, сигнальный жёлтый, подсветка, стоп, финиш.
const P = {
  bg: "101418", panel: "1D2228", steel: "4B535B", concrete: "8E8D88", light: "C9C7C0",
  text: "F2F0EA", muted: "A3AAB0", yellow: "F2C200", cyan: "1FB6C9", red: "D23A2A", green: "3E9E4F", ghost: "B9A8F0",
};
const FONT = "Arial";

// Размеры картинок, px — для пропорций.
const SIZE = {
  "img/yg-15-result-r1.png": [1600, 1100], "img/yg-15-stop-r3.png": [1280, 960],
  "img/yg-15-final-a1.png": [1280, 720], "img/yg-15-final-a3.png": [1280, 720],
  "img/vp-18-demo.png": [1280, 900], "img/m2-blocking-overview.png": [1280, 720],
  "img/ng-17-overview.png": [1280, 720], "img/ng-32-onboarding.png": [1280, 720],
  "img/mm-16-breech.png": [1280, 480], "img/vp-17-replay-frame.png": [640, 360],
  "renders/pistol.png": [1600, 1000], "renders/target_mechanism.png": [1600, 1000],
  "renders/pistol_wire.png": [1574, 896], "img/vp-07-look-into-muzzle.png": [1280, 720],
  "moodboard/wc_01.jpg": [500, 375], "moodboard/wc_02.jpg": [500, 375],
  "moodboard/wc_04.jpg": [500, 375], "moodboard/wc_06.jpg": [500, 375],
};

const pres = new pptxgen();
pres.layout = "LAYOUT_WIDE"; // 13.33 × 7.5
pres.title = "Рубеж — финальная защита";
pres.author = "Команда Рубежа";
pres.theme = { headFontFace: FONT, bodyFontFace: FONT };

// Картинка по ширине или высоте с сохранением пропорций; рамка — тонкий контур цвета стали.
function image(slide, p, x, y, { w, h, name }) {
  const [pw, ph] = SIZE[p];
  if (w && !h) h = w * ph / pw;
  if (h && !w) w = h * pw / ph;
  slide.addImage({ path: IMG(p), x, y, w, h, objectName: name || p });
  slide.addShape(pres.shapes.RECTANGLE, { x, y, w, h, fill: { type: "none" }, line: { color: P.steel, width: 1 }, objectName: (name || p) + " frame" });
  return { x, y, w, h };
}

function text(slide, t, opts) {
  slide.addText(t, Object.assign({ isTextBox: true, fontFace: FONT, color: P.text, margin: 0, valign: "top" }, opts));
}

// Заголовок контентного слайда: всегда на одном месте; слева — метка участника кружком.
function header(slide, title, who) {
  slide.background = { color: P.bg };
  text(slide, title, { x: 0.6, y: 0.45, w: 10.6, h: 0.8, fontSize: 32, bold: true, objectName: "Title" });
  if (who) {
    slide.addShape(pres.shapes.OVAL, { x: 11.55, y: 0.5, w: 0.62, h: 0.62, fill: { color: who.color }, line: { type: "none" }, objectName: "Who badge" });
    text(slide, who.initials, { x: 11.55, y: 0.5, w: 0.62, h: 0.62, fontSize: 16, bold: true, color: P.bg, align: "center", valign: "middle", objectName: "Who initials" });
    text(slide, who.name, { x: 9.2, y: 1.18, w: 3.5, h: 0.3, fontSize: 12, color: P.muted, align: "right", objectName: "Who name" });
  }
  slide.addText(String(slideNo++), { x: 12.3, y: 6.95, w: 0.5, h: 0.3, fontSize: 10, color: P.steel, align: "right", isTextBox: true, margin: 0 });
}
let slideNo = 1;

const TEAM = {
  nikita: { name: "Никита Григорьев · сборка, XR", initials: "НГ", color: P.cyan },
  vlad: { name: "Владислав Пахомов · логика", initials: "ВП", color: P.yellow },
  maria: { name: "Мария Махмудова · модели", initials: "ММ", color: P.ghost },
  yulia: { name: "Юлия · окружение, UX", initials: "Ю", color: P.green },
};

// Карточка: тёмная плашка, заголовок и текст.
function card(slide, x, y, w, h, title, body, accent, name) {
  slide.addShape(pres.shapes.ROUNDED_RECTANGLE, { x, y, w, h, rectRadius: 0.08, fill: { color: P.panel }, line: { type: "none" }, objectName: name + " card" });
  slide.addShape(pres.shapes.OVAL, { x: x + 0.25, y: y + 0.28, w: 0.22, h: 0.22, fill: { color: accent }, line: { type: "none" }, objectName: name + " dot" });
  text(slide, title, { x: x + 0.6, y: y + 0.2, w: w - 0.85, h: 0.42, fontSize: 18, bold: true, objectName: name + " title" });
  text(slide, body, { x: x + 0.25, y: y + 0.75, w: w - 0.5, h: h - 0.95, fontSize: 14, color: P.light, paraSpaceAfter: 4, objectName: name + " body" });
}

function bullets(items) {
  return items.map((t, i) => ({ text: t, options: { bullet: { indent: 14 }, breakLine: i < items.length - 1 } }));
}

// ---------------------------------------------------------------- 1. Титул
{
  const s = pres.addSlide();
  s.background = { color: P.bg };
  s.addImage({ path: IMG("img/yg-15-final-a3.png"), x: 6.6, y: 0, w: 6.73, h: 7.5, sizing: { type: "cover", w: 6.73, h: 7.5 }, objectName: "Hero" });
  text(s, "РУБЕЖ", { x: 0.7, y: 1.6, w: 5.6, h: 1.3, fontSize: 72, bold: true, charSpacing: 6, objectName: "Title" });
  text(s, "VR-тренажёр безопасного обращения с оружием в тире", { x: 0.7, y: 2.95, w: 5.4, h: 0.9, fontSize: 22, color: P.light });
  text(s, "«Ты держишь оружие — значит, ты всё время отвечаешь за то, куда оно смотрит»", { x: 0.7, y: 4.1, w: 5.4, h: 0.9, fontSize: 16, italic: true, color: P.yellow });
  text(s, "Никита Григорьев · Владислав Пахомов · Мария Махмудова · Юлия\nUnity 6.3 · URP · OpenXR · Meta Quest 2 · защита 18.12", { x: 0.7, y: 6.1, w: 5.6, h: 0.8, fontSize: 13, color: P.muted });
  s.addNotes("Рубеж — тренажёр состояния постоянного контроля. Меткость вторична: можно попасть в десятку и не сдать упражнение. Дальше — что сделали, как решали и что поменяли по ходу.");
  slideNo++;
}

// ---------------------------------------------------------------- 2. Идея в цифрах
{
  const s = pres.addSlide();
  header(s, "Упражнение целиком, контроль всё время");
  const stats = [["9", "шагов: от шкафа до шкафа", P.yellow], ["13", "нарушений, каждое ловится", P.red], ["3", "детектора работают фоном", P.cyan], ["≤ 6", "минут — цикл на повторе", P.green]];
  stats.forEach(([n, l, c], i) => {
    const x = 0.6 + i * 3.08;
    s.addShape(pres.shapes.ROUNDED_RECTANGLE, { x, y: 1.75, w: 2.8, h: 2.1, rectRadius: 0.08, fill: { color: P.panel }, line: { type: "none" }, objectName: "Stat " + i });
    text(s, n, { x: x + 0.3, y: 1.95, w: 2.3, h: 1.0, fontSize: 54, bold: true, color: c });
    text(s, l, { x: x + 0.3, y: 3.0, w: 2.3, h: 0.7, fontSize: 15, color: P.light });
  });
  text(s, bullets([
    "Аудитория — новички 18–25 лет: впервые берут оружие, VR знают, навязчивой опеки не терпят",
    "Инструктор — только голос и табло команд, людей в сцене нет",
    "О нарушениях заранее не предупреждаем: сначала действие, потом замечание; подсказка — после 5 с бездействия",
    "Почему VR: касание спуска отличается от нажатия только на контроллере, ствол и взгляд — из положения рук и головы",
  ]), { x: 0.6, y: 4.3, w: 12.1, h: 2.5, fontSize: 16, color: P.text, paraSpaceAfter: 8 });
  s.addNotes("Четыре числа держат весь проект: девять шагов упражнения, тринадцать нарушений, три фоновых детектора и шесть минут на цикл.");
}

// ---------------------------------------------------------------- 3. Цикл упражнения
{
  const s = pres.addSlide();
  header(s, "Девять шагов — по табло и шкале прогресса");
  const steps = ["Получение", "Осмотр", "СИЗ", "Снаряжение", "Рубеж", "Стрельба", "Задержка", "Разряжание", "Сдача"];
  const where = ["A1", "A1", "A1", "A2", "A3", "A3", "A3", "A3", "A1"];
  steps.forEach((t, i) => {
    const x = 0.6 + i * 1.36;
    s.addShape(pres.shapes.ROUNDED_RECTANGLE, { x, y: 2.0, w: 1.24, h: 1.3, rectRadius: 0.06, fill: { color: i === 5 ? P.yellow : P.panel }, line: { type: "none" }, objectName: "Step " + (i + 1) });
    text(s, String(i + 1), { x, y: 2.1, w: 1.24, h: 0.55, fontSize: 26, bold: true, align: "center", color: i === 5 ? P.bg : P.yellow });
    text(s, t, { x: x + 0.04, y: 2.7, w: 1.16, h: 0.4, fontSize: 12, align: "center", color: i === 5 ? P.bg : P.text });
    text(s, where[i], { x, y: 3.42, w: 1.24, h: 0.3, fontSize: 12, align: "center", color: P.muted });
  });
  image(s, "img/ng-17-overview.png", 0.6, 4.05, { h: 3.0, name: "Room overview" });
  text(s, bullets([
    "Три якоря телепорта: выдача A1, подготовка A2, рубеж A3; плавного хода нет",
    "Фаза закрывается по состоянию мира, а не по кнопке «дальше»",
    "Задержка — осечка или прихват гильзы, случайно с зерном",
  ]), { x: 6.2, y: 4.15, w: 6.5, h: 2.8, fontSize: 15, paraSpaceAfter: 8 });
  s.addNotes("Шкала прогресса O-43 под табло повторяет эти девять сегментов. Якорь под каждым шагом — где стоит пользователь.");
}

// ---------------------------------------------------------------- 4. До и после (Юля)
{
  const s = pres.addSlide();
  header(s, "Комната: блокинг M2 и финал", TEAM.yulia);
  image(s, "img/m2-blocking-overview.png", 0.6, 1.75, { w: 5.9, name: "Before" });
  image(s, "img/ng-17-overview.png", 6.83, 1.75, { w: 5.9, name: "After" });
  text(s, "09.10 · блокинг: серые коробки, жёлтые круги якорей", { x: 0.6, y: 5.2, w: 5.9, h: 0.35, fontSize: 13, color: P.muted });
  text(s, "06.11 · финал: атлас-палитра 2048, запечённый свет, разметка", { x: 6.83, y: 5.2, w: 5.9, h: 0.35, fontSize: 13, color: P.muted });
  text(s, bullets([
    "Планировка не менялась с блокинга: сначала проверили расстояния и сектор, потом рисовали",
    "Окружение — 2 608 треугольников комнаты из бюджета 6 000; вся сцена около 21 тыс.",
    "Свет запечён: 10 площадных источников, 165 зондов, без реального времени — ради 72 Гц",
  ]), { x: 0.6, y: 5.75, w: 12.1, h: 1.2, fontSize: 15, paraSpaceAfter: 6 });
  s.addNotes("Слева — блок-модель второй вехи, справа — финальная сцена. Сначала геометрия и расстояния, потом материалы и свет.");
}

// ---------------------------------------------------------------- 5. Дизайнерские решения (Юля)
{
  const s = pres.addSlide();
  header(s, "Откуда взялись решения", TEAM.yulia);
  ["moodboard/wc_01.jpg", "moodboard/wc_02.jpg", "moodboard/wc_04.jpg", "moodboard/wc_06.jpg"].forEach((p, i) => {
    image(s, p, 0.6 + (i % 2) * 2.95, 1.75 + Math.floor(i / 2) * 2.25, { w: 2.8, name: "Moodboard " + i });
  });
  const sw = [["8E8D88", "бетон"], ["4B535B", "металл"], ["F2C200", "разметка"], ["1FB6C9", "подсказка"], ["D23A2A", "стоп"], ["3E9E4F", "финиш"]];
  sw.forEach(([c, l], i) => {
    const x = 6.7 + i * 1.02;
    s.addShape(pres.shapes.ROUNDED_RECTANGLE, { x, y: 1.8, w: 0.85, h: 0.85, rectRadius: 0.08, fill: { color: c }, line: { type: "none" }, objectName: "Swatch " + l });
    text(s, l, { x: x - 0.1, y: 2.72, w: 1.05, h: 0.3, fontSize: 11, align: "center", color: P.muted });
  });
  text(s, bullets([
    "Мудборд настоящих тиров: бетон, металл, жёлтая разметка — спортивно-технически, без камуфляжа",
    "Один цвет — одна роль: голубая подсветка подсказки не спорит с жёлтой разметкой и красным стопом",
    "Прозрачности нет нигде: на Quest 2 она дорогая, а «стекло» очков — непрозрачное",
    "Комфорт перемещения — звук телепорта, а не виньетка: дешевле и без прозрачности",
  ]), { x: 6.7, y: 3.35, w: 6.0, h: 3.6, fontSize: 15, paraSpaceAfter: 8 });
  s.addNotes("Каждое решение по цвету и материалу выведено из мудборда и ограничений Quest 2. Палитра одна на всю команду: атласы оружия и окружения из неё же.");
}

// ---------------------------------------------------------------- 6. Подсказки и экран результата (Юля)
{
  const s = pres.addSlide();
  header(s, "Подсказки и экран результата в шлеме", TEAM.yulia);
  image(s, "img/ng-32-onboarding.png", 0.6, 1.75, { w: 5.9, name: "Onboarding" });
  image(s, "img/yg-15-result-r1.png", 6.83, 1.75, { h: 4.0, name: "Result screen" });
  text(s, "Выноска у контроллера: «кнопка: действие», кнопка — по руке", { x: 0.6, y: 5.2, w: 5.9, h: 0.35, fontSize: 13, color: P.muted });
  text(s, bullets([
    "Экран у шкафа, 1,95 м от глаз; ни одна надпись не мельче 32 мм",
    "Вердикт — значком и словом, не только цветом",
    "Нарушения со временем и штрафом, под ними — повтор эпизода",
  ]), { x: 0.6, y: 5.7, w: 12.1, h: 1.3, fontSize: 15, paraSpaceAfter: 6 });
  s.addNotes("Правки по протоколу: пояснение повтора было 26 мм — подняли до минимума экрана; повтор больше не открывается на нарушениях СИЗ, которые движением не видны.");
}

// ---------------------------------------------------------------- 7. Модели и бюджеты (Маша)
{
  const s = pres.addSlide();
  header(s, "Что смоделировали своими руками", TEAM.maria);
  image(s, "renders/pistol.png", 0.6, 1.75, { w: 6.0, name: "Pistol render" });
  const rows = [["Модель", "Треугольники", "Бюджет"],
    ["Пистолет", "1 760", "8 000"], ["Мишенный механизм", "836", "6 000"], ["Наушники", "364", "800"],
    ["Очки", "284", "800"], ["Магазин", "300", "800"], ["Патрон / гильза", "120 / 96", "200"], ["Вспышка выстрела", "24", "100"]];
  s.addTable(rows.map((r, i) => r.map((c, j) => ({
    text: c, options: { bold: i === 0, color: i === 0 ? P.muted : P.text, fill: { color: i === 0 ? P.bg : P.panel }, align: j === 0 ? "left" : "right", fontSize: 14, fontFace: FONT },
  }))), { x: 7.0, y: 1.75, w: 5.7, colW: [2.7, 1.6, 1.4], rowH: 0.42, border: { type: "solid", pt: 1, color: P.bg }, margin: 0.08, valign: "middle" });
  text(s, "Blender 5.1 → FBX: метры, перёд −Y, Apply Transform; эталонный куб проверяет масштаб и оси. Все модели — в одном атласе 1024.", { x: 7.0, y: 5.4, w: 5.7, h: 1.2, fontSize: 14, color: P.light });
  s.addNotes("Все модели — скриптами Blender в репозитории, поэтому любую можно пересобрать. Бюджеты держим с большим запасом: на Quest важнее draw calls.");
}

// ---------------------------------------------------------------- 8. Иерархия и подвижные части (Маша)
{
  const s = pres.addSlide();
  header(s, "Иерархия и подвижные части", TEAM.maria);
  image(s, "renders/target_mechanism.png", 6.73, 1.75, { w: 6.0, name: "Target render" });
  card(s, 0.6, 1.75, 5.8, 2.45, "Пистолет", "Подвижные: затвор Slide (ход назад 30 мм), спуск, предохранитель, защёлка магазина, задержка затвора. Пустышки для кода: Muzzle, EjectionPort, MagWell, Grip_Primary, Grip_Secondary.", P.ghost, "Pistol");
  card(s, 0.6, 4.4, 5.8, 2.45, "Мишенный механизм", "Каретка с роликами едет по направляющей, рамка поворачивается ребром к стрелку. В .blend — настоящая иерархия, перед экспортом детали переподвешиваются к корню.", P.ghost, "Target");
  text(s, "Начало координат детали — там, откуда она движется; ось движения — локальная ось", { x: 6.73, y: 5.65, w: 6.0, h: 0.8, fontSize: 14, color: P.muted });
  s.addNotes("Имена деталей и пустышек — контракт с кодом: Влад ищет Slide, Muzzle, MagWell по имени.");
}

// ---------------------------------------------------------------- 9. Чистота сетки (Маша)
{
  const s = pres.addSlide();
  header(s, "Проверка сеток нашла дыру в пистолете", TEAM.maria);
  image(s, "img/mm-16-breech.png", 0.6, 1.75, { w: 12.1, name: "Breech before after" });
  text(s, "Окно выброса: слева казённик вывернут нормалями внутрь — дыра; справа после правки", { x: 0.6, y: 6.5, w: 12.1, h: 0.35, fontSize: 13, color: P.muted });
  s.addNotes("Проверка всех сеток: н-гоны, вывернутые грани, вырожденные треугольники — четыре бага, исправлены; автотест MeshHygieneTests ловит это в Unity. Перепроверка силуэтов по референсам нашла ещё зеркальный изгиб спуска и линзу очков без выреза под нос.");
}

// ---------------------------------------------------------------- 10. Детекторы (Влад)
{
  const s = pres.addSlide();
  header(s, "Три детектора работают раз в кадр", TEAM.vlad);
  card(s, 0.6, 1.75, 3.9, 2.65, "Сектор", "Луч из дульного среза до первой поверхности. Пулеулавливатель или мишень — в секторе, щит или стена — вне. Дольше 1,5 с с магазином — стоп.", P.yellow, "Sector");
  card(s, 0.6, 4.55, 3.9, 2.4, "Палец на спуске", "Ёмкостный датчик курка контроллера: касание, а не нажатие. Вне команды «Огонь» дольше 0,15 с — замечание.", P.yellow, "Trigger");
  image(s, "img/vp-18-demo.png", 4.8, 1.75, { h: 5.2, name: "Detector demo" });
  text(s, "Демо: Menu левой руки — лучи и состояния видны", { x: 4.8, y: 6.98, w: 7.4, h: 0.3, fontSize: 12, color: P.muted });
  s.addNotes("Демо на защите: развернуть пистолет вбок — луч красный; положить палец на спуск — «Палец: на спуске»; поднести ствол к лицу — конус красный, стоп «Взгляд в ствол».");
}

// ---------------------------------------------------------------- 11. Взгляд в ствол и почему нет людей (Влад)
{
  const s = pres.addSlide();
  header(s, "Почему в сцене нет людей", TEAM.vlad);
  card(s, 0.6, 1.75, 5.9, 2.4, "Взгляд в ствол", "Луч из камеры шлема до 0,7 м в дульный срез и камера в конусе 30° вокруг оси ствола. Дольше 0,2 с — стоп. Сбоку смотреть можно.", P.red, "Look");
  card(s, 0.6, 4.35, 5.9, 2.6, "Сектор вместо «не целься в людей»", "Правило тира — ствол только к мишеням, есть там кто-то или нет. Человек в сцене научил бы обратному: «можно, если никого нет». И этика: ни людей, ни манекенов, мишень только спортивная.", P.red, "Ethics");
  image(s, "img/vp-17-replay-frame.png", 6.83, 1.75, { w: 5.9, name: "Replay frame" });
  text(s, "Повтор нарушения на экране результата: призрак стрелка, красный луч — куда смотрел ствол", { x: 6.83, y: 5.15, w: 5.9, h: 0.7, fontSize: 13, color: P.muted });
  s.addNotes("Нарушение считается по сектору, а не по наведению на человека. Повтор эпизода показывает момент нарушения со стороны.");
}

// ---------------------------------------------------------------- 12. Сборка и качество (Никита)
{
  const s = pres.addSlide();
  header(s, "Сборка, тесты и протокол", TEAM.nikita);
  const stats = [["236", "EditMode-тестов", P.cyan], ["137", "PlayMode, со сквозным прогоном", P.cyan], ["21 тыс.", "треугольников в кадре из 150 тыс.", P.green], ["46–57", "batches из 150", P.green]];
  stats.forEach(([n, l, c], i) => {
    const x = 0.6 + i * 3.08;
    s.addShape(pres.shapes.ROUNDED_RECTANGLE, { x, y: 1.75, w: 2.8, h: 1.9, rectRadius: 0.08, fill: { color: P.panel }, line: { type: "none" }, objectName: "QA stat " + i });
    text(s, n, { x: x + 0.3, y: 1.92, w: 2.4, h: 0.9, fontSize: 36, bold: true, color: c });
    text(s, l, { x: x + 0.3, y: 2.85, w: 2.3, h: 0.7, fontSize: 14, color: P.light });
  });
  text(s, bullets([
    "Сквозной тест проходит упражнение руками XRI от шкафа до шкафа: «Зачёт 100», 4 из 4",
    "Протокол проверки: строка на каждое нарушение и сценарий; прогон в редакторе нашёл 8 багов, все закрыты",
    "Задача — ветка, PR в dev, конец спринта — релиз с архивом проекта",
    "Unity 6000.3.24f1, OpenXR с Meta Quest Support: APK 49 МБ, видео прогона и Unity Package — в релизе",
  ]), { x: 0.6, y: 4.0, w: 12.1, h: 3.0, fontSize: 16, paraSpaceAfter: 8 });
  s.addNotes("Автотесты гоняют то же, что протокол в шлеме. Сквозной прогон нашёл два бага логики раньше первого запуска в шлеме.");
}

// ---------------------------------------------------------------- 13. Как менялись решения
{
  const s = pres.addSlide();
  header(s, "Как менялись решения по ходу");
  const ms = [["M2 · 09.10", "Блокинг и якоря", "Телепорт только по трём якорям: плавный ход убрали ради комфорта"],
    ["M3 · 23.10", "Оружие в руках", "Затвор тянется рукой, патроны — по одному в магазин"],
    ["M4 · 06.11", "Балл и экран", "Меткость вторична: грубое нарушение — «не зачёт» при любом балле"],
    ["M5 · 20.11", "Подсказки и стоп", "Подсказка только после 5 с бездействия, а не заранее"],
    ["M6 · 04.12", "Протокол и правки", "Вне рубежа ствол «вниз» безопасен — иначе чистый проход был невозможен"],
    ["M7 · 18.12", "Защита", "Повтор нарушения и демо детекторов"]];
  s.addShape(pres.shapes.LINE, { x: 0.9, y: 2.2, w: 11.5, h: 0, line: { color: P.steel, width: 2 }, objectName: "Timeline" });
  ms.forEach(([d, t, b], i) => {
    const x = 0.6 + i * 2.05;
    s.addShape(pres.shapes.OVAL, { x: x + 0.15, y: 2.05, w: 0.3, h: 0.3, fill: { color: P.yellow }, line: { type: "none" }, objectName: "Milestone " + i });
    text(s, d, { x, y: 1.6, w: 1.95, h: 0.35, fontSize: 13, bold: true, color: P.yellow });
    text(s, t, { x, y: 2.6, w: 1.9, h: 0.45, fontSize: 16, bold: true });
    text(s, b, { x, y: 3.15, w: 1.9, h: 3.0, fontSize: 13, color: P.light });
  });
  s.addNotes("Шесть вех, шесть решений, которые поменяли продукт. Самое важное — меткость вторична и подсказка не опережает пользователя.");
}

// ---------------------------------------------------------------- 14. Итог
{
  const s = pres.addSlide();
  s.background = { color: P.bg };
  image(s, "img/yg-15-stop-r3.png", 7.0, 1.2, { w: 5.7, name: "Stop screen" });
  text(s, "Итог", { x: 0.6, y: 0.45, w: 6, h: 0.8, fontSize: 32, bold: true, objectName: "Title" });
  text(s, bullets([
    "Упражнение проходится целиком: 9 шагов, 13 нарушений, стоп-экран, результат с повтором и «Повторить»",
    "Модели, окружение, звук и свет — свои, в бюджетах Quest 2",
    "Дальше: прогон протокола в шлеме всей командой, замер 72 Гц в APK, плейтест с двумя командами",
  ]), { x: 0.6, y: 1.5, w: 6.0, h: 4.0, fontSize: 18, paraSpaceAfter: 12 });
  text(s, "Спасибо! Вопросы", { x: 0.6, y: 6.0, w: 6, h: 0.7, fontSize: 28, bold: true, color: P.yellow });
  s.addNotes("Главное, что не сделано без шлема: проверка в шлеме и замер на Quest 2. Всё остальное — в релизах на GitHub.");
}

pres.writeFile({ fileName: OUT }).then((f) => console.log("written", f));
