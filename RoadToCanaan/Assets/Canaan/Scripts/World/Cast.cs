using UnityEngine;

namespace Canaan
{
    /// <summary>Looks of the named people of the story and of the town.</summary>
    public static class Cast
    {
        public static HumanSpec Marta() { return new HumanSpec { female = true, height = 1.70f, build = 0.98f, skin = Pal.Skin[0], hair = Pal.Hair[4], hairKind = HairKind.Bun, shirt = Pal.H(0x6E7C8C), skirt = true, skirtColor = Pal.H(0x3A4250), boots = Pal.LeatherDark }; }
        public static HumanSpec Tess() { return new HumanSpec { female = true, height = 1.56f, build = 0.9f, skin = Pal.Skin[0], hair = Pal.Hair[4], hairKind = HairKind.Long, shirt = Pal.ClothCream, pants = Pal.Wool, suspenders = true, hat = HatKind.Straw, hatColor = Pal.Hay }; }
        public static HumanSpec Isaiah() { return new HumanSpec { height = 1.86f, build = 1.1f, skin = Pal.Skin[5], hair = Pal.Hair[0], hairKind = HairKind.Short, beard = BeardKind.Moustache, shirt = Pal.H(0x4F6079), pants = Pal.H(0x8A8372), suspenders = true, hat = HatKind.Kepi, hatColor = Pal.ClothNavy, gunbelt = true }; }
        public static HumanSpec Yefim() { return new HumanSpec { height = 1.80f, build = 1.2f, belly = 0.4f, skin = Pal.Skin[1], hair = Pal.Hair[5], hairKind = HairKind.Balding, beard = BeardKind.Full, shirt = Pal.ClothCream, pants = Pal.ClothGrey, apron = true, rolledSleeves = true }; }
        public static HumanSpec Finch() { return new HumanSpec { female = true, height = 1.66f, build = 0.92f, skin = Pal.Skin[1], hair = Pal.Hair[0], hairKind = HairKind.Bun, shirt = Pal.ClothWhite, skirt = true, skirtColor = Pal.H(0x4A3F3C), apron = true }; }
        public static HumanSpec Holt() { return new HumanSpec { height = 1.78f, build = 0.92f, skin = Pal.Skin[0], hair = Pal.Hair[6], hairKind = HairKind.Short, beard = BeardKind.None, shirt = Pal.ClothWhite, pants = Pal.ClothBlack, coat = true, coatColor = Pal.ClothBlack, hat = HatKind.FlatBrim, hatColor = Pal.ClothBlack }; }
        public static HumanSpec Buck() { return new HumanSpec { height = 1.72f, build = 1.15f, belly = 0.8f, skin = Pal.Skin[2], hair = Pal.Hair[5], hairKind = HairKind.Balding, beard = BeardKind.Full, shirt = Pal.ClothRed, pants = Pal.ClothBrown, apron = true, hat = HatKind.Derby, hatColor = Pal.ClothBrown }; }
        public static HumanSpec Abe() { return new HumanSpec { height = 1.75f, build = 0.9f, skin = Pal.Skin[2], hair = Pal.Hair[6], hairKind = HairKind.Long, beard = BeardKind.Full, shirt = Pal.H(0x9C7B52), pants = Pal.H(0x6B5A42), coat = true, coatColor = Pal.LeatherTan, hat = HatKind.Cowboy, hatColor = Pal.LeatherTan, gunbelt = true }; }
        public static HumanSpec Jude() { return new HumanSpec { height = 1.76f, build = 0.9f, skin = Pal.Skin[0], hair = Pal.Hair[3], hairKind = HairKind.Short, shirt = Pal.ClothBlue, pants = Pal.Denim, vest = true, vestColor = Pal.ClothBrown, hat = HatKind.Cowboy, hatColor = Pal.ClothGrey, gunbelt = true, bandana = true, bandanaColor = Pal.ClothYellow }; }
        public static HumanSpec Josefa() { return new HumanSpec { female = true, height = 1.68f, build = 0.95f, skin = Pal.Skin[3], hair = Pal.Hair[0], hairKind = HairKind.Long, shirt = Pal.H(0x9A3B2C), pants = Pal.H(0x3F3A36), vest = true, vestColor = Pal.LeatherDark, hat = HatKind.FlatBrim, hatColor = Pal.ClothBlack, gunbelt = true }; }

        public static HumanSpec Sheriff() { return new HumanSpec { height = 1.84f, build = 1.08f, belly = 0.3f, skin = Pal.Skin[1], hair = Pal.Hair[5], hairKind = HairKind.Short, beard = BeardKind.Moustache, shirt = Pal.ClothWhite, pants = Pal.ClothGrey, vest = true, vestColor = Pal.ClothBlack, badge = true, gunbelt = true, hat = HatKind.Cowboy, hatColor = Pal.ClothCream }; }
        public static HumanSpec Deputy(int i) { return new HumanSpec { height = 1.78f + i * 0.04f, build = 1f, skin = Pal.Skin[i % 3], hair = Pal.Hair[1 + i], hairKind = HairKind.Short, beard = i == 0 ? BeardKind.None : BeardKind.Goatee, shirt = Pal.ClothGrey, pants = Pal.ClothBrown, vest = true, vestColor = Pal.H(0x3A3F4C), badge = true, gunbelt = true, hat = HatKind.Cowboy, hatColor = Pal.LeatherDark }; }
        public static HumanSpec Maud() { return new HumanSpec { female = true, height = 1.69f, build = 1.05f, skin = Pal.Skin[1], hair = Pal.Hair[7], hairKind = HairKind.Bun, shirt = Pal.H(0x7A2F3A), skirt = true, skirtColor = Pal.H(0x2E2A30), apron = true }; }
        public static HumanSpec Peabody() { return new HumanSpec { height = 1.70f, build = 1.05f, belly = 0.6f, skin = Pal.Skin[0], hair = Pal.Hair[5], hairKind = HairKind.Balding, beard = BeardKind.Moustache, shirt = Pal.ClothWhite, pants = Pal.ClothGrey, apron = true, vest = true, vestColor = Pal.ClothGreen }; }
        public static HumanSpec Schramm() { return new HumanSpec { height = 1.82f, build = 0.95f, skin = Pal.Skin[0], hair = Pal.Hair[4], hairKind = HairKind.Short, beard = BeardKind.Full, shirt = Pal.ClothGrey, pants = Pal.ClothBlack, apron = true, rolledSleeves = true }; }
        public static HumanSpec Harper() { return new HumanSpec { height = 1.74f, build = 1f, skin = Pal.Skin[4], hair = Pal.Hair[0], hairKind = HairKind.Short, beard = BeardKind.Goatee, shirt = Pal.ClothYellow, pants = Pal.Denim, suspenders = true, hat = HatKind.Straw, hatColor = Pal.Hay }; }
        public static HumanSpec Whitfield() { return new HumanSpec { height = 1.77f, build = 0.9f, skin = Pal.Skin[0], hair = Pal.Hair[6], hairKind = HairKind.Short, beard = BeardKind.Goatee, shirt = Pal.ClothWhite, pants = Pal.ClothBlack, vest = true, vestColor = Pal.ClothGrey }; }
        public static HumanSpec Troy() { return new HumanSpec { height = 1.73f, build = 0.92f, skin = Pal.Skin[1], hair = Pal.Hair[1], hairKind = HairKind.Short, shirt = Pal.ClothWhite, pants = Pal.ClothNavy, vest = true, vestColor = Pal.ClothNavy, hat = HatKind.Kepi, hatColor = Pal.ClothNavy }; }
        public static HumanSpec Pruitt() { return new HumanSpec { height = 1.71f, build = 0.88f, skin = Pal.Skin[0], hair = Pal.Hair[1], hairKind = HairKind.Short, beard = BeardKind.Moustache, shirt = Pal.ClothWhite, pants = Pal.ClothGrey, coat = true, coatColor = Pal.H(0x4B4A52), hat = HatKind.Derby, hatColor = Pal.ClothBlack }; }
        public static HumanSpec Butcher() { return new HumanSpec { height = 1.79f, build = 1.18f, belly = 0.7f, skin = Pal.Skin[2], hair = Pal.Hair[0], hairKind = HairKind.Short, beard = BeardKind.Moustache, shirt = Pal.ClothCream, pants = Pal.ClothBrown, apron = true, rolledSleeves = true }; }
        public static HumanSpec Smith() { return new HumanSpec { height = 1.85f, build = 1.2f, skin = Pal.Skin[3], hair = Pal.Hair[0], hairKind = HairKind.Bald, beard = BeardKind.Full, shirt = Pal.ClothGrey, pants = Pal.ClothBlack, apron = true, rolledSleeves = true }; }
    }

    /// <summary>What people say. Lines are picked at random within a situation.</summary>
    public static class Barks
    {
        static string P(params string[] a) { return a[Random.Range(0, a.Length)]; }

        public static string PlayerGreet() { return P("Добрый день.", "Здравствуйте.", "Хороший денёк.", "Мэм. Сэр.", "Как поживаете?"); }
        public static string PlayerInsult() { return P("Чего уставился?", "Проваливай с дороги.", "У тебя лицо как у больного мула.", "Отойди, от тебя несёт."); }
        public static string PlayerRob() { return P("Деньги. Живо.", "Выворачивай карманы.", "Без глупостей. Кошелёк сюда."); }

        public static string Greet(bool female, float honor)
        {
            if (honor <= -40f) return P("Я вас не знаю и знать не хочу.", "Идите своей дорогой.", "…");
            if (honor >= 40f) return P("Рад вас видеть, капитан!", "Доброго здоровья, мистер Уитлок.", "Храни вас Бог.", "О вас в городе хорошо говорят.");
            return P("Добрый день.", "И вам не хворать.", "День добрый, мистер.", "Здрасьте.", female ? "Добрый день, сэр." : "Как оно?", "Погода нынче что надо.");
        }

        public static string Insulted(int times) { return times <= 1 ? P("Что вы себе позволяете?", "Полегче, приятель.", "Вы пьяны?", "Не ищите беды.") : P("Ну всё, сам напросился!", "Я шерифу скажу!", "Хватит с меня!"); }
        public static string Scared() { return P("Не стреляйте!", "Господи помилуй!", "У меня дети!", "Берите что хотите!"); }
        public static string Flee() { return P("Помогите!", "Убивают!", "Шериф! Шериф!", "Бегите!"); }
        public static string Witness() { return P("Я всё видел!", "Шериф узнает об этом!", "Держите его!", "Я вас запомнил!"); }
        public static string Robbed() { return P("Вот, вот всё, что есть!", "Забирайте, только не стреляйте.", "Чтоб вам пусто было…"); }
        public static string LawOrder() { return P("Стоять! Руки, чтоб я видел!", "Бросай оружие!", "Именем закона!"); }
        public static string LawRecognise() { return P("Это он! Держи его!", "Ты-то мне и нужен.", "Узнаю эту рожу с плаката."); }
        public static string LawIdle() { return P("В городе тихо. Так и оставим.", "Оружие в кобуре держите.", "Без фокусов, приезжий."); }
        public static string KeeperHello() { return P("Чем могу служить?", "Смотрите, выбирайте.", "Товар свежий, с последнего поезда.", "Чего желаете?"); }
        public static string KeeperBye() { return P("Заходите ещё.", "Счастливого пути.", "Берегите себя."); }
        public static string Camp(string name)
        {
            switch (name)
            {
                case "Марта Линдквист": return P("Капитан. Мы трогаемся, когда вы скажете.", "Касса не бездонная, капитан.", "Люди на вас смотрят. Помните об этом.");
                case "Тесс": return P("Научите меня стрелять? Мама не узнает.", "Я вчера сама оседлала лошадь!", "А правда, что вы были на войне?");
                case "Исайя Фримен": return P("Вилороги ушли к северным холмам.", "След свежий — значит, зверь близко. Запомните.", "Капитан.");
                case "Ефим Кузнецов": return P("Железо любит масло.", "Принесёте ружьё — посмотрю.", "Хм.");
                case "Док Финч": return P("Спите больше, капитан. Это рецепт.", "Тысячелистник несите — пригодится.", "Все пока на ногах. Пока.");
                case "Пастор Холт": return P("Не оружием спасётся человек.", "Я молюсь и за вас, капитан.", "Господь ведёт нас, а не вы.");
                case "Бак Тиллер": return P("Похлёбка будет, если будет мясо.", "Голодный обоз далеко не уйдёт.", "В шестьдесят втором мы ели сапоги. Вкуснее, чем бобы.");
                case "Джуд Харкер": return P("Всё тихо, капитан.", "Долго нам ещё тут стоять?", "Я на посту.");
                case "Эйб Коулман": return P("Брод нынче низкий. Пройдём.", "Я эту тропу топтал, когда тебя на свете не было.", "Дальше каньоны. Там вода дороже золота.");
                default: return P("Капитан.", "Когда выступаем?", "Доброго дня, капитан.", "Дети спрашивают, далеко ли ещё.", "Говорят, там земля — палку воткни, зацветёт.");
            }
        }
    }
}
