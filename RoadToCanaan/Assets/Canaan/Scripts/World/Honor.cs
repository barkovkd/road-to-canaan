using UnityEngine;

namespace Canaan
{
    /// <summary>The honour scale, -100..+100. Deeds move it; it sets prices, how people speak, and the ending.</summary>
    public class Honor
    {
        public float value;
        public int greetingsToday, greetDay = -1;
        float shownAt = -99f;

        public string Label => value >= 60f ? "человек слова" : value >= 20f ? "порядочный" : value > -20f ? "как все"
                             : value > -60f ? "тёмная лошадка" : "волк";

        public string About => "Сейчас " + value.ToString("0") + " по шкале от −100 до +100.\n\nЧесть растёт, когда вы помогаете людям и лагерю, " +
                               "и падает от грабежа, побоев и убийств. При высокой чести лавочники сбавляют цены, при низкой — набавляют. " +
                               "От неё зависит, чем кончится дорога.";

        /// <summary>Shop price multiplier.</summary>
        public float PriceMul => value >= 40f ? 0.85f : value <= -40f ? 1.15f : 1f;

        public void Add(float amount, bool announce = true)
        {
            if (Mathf.Approximately(amount, 0f)) return;
            value = Mathf.Clamp(value + amount, -100f, 100f);
            if (announce && Mathf.Abs(amount) >= 1f && Game.UTime - shownAt > 2f && Game.I != null)
            {
                shownAt = Game.UTime;
                Game.I.Notify(amount > 0f ? "Честь выросла" : "Честь упала", 3f);
            }
        }

        /// <summary>A friendly word: a little honour, up to five times a day.</summary>
        public void Greeted(int day)
        {
            if (day != greetDay) { greetDay = day; greetingsToday = 0; }
            if (greetingsToday++ < 5) Add(0.5f, false);
        }
    }
}
