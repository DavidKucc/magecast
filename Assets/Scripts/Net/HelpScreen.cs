using MageCast.Gestures;
using UnityEngine;

namespace MageCast
{
    /// <summary>
    /// How to play, in the game itself: the controls, the rules and every spell with its rune and tiers.
    /// Opened from the main menu and from the Esc menu; drawn from here so the two cannot differ.
    ///
    /// In Czech, for the people testing it. The numbers are the game's own (SpellTiers), so the page
    /// cannot promise something the game no longer does.
    /// </summary>
    public static class HelpScreen
    {
        public static bool Open;

        static int tab;
        static Vector2 scroll;
        static GUIStyle heading, body, tabStyle, name, small, close;
        static readonly string[] Tabs = { "Ovládání", "Pravidla", "Kouzla" };

        struct SpellInfo
        {
            public string Rune, Name, Czech, Basics, I, II, III;
            public Color Colour;
        }

        static readonly SpellInfo[] Spells =
        {
            new SpellInfo { Rune = GestureTemplates.Kenaz, Name = "FIRE", Czech = "Kenaz (pochodeň) - oheň",
                Colour = new Color(1f, 0.45f, 0.15f),
                Basics = "22 poškození, 26 m/s. Běžný útok.",
                I = "zásah",
                II = "+ hoření na zasaženém (" + SpellTiers.BurnPerSecond + "/s na " + SpellTiers.BurnSeconds + " s), hořící plocha na zemi, jednou se odrazí od zdi",
                III = "+ výbuch: všichni do " + SpellTiers.ExplosionRadius + " m kromě zasaženého dostanou polovinu zásahu - i ty, když stojíš blízko" },
            new SpellInfo { Rune = GestureTemplates.Laguz, Name = "ICE", Czech = "Laguz (voda) - led",
                Colour = new Color(0.55f, 0.85f, 1f),
                Basics = "30 poškození, 17 m/s. Pomalý a tlustý, dá se uhnout.",
                I = "zásah",
                II = "+ zpomalení zasaženého, kluzká ledová plocha na zemi",
                III = "+ zmrazí na " + SpellTiers.FreezeSeconds + " s: nehne se ani neskočí, kreslit ale může" },
            new SpellInfo { Rune = GestureTemplates.Sowulo, Name = "LIGHTNING", Czech = "Sowulo (slunce) - blesk",
                Colour = new Color(1f, 0.9f, 0.35f),
                Basics = "12 poškození, 44 m/s - skoro se nedá uhnout. Do ledu nebo vody vybije všechny, kdo na ní stojí.",
                I = "zásah",
                II = "+ přeskočí na nejbližší jiný cíl do " + SpellTiers.ChainRange + " m za polovinu; do země nechá elektrickou plochu " +
                     "(" + SpellTiers.ShockPerSecond + "/s, kdo v ní stojí, nemůže kreslit)",
                III = "+ elektrická plocha se objeví přímo pod zasaženým" },
            new SpellInfo { Rune = GestureTemplates.Ehwaz, Name = "AIR", Czech = "Ehwaz (pohyb) - vzduch",
                Colour = new Color(0.8f, 0.95f, 0.9f),
                Basics = "Žádné poškození, nikdy. Odhodí a vždycky přeruší kreslení.",
                I = "odhoz",
                II = "+ updraft plocha (vyhodí toho, kdo na ni vstoupí, a odkloní střely nahoru); do ohně: sfoukne ho jako ohnivou vlnu, " +
                     "která se 8 m převalí ve směru větru, zapálí a odhodí každého v cestě; na ledu rozklouže lidi",
                III = "+ dopadne-li do jakékoli plochy: vzduchová bomba - všechny v ní a do " + SpellTiers.BombReachBeyondPatch + " m od ní hodí do jejího středu (i tebe)" },
            new SpellInfo { Rune = GestureTemplates.Uruz, Name = "BARRIER", Czech = "Uruz (síla) - bariéra",
                Colour = new Color(1f, 0.85f, 0.4f),
                Basics = "Zastaví kouzla z obou stran, dokud ji zásahy neprorazí. Jedna na hráče.",
                I = "stěna",
                II = "větší stěna, vydrží víc",
                III = "kopule kolem tebe na " + SpellTiers.DomeSeconds + " s, chodí s tebou, tvoje kouzla pouští ven, cizí zastaví" },
        };

        const string Controls =
            "<b>Pohyb</b>\n" +
            "WASD - chůze,  Shift - sprint,  mezerník - skok\n" +
            "Myš - míření\n" +
            "Šplhání: skoč proti hraně a drž směr k ní - vytáhneš se na cokoli do 2,4 m. Během šplhání nekreslíš; " +
            "zásah tě shodí dolů.\n\n" +
            "<b>Kouzlo - všechno levým tlačítkem</b>\n" +
            "1.  Drž levé tlačítko a nakresli runu. Kamera stojí, jdeš pomaleji a nemůžeš sprintovat ani skákat.\n" +
            "2.  Pusť - kouzlo máš v ruce. Nahoře vlevo vidíš, co a v jakém tieru (I / II / III).\n" +
            "3.  Klikni levým - kouzlo letí tam, kam míříš.\n" +
            "     Podrž levé a pusť - kouzlo zahodíš a ruku máš prázdnou.\n\n" +
            "Pravé tlačítko během kreslení - zruší rozkreslenou runu.\n" +
            "Kliknutí kolečkem myši - kamera přeskočí přes druhé rameno a kouzla letí z druhé ruky (výchozí je pravá).\n" +
            "S kouzlem v ruce jen jdeš (asi poloviční rychlostí) a nedá se sprintovat.\n\n" +
            "<b>Ostatní</b>\n" +
            "Esc - menu (nastavení, nápověda, odchod)\n" +
            "F6 - schová / ukáže tabulku run vpravo nahoře\n" +
            "V tréninku: F1-F5 cvičný cíl pro záznam, F7 přepne rozpoznávání run (čáry a úhly / starý $P)";

        static readonly string Rules =
            "<b>Zápas</b>\n" +
            "Kdo první zabije " + MatchDirector.KillsToWin + "x, vyhrává. Pak se skóre vynuluje a začíná nový zápas. " +
            "Po smrti jsi za 3 s zpátky na spawnu, který soupeř nevidí. Mapu vybírá hostitel v menu.\n\n" +
            "<b>Runy</b>\n" +
            "Runa se kreslí jedním tahem. Nezáleží, na kterém konci začneš. Hra tah rozloží na rovné čáry a porovná jejich " +
            "úhly s runou - každá čára smí být nejvýš 35° vedle. Hotové čáry se hned narovnají.\n" +
            "Špatně nakreslená runa nevyjde (FIZZLE). Krátké cuknutí kreslení zruší.\n\n" +
            "<b>Tiery</b>\n" +
            "Podle toho, jak přesně runu nakreslíš, dostane kouzlo tier I, II nebo III. Tier mění, co kouzlo dělá " +
            "(viz Kouzla); poškození samo se hýbe jen 0,8-1,2x.\n" +
            "Tier I je holé kouzlo: žádná plocha, žádný odraz, žádná kombinace.\n" +
            "Tier III v ruce vydrží " + "4" + " s, pak spadne na II. Tier II vydrží napořád.\n" +
            "Když tě cokoli trefí přímým zásahem, tier kouzla v ruce klesne o jeden - a kouzlo s tierem I zmizí.\n\n" +
            "<b>Soupeř tě čte</b>\n" +
            "Tvoje runa se kreslí nad tvou hlavou a jakmile kouzlo vznikne, objeví se nad tebou i jeho název s tierem " +
            "(FIRE III). Kreslení tě vystavuje - a soupeř to vidí.\n\n" +
            "<b>Plochy na zemi</b>\n" +
            "Oheň, led, vzduch a blesk (od tieru II) nechávají na zemi plochu. Každý smí mít najednou dvě - třetí vezme tu nejstarší. " +
            "V elektrické ploše (blesk) se nedá kreslit - runa se rozpadne. " +
            "Plochy působí na všechny, i na toho, kdo je udělal.\n\n" +
            "<b>Kombinace</b>\n" +
            "oheň do ledu / led do ohně - voda (vede blesk)\n" +
            "oheň do vody - uhasne,  led do vody - zamrzne\n" +
            "blesk do ledu nebo vody - zasáhne všechny, kdo na ní stojí\n" +
            "vzduch II do ohně - ohnivá vlna,  vzduch II na led - rozklouže lidi\n" +
            "vzduch III do plochy - vzduchová bomba\n" +
            "Updraft odkloní střely, které jím letí, nahoru.";

        static void Styles()
        {
            if (heading != null) return;
            heading = new GUIStyle(GUI.skin.label) { fontSize = 26, fontStyle = FontStyle.Bold };
            heading.normal.textColor = Color.white;
            body = new GUIStyle(GUI.skin.label) { fontSize = 15, wordWrap = true, richText = true };
            body.normal.textColor = new Color(0.92f, 0.92f, 0.95f);
            tabStyle = new GUIStyle(GUI.skin.button) { fontSize = 16, fontStyle = FontStyle.Bold };
            name = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold };
            small = new GUIStyle(GUI.skin.label) { fontSize = 14, wordWrap = true, richText = true };
            small.normal.textColor = new Color(0.9f, 0.9f, 0.93f);
            close = new GUIStyle(GUI.skin.button) { fontSize = 16, fontStyle = FontStyle.Bold };
        }

        /// <summary>Draws the help over everything, if it is open. Returns whether it is.</summary>
        public static bool Draw()
        {
            if (!Open) return false;
            Styles();

            GUI.color = new Color(0f, 0f, 0f, 0.75f);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;

            float w = Mathf.Min(900f, Screen.width - 40f), h = Mathf.Min(680f, Screen.height - 40f);
            Rect panel = new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.5f, w, h);
            GUI.color = new Color(0.08f, 0.09f, 0.12f, 0.96f);
            GUI.DrawTexture(panel, Texture2D.whiteTexture);
            GUI.color = Color.white;

            GUI.Label(new Rect(panel.x + 20f, panel.y + 14f, 400f, 34f), "Jak hrát", heading);
            if (GUI.Button(new Rect(panel.xMax - 110f, panel.y + 16f, 90f, 32f), "Zavřít", close)) Open = false;

            float tx = panel.x + 20f;
            for (int i = 0; i < Tabs.Length; i++)
            {
                GUI.color = i == tab ? Color.white : new Color(1f, 1f, 1f, 0.6f);
                if (GUI.Button(new Rect(tx, panel.y + 60f, 150f, 34f), Tabs[i], tabStyle)) { tab = i; scroll = Vector2.zero; }
                GUI.color = Color.white;
                tx += 158f;
            }

            Rect view = new Rect(panel.x + 20f, panel.y + 106f, w - 40f, h - 122f);
            float contentWidth = view.width - 24f;

            if (tab == 2) DrawSpells(view, contentWidth);
            else
            {
                string text = tab == 0 ? Controls : Rules;
                float textHeight = body.CalcHeight(new GUIContent(text), contentWidth) + 20f;
                scroll = GUI.BeginScrollView(view, scroll, new Rect(0f, 0f, contentWidth, textHeight));
                GUI.Label(new Rect(0f, 0f, contentWidth, textHeight), text, body);
                GUI.EndScrollView();
            }

            // Esc closes the help before it closes anything else
            Event e = Event.current;
            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape) { Open = false; e.Use(); }
            return true;
        }

        static void DrawSpells(Rect view, float width)
        {
            const float icon = 64f;
            float textX = icon + 16f, textW = width - textX;

            // measured first, so the scroll area is as tall as what is in it
            float total = 0f;
            foreach (SpellInfo s in Spells) total += SpellHeight(s, textW) + 18f;

            scroll = GUI.BeginScrollView(view, scroll, new Rect(0f, 0f, width, total));
            float y = 0f;
            foreach (SpellInfo s in Spells)
            {
                float hgt = SpellHeight(s, textW);
                Texture2D rune = RuneIcons.Get(s.Rune, s.Colour, 64);
                if (rune != null) GUI.DrawTexture(new Rect(0f, y + 4f, icon, icon), rune);

                name.normal.textColor = s.Colour;
                GUI.Label(new Rect(textX, y, textW, 24f), s.Name + "   <size=13><color=#aaaab4>" + s.Czech + "</color></size>",
                          new GUIStyle(name) { richText = true });
                float ty = y + 26f;
                ty = Line(textX, ty, textW, s.Basics);
                ty = Line(textX, ty, textW, "<b>I</b>   " + s.I);
                ty = Line(textX, ty, textW, "<b>II</b>  " + s.II);
                Line(textX, ty, textW, "<b>III</b> " + s.III);
                y += hgt + 18f;
            }
            GUI.EndScrollView();
        }

        static float SpellHeight(SpellInfo s, float width)
        {
            float h = 26f;
            foreach (string line in new[] { s.Basics, "I   " + s.I, "II  " + s.II, "III " + s.III })
                h += small.CalcHeight(new GUIContent(line), width) + 2f;
            return Mathf.Max(h, 72f);
        }

        static float Line(float x, float y, float width, string text)
        {
            float h = small.CalcHeight(new GUIContent(text), width);
            GUI.Label(new Rect(x, y, width, h), text, small);
            return y + h + 2f;
        }
    }
}
