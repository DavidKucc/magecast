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
                Basics = "16 poškození, 26 m/s (tier I 34 m/s). Běžný útok. " +
                         "Zamířený na zem do " + SpellTiers.IceWallReach + " m před sebe (svítí LINE a ukáže se obrys na zemi) nevyletí, " +
                         "ale zapálí ohnivou čáru napříč na " + SpellTiers.FireLineSeconds + " s: nic nezastaví, ale kdo jí projde, hoří. " +
                         "Mokrého nezapálí, jen zasyčí a vysuší ho.",
                I = "zásah, rychlejší let; dopad na zem, zeď nebo bariéru zraní každého do " + SpellTiers.SplashRadius + " m (i tebe)",
                II = "+ hoření na zasaženém (" + SpellTiers.BurnPerSecond + "/s na " + SpellTiers.BurnSeconds + " s), hořící plocha na zemi, jednou se odrazí od zdi",
                III = "+ výbuch: všichni do " + SpellTiers.ExplosionRadius + " m kromě zasaženého dostanou polovinu zásahu - i ty, když stojíš blízko" },
            new SpellInfo { Rune = GestureTemplates.Laguz, Name = "ICE", Czech = "Laguz (voda) - led",
                Colour = new Color(0.55f, 0.85f, 1f),
                Basics = "22 poškození, 17 m/s (tier I 24 m/s). Pomalý a tlustý, dá se uhnout. Zasažený se vždy zpomalí. " +
                         "Zamířený na zem do " + SpellTiers.IceWallReach + " m před sebe (svítí WALL a ukáže se obrys na zemi) nevyletí, " +
                         "ale vyrazí jako ledová stěna: první zásah ji roztříští, po " + SpellTiers.IceWallSeconds + " s roztaje, oheň z ní nechá vodu. " +
                         "Proletí-li ohnivou čárou, je z něj voda: bez zpomalení, ale zasažený je " + SpellTiers.WetSeconds + " s mokrý (i ty) " +
                         "a na ledové ploše přimrzne.",
                I = "zásah se zpomalením, rychlejší let; dopad na zem, zeď nebo bariéru zraní každého do " + SpellTiers.SplashRadius + " m (i tebe)",
                II = "+ kluzká ledová plocha na zemi; kdo je mokrý, na ní na " + SpellTiers.WetFreezeSeconds + " s přimrzne",
                III = "+ zmrazí na " + SpellTiers.FreezeSeconds + " s: nehne se ani neskočí, kreslit ale může" },
            new SpellInfo { Rune = GestureTemplates.Sowulo, Name = "LIGHTNING", Czech = "Sowulo (slunce) - blesk",
                Colour = new Color(1f, 0.9f, 0.35f),
                Basics = "12 poškození, 44 m/s (tier I 52 m/s) - skoro se nedá uhnout. Do ledu nebo vody vybije všechny, kdo na ní stojí. " +
                         "Do mokrého " + SpellTiers.WetShockMultiplier + "x poškození (a vysuší ho).",
                I = "zásah, rychlejší let",
                II = "+ přeskočí na nejbližší jiný cíl do " + SpellTiers.ChainRange + " m za polovinu; do země nechá elektrickou plochu " +
                     "(" + SpellTiers.ShockPerSecond + "/s, kdo v ní stojí, nemůže kreslit)",
                III = "+ elektrická plocha se objeví přímo pod zasaženým" },
            new SpellInfo { Rune = GestureTemplates.Ehwaz, Name = "AIR", Czech = "Ehwaz (pohyb) - vzduch",
                Colour = new Color(0.8f, 0.95f, 0.9f),
                Basics = "Žádné poškození, nikdy. Odhodí a vždycky přeruší kreslení.",
                I = "odhoz, rychlejší let; kdo po odhozu narazí do zdi (do ~2 m od ní), dostane " + SpellTiers.SlamDamage + " a na " + SpellTiers.SlamStagger + " s se zastaví",
                II = "+ updraft plocha (vyhodí toho, kdo na ni vstoupí, tebe až na 4 m - na střechu nebo most - a odkloní střely nahoru); do ohně: sfoukne ho jako ohnivou vlnu, " +
                     "která se 8 m převalí ve směru větru, zapálí a odhodí každého v cestě; na ledu rozklouže lidi",
                III = "+ dopadne-li do jakékoli plochy: vzduchová bomba - všechny v ní a do " + SpellTiers.BombReachBeyondPatch + " m od ní hodí do jejího středu (i tebe), po " + SpellTiers.BombDelay + " s" },
            new SpellInfo { Rune = GestureTemplates.Thurisaz, Name = "EARTH", Czech = "Thurisaz (trn) - země",
                Colour = new Color(0.85f, 0.62f, 0.38f),
                Basics = "20 poškození. Hozený vysokým obloukem přes krytí: dopadne tam, kam míříš (na 10 m asi za sekundu). " +
                         "Od hodu je na zemi vidět kruh, kam spadne - vidí ho všichni a dá se mu utéct. " +
                         "Při dopadu otřes do " + SpellTiers.QuakeRadius + " m: kdo v něm stojí, dostane až " +
                         Mathf.RoundToInt(SpellTiers.QuakeShare * 100f) + " % zásahu (i ty) a vyhodí ho to kousek do vzduchu. " +
                         "Zeď nebo bariéra mezi ním a dopadem ho kryje.",
                I = "kámen s otřesem",
                II = "+ rozbitá zem na 6 s: kdo v ní stojí, jde na " + Mathf.RoundToInt(SpellTiers.RubbleSlow * 100f) + " % rychlosti; " +
                     "do ohně: magma (pálí víc a " + SpellTiers.MagmaSeconds + " s); do vody: bahno (" + Mathf.RoundToInt(SpellTiers.MudSlow * 100f) +
                     " % rychlosti); led a elektrickou plochu rozbije na rozbitou zem",
                III = "+ větší otřes (" + SpellTiers.QuakeRadiusIII + " m): srazí každého v něm na " + SpellTiers.QuakeStagger + " s na zem a přeruší kreslení" },
            new SpellInfo { Rune = GestureTemplates.Uruz, Name = "BARRIER", Czech = "Uruz (síla) - bariéra",
                Colour = new Color(1f, 0.85f, 0.4f),
                Basics = "Zastaví kouzla z obou stran, dokud ji zásahy neprorazí. Jedna na hráče. Silný, vytrvalý kryt - " +
                         "rychlý a křehký je ledová stěna.",
                I = "stěna na " + SpellTiers.WallSeconds + " s",
                II = "větší stěna, vydrží víc",
                III = "kopule kolem tebe na " + SpellTiers.DomeSeconds + " s, chodí s tebou, tvoje kouzla pouští ven, cizí zastaví" },
        };

        const string Controls =
            "<b>Pohyb</b>\n" +
            "WASD - chůze,  Shift - sprint (stojí staminu: žlutý pruh nad životy),  mezerník - skok\n" +
            "Myš - míření\n" +
            "Šplhání: skoč proti hraně a drž směr k ní - vytáhneš se na cokoli do 2,4 m. Během šplhání nekreslíš; " +
            "zásah tě shodí dolů.\n\n" +
            "<b>Kouzlo - levé tlačítko, pravé ruší</b>\n" +
            "1.  Drž levé tlačítko a nakresli runu. Kamera stojí, jdeš pomaleji a nemůžeš sprintovat ani skákat.\n" +
            "2.  Pusť - kouzlo máš v ruce. Nahoře vlevo vidíš, co a v jakém tieru (I / II / III).\n" +
            "3.  Klikni levým - kouzlo letí tam, kam míříš (hned při stisku).\n" +
            "     Klikni pravým - kouzlo zahodíš a ruku máš prázdnou.\n\n" +
            "Pravé tlačítko během kreslení - zruší rozkreslenou runu.\n" +
            "Kliknutí kolečkem myši - kamera přeskočí přes druhé rameno a kouzla letí z druhé ruky (výchozí je pravá).\n" +
            "S kouzlem v ruce jen jdeš (70 % rychlosti) a nedá se sprintovat.\n\n" +
            "<b>Ostatní</b>\n" +
            "Esc - menu (nastavení, nápověda, odchod)\n" +
            "F6 - schová / ukáže panel kouzel dole\n" +
            "V tréninku: F1-F5 a F8 cvičný cíl pro záznam, F7 přepne rozpoznávání run (čáry a úhly / starý $P)";

        static readonly string Rules =
            "<b>Lobby</b>\n" +
            "Online hra začíná v lobby: hostitel pošle kód, hráči se připojí. Hostitel vybírá mapu, režim a počet " +
            "killů a přidává boty; každý si vybere tým a dá Ready. Hostitel spustí zápas, až jsou všichni ready. " +
            "Když hostitel změní nastavení, ready se všem zruší. Po zápase se všichni vrátí do lobby.\n\n" +
            "<b>Zápas</b>\n" +
            "Každý proti každému: kdo první dosáhne nastaveného počtu killů (výchozí " + MatchDirector.KillsToWin + "), vyhrává. " +
            "Týmy (Modří proti Červeným, max 3 na tým): počítají se killy celého týmu (výchozí 10). " +
            "Kouzla spoluhráčů ti nikdy neublíží ani tě neodhodí - střely tebou proletí. Jejich led ale klouže i tobě " +
            "a jejich updraft tě vyhodí vysoko jako je samotné. Jména spoluhráčů vidíš i přes zdi.\n" +
            "Po smrti jsi za 3 s zpátky na spawnu, který soupeř nevidí (v týmech na straně tvého týmu).\n\n" +
            "<b>Runy</b>\n" +
            "Runa se kreslí jedním tahem. Nezáleží, na kterém konci začneš. Hra tah rozloží na rovné čáry a porovná jejich " +
            "úhly s runou - každá čára smí být nejvýš 35° vedle. Hotové čáry se hned narovnají.\n" +
            "Špatně nakreslená runa nevyjde (FIZZLE) a další jde začít až po 0,5 s. Krátké cuknutí kreslení zruší.\n\n" +
            "<b>Tiery</b>\n" +
            "Podle toho, jak přesně runu nakreslíš, dostane kouzlo tier I, II nebo III. Tier mění, co kouzlo dělá " +
            "(viz Kouzla); poškození samo se hýbe jen 0,8-1,2x.\n" +
            "Tier I je holé kouzlo: žádná plocha, žádný odraz, žádná kombinace - ale letí rychleji a oheň s ledem " +
            "při dopadu mimo člověka zraní okolí (splash).\n" +
            "Cooldown: stejný prvek jde vytvořit znovu až " + SpellTiers.ElementCooldown + " s po minulém odeslání. " +
            "Runa prvku, který má ještě cooldown (nebo je zamčený), nevznikne - NOT READY - a čas kreslení je pryč. " +
            "Čára i rámeček v panelu jsou pak při kreslení červené. Runa dokreslená těsně před koncem (" + SpellTiers.QueueWindow + " s) ještě vznikne.\n" +
            "Navíc globální cooldown (GCD): po odeslání útočného kouzla nejde " + SpellTiers.GlobalCooldown + " s poslat žádné jiné útočné. " +
            "Bariéra má svůj vlastní - obrana nečeká na rytmus útoku. GCD tvoření nebrání: další runu nakresli a drž během " +
            "pauzy. Klik do " + SpellTiers.QueueWindow + " s před koncem se zapamatuje a kouzlo odletí samo. Panel kouzel dole ukazuje, co ještě čeká: tmavá ručička s odpočtem, samotné GCD bez čísla.\n" +
            "Zámek školy: když tě vzduch nebo šokové pole přeruší při kreslení a z rozkreslené runy už jde poznat, co to je, " +
            "ten prvek se ti zamkne na " + SpellTiers.SchoolLock + " s (LOCKED). Přerušení moc brzy nezamkne nic.\n" +
            "Tier III v ruce vydrží " + "4" + " s, pak spadne na II. Tier II vydrží napořád.\n" +
            "Když tě cokoli trefí přímým zásahem, tier kouzla v ruce klesne o jeden - a kouzlo s tierem I zmizí.\n\n" +
            "<b>Asistent míření</b>\n" +
            "Střela, která by o kousek minula nepřítele, se k němu jemně stočí: jen když je od ní do " + Projectile.AssistRange + " m " +
            "a skoro přímo před ní, na volno (ne za zdí ani bariérou), nejvýš o " + Projectile.AssistPerMetre + "° na metr letu " +
            "a " + Projectile.AssistBudget + "° za celý let. Z 10 m to vyrovná zhruba půl metru vedle, zblízka skoro nic, ostrou zatáčku nikdy. " +
            "Jen pro hráče, boti míří sami.\n\n" +
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

        static GUIStyle cardHeading, badge;
        static float openedAt = -1f;
        static float tabSlide = -1f;

        static void Styles()
        {
            if (heading != null) return;
            heading = new GUIStyle(GUI.skin.label) { fontSize = 30, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
            body = new GUIStyle(GUI.skin.label) { fontSize = 15, wordWrap = true, richText = true };
            body.normal.textColor = new Color(0.9f, 0.9f, 0.93f);
            tabStyle = new GUIStyle(GUI.skin.label) { fontSize = 17, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            name = new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold, richText = true };
            small = new GUIStyle(GUI.skin.label) { fontSize = 14, wordWrap = true, richText = true };
            small.normal.textColor = new Color(0.88f, 0.88f, 0.92f);
            close = new GUIStyle(GUI.skin.label) { fontSize = 15, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            cardHeading = new GUIStyle(GUI.skin.label) { fontSize = 17, fontStyle = FontStyle.Bold };
            badge = new GUIStyle(GUI.skin.label) { fontSize = 12, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        }

        /// <summary>
        /// Draws the help over everything, if it is open. Returns whether it is.
        ///
        /// In the menus' look (MenuArt): a pane of dark glass with a gold edge that eases in, tabs with a
        /// gold bar sliding under the chosen one, the rules in cards with gold headings, and each spell
        /// as a card with its rune glowing in its colour and its tiers on badges. Cards light up at the
        /// edge under the mouse.
        /// </summary>
        public static bool Draw()
        {
            if (!Open) { openedAt = -1f; return false; }
            Styles();
            Event e = Event.current;
            bool repaint = e.type == EventType.Repaint;
            if (openedAt < 0f) openedAt = Time.unscaledTime;
            float appear = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((Time.unscaledTime - openedAt) / 0.22f));

            if (repaint)
            {
                // the room behind goes dark (linear blending: 0.85 reads as about two thirds)
                GUI.color = new Color(0.01f, 0.012f, 0.02f, 0.85f * appear);
                GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
                GUI.color = Color.white;
            }

            float w = Mathf.Min(940f, Screen.width - 40f), h = Mathf.Min(720f, Screen.height - 40f);
            Rect panel = new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.5f + (1f - appear) * 24f, w, h);
            if (repaint)
            {
                GUI.color = new Color(0f, 0f, 0f, 0.6f * appear);
                GUI.DrawTexture(MenuArt.Grow(panel, 70f), MenuArt.Soft);
                GUI.color = Color.white;
                MenuArt.RoundRect(panel, new Color(0.045f, 0.05f, 0.072f, 0.96f * appear), 18f);
                MenuArt.RoundRect(panel, new Color(MenuArt.Gold.r, MenuArt.Gold.g, MenuArt.Gold.b, 0.28f * appear), 18f, 1.3f);
            }

            MenuArt.GlowText(new Rect(panel.x + 26f, panel.y + 14f, 400f, 44f), "Jak hrát", heading, MenuArt.Gold, 0.8f);
            if (MenuArt.Button(new Rect(panel.xMax - 126f, panel.y + 18f, 104f, 38f), "Zavřít", close)) Open = false;

            // the tabs, with a gold bar sliding under the chosen one
            float tx = panel.x + 26f, tw = 150f, ty = panel.y + 70f;
            for (int i = 0; i < Tabs.Length; i++)
            {
                Rect r = new Rect(tx + i * (tw + 8f), ty, tw, 36f);
                bool over = r.Contains(e.mousePosition);
                float hv = MenuArt.Hover(MenuArt.Key("tab" + Tabs[i], r), over);
                if (repaint)
                {
                    if (hv > 0.01f) MenuArt.RoundRect(r, new Color(1f, 1f, 1f, 0.05f * hv), 8f);
                    tabStyle.normal.textColor = i == tab ? MenuArt.Gold : Color.Lerp(new Color(1f, 1f, 1f, 0.55f), Color.white, hv);
                    GUI.Label(r, Tabs[i], tabStyle);
                }
                if (GUI.Button(r, GUIContent.none, GUIStyle.none) && tab != i)
                {
                    tab = i;
                    scroll = Vector2.zero;
                    MenuArt.Ripple(GUI.matrix.MultiplyPoint3x4(e.mousePosition), MenuArt.Gold);
                }
            }
            if (repaint)
            {
                if (tabSlide < 0f) tabSlide = tab;
                tabSlide = Mathf.Lerp(tabSlide, tab, Mathf.Clamp01(Time.unscaledDeltaTime * 14f));
                float bx = tx + tabSlide * (tw + 8f);
                MenuArt.RoundRect(new Rect(panel.x + 20f, ty + 40f, w - 40f, 1f), new Color(1f, 1f, 1f, 0.08f), 0f);
                MenuArt.Glow(new Vector2(bx + tw * 0.5f, ty + 40f), tw * 0.9f, new Color(MenuArt.Gold.r, MenuArt.Gold.g, MenuArt.Gold.b, 0.18f));
                MenuArt.RoundRect(new Rect(bx + 18f, ty + 38f, tw - 36f, 3f), MenuArt.Gold, 1.5f);
            }

            Rect view = new Rect(panel.x + 22f, panel.y + 124f, w - 44f, h - 140f);
            float contentWidth = view.width - 18f;

            ThinScrollbars(true);
            if (tab == 2) DrawSpells(view, contentWidth);
            else DrawSections(view, contentWidth, tab == 0 ? Controls : Rules);
            ThinScrollbars(false);

            MenuArt.EndFrame();

            // Esc closes the help before it closes anything else
            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape) { Open = false; e.Use(); }
            return true;
        }

        // ---------------------------------------------------------------- a thin gold scrollbar

        static Texture2D track, thumb;
        static GUIStyle keepBar, keepThumb, keepUp, keepDown;

        /// <summary>Swaps the skin's scrollbar for a thin gold one while the help draws, and back after.</summary>
        static void ThinScrollbars(bool on)
        {
            if (on)
            {
                if (track == null) { track = Pill(new Color(1f, 1f, 1f, 0.06f)); thumb = Pill(new Color(0.95f, 0.78f, 0.42f, 0.75f)); }
                keepBar = GUI.skin.verticalScrollbar;
                keepThumb = GUI.skin.verticalScrollbarThumb;
                GUI.skin.verticalScrollbar = new GUIStyle(keepBar) { fixedWidth = 8f, border = new RectOffset(4, 4, 4, 4), margin = new RectOffset(6, 0, 0, 0), padding = new RectOffset(0, 0, 0, 0) };
                GUI.skin.verticalScrollbar.normal.background = track;
                GUI.skin.verticalScrollbarThumb = new GUIStyle(keepThumb) { fixedWidth = 8f, border = new RectOffset(4, 4, 4, 4) };
                GUI.skin.verticalScrollbarThumb.normal.background = thumb;
                keepUp = GUI.skin.verticalScrollbarUpButton;
                keepDown = GUI.skin.verticalScrollbarDownButton;
                GUI.skin.verticalScrollbarUpButton = new GUIStyle(GUIStyle.none) { name = keepUp.name, fixedWidth = 8f, fixedHeight = 0.01f };
                GUI.skin.verticalScrollbarDownButton = new GUIStyle(GUIStyle.none) { name = keepDown.name, fixedWidth = 8f, fixedHeight = 0.01f };
            }
            else if (keepBar != null)
            {
                GUI.skin.verticalScrollbar = keepBar;
                GUI.skin.verticalScrollbarThumb = keepThumb;
                GUI.skin.verticalScrollbarUpButton = keepUp;
                GUI.skin.verticalScrollbarDownButton = keepDown;
            }
        }

        static Texture2D Pill(Color c)
        {
            var t = new Texture2D(8, 16, TextureFormat.RGBA32, false) { hideFlags = HideFlags.DontSave, wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < 16; y++)
                for (int x = 0; x < 8; x++)
                {
                    float cy = Mathf.Clamp(y + 0.5f, 4f, 12f);
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(4f, cy));
                    t.SetPixel(x, y, new Color(c.r, c.g, c.b, c.a * Mathf.Clamp01(4f - d)));
                }
            t.Apply();
            return t;
        }

        // ---------------------------------------------------------------- the rules, in cards

        struct Section { public string Heading, Body; }
        static readonly System.Collections.Generic.Dictionary<string, Section[]> parsed = new System.Collections.Generic.Dictionary<string, Section[]>();

        /// <summary>The text cut at its bold headings: one card each.</summary>
        static Section[] Sections(string text)
        {
            Section[] found;
            if (parsed.TryGetValue(text, out found)) return found;
            var list = new System.Collections.Generic.List<Section>();
            foreach (string block in text.Split(new[] { "<b>" }, System.StringSplitOptions.RemoveEmptyEntries))
            {
                int end = block.IndexOf("</b>");
                if (end < 0) { list.Add(new Section { Heading = "", Body = block.Trim() }); continue; }
                list.Add(new Section { Heading = block.Substring(0, end), Body = block.Substring(end + 4).Trim() });
            }
            found = list.ToArray();
            parsed[text] = found;
            return found;
        }

        static void DrawSections(Rect view, float width, string text)
        {
            Section[] sections = Sections(text);
            float inner = width - 36f;
            float total = 0f;
            foreach (Section sec in sections) total += SectionHeight(sec, inner) + 14f;

            scroll = GUI.BeginScrollView(view, scroll, new Rect(0f, 0f, width, total));
            float y = 0f;
            foreach (Section sec in sections)
            {
                float hgt = SectionHeight(sec, inner);
                Rect card = new Rect(0f, y, width, hgt);
                float hv = MenuArt.Hover(MenuArt.Key("sec" + sec.Heading, card), card.Contains(Event.current.mousePosition));
                if (Event.current.type == EventType.Repaint)
                {
                    MenuArt.RoundRect(card, CardFill(hv), 12f);
                    MenuArt.RoundRect(card, new Color(MenuArt.Gold.r, MenuArt.Gold.g, MenuArt.Gold.b, 0.1f + 0.35f * hv), 12f, 1f);
                    MenuArt.RoundRect(new Rect(card.x, card.y + 14f, 3f, 22f), MenuArt.Gold, 1.5f);
                }
                cardHeading.normal.textColor = MenuArt.Gold;
                GUI.Label(new Rect(18f, y + 12f, inner, 26f), sec.Heading, cardHeading);
                float bh = body.CalcHeight(new GUIContent(sec.Body), inner);
                GUI.Label(new Rect(18f, y + 42f, inner, bh), sec.Body, body);
                y += hgt + 14f;
            }
            GUI.EndScrollView();
        }

        /// <summary>A card a shade lighter than the pane, a touch more under the mouse.</summary>
        static Color CardFill(float hover)
        {
            return Color.Lerp(new Color(0.08f, 0.085f, 0.115f, 0.95f), new Color(0.1f, 0.105f, 0.14f, 0.95f), hover);
        }

        static float SectionHeight(Section sec, float inner)
        {
            return 42f + body.CalcHeight(new GUIContent(sec.Body), inner) + 14f;
        }

        // ---------------------------------------------------------------- the spells, in cards

        static void DrawSpells(Rect view, float width)
        {
            const float icon = 76f;
            float textX = icon + 40f, textW = width - textX - 18f;

            // measured first, so the scroll area is as tall as what is in it
            float total = 0f;
            foreach (SpellInfo s in Spells) total += SpellHeight(s, textW) + 14f;

            scroll = GUI.BeginScrollView(view, scroll, new Rect(0f, 0f, width, total));
            float y = 0f;
            foreach (SpellInfo s in Spells)
            {
                float hgt = SpellHeight(s, textW);
                Rect card = new Rect(0f, y, width, hgt);
                float hv = MenuArt.Hover(MenuArt.Key("spell" + s.Name, card), card.Contains(Event.current.mousePosition));
                Color c = s.Colour;
                if (Event.current.type == EventType.Repaint)
                {
                    MenuArt.RoundRect(card, CardFill(hv), 12f);
                    MenuArt.RoundRect(card, new Color(c.r, c.g, c.b, 0.14f + 0.5f * hv), 12f, 1f + hv * 0.6f);

                    // the rune, glowing in its colour
                    Rect plate = new Rect(16f, y + 16f, icon, icon);
                    MenuArt.Glow(plate.center, icon * (1.5f + 0.4f * hv), new Color(c.r, c.g, c.b, 0.12f + 0.15f * hv));
                    MenuArt.RoundRect(plate, new Color(0.03f, 0.035f, 0.05f, 0.9f), 12f);
                    MenuArt.RoundRect(plate, new Color(c.r, c.g, c.b, 0.45f + 0.4f * hv), 12f, 1.3f);
                    // the drawn icon, not MenuArt.Rune: lines turned by GUI.matrix land in the wrong place
                    // inside a scroll view
                    Texture2D rune = RuneIcons.Get(s.Rune, c, 64);
                    if (rune != null) GUI.DrawTexture(MenuArt.Grow(plate, -8f - 2f * (1f - hv)), rune);
                }

                name.normal.textColor = c;
                GUI.Label(new Rect(textX, y + 12f, textW, 28f), s.Name + "   <size=13><color=#aaaab4>" + s.Czech + "</color></size>", name);
                float ty = y + 42f;
                ty = Line(textX, ty, textW, s.Basics) + 4f;
                ty = Tier(textX, ty, textW, "I", s.I, c);
                ty = Tier(textX, ty, textW, "II", s.II, c);
                Tier(textX, ty, textW, "III", s.III, c);
                y += hgt + 14f;
            }
            GUI.EndScrollView();
        }

        const float BadgeWidth = 34f;

        static float SpellHeight(SpellInfo s, float width)
        {
            float h = 42f + small.CalcHeight(new GUIContent(s.Basics), width) + 2f + 4f;
            foreach (string line in new[] { s.I, s.II, s.III })
                h += Mathf.Max(20f, small.CalcHeight(new GUIContent(line), width - BadgeWidth - 8f)) + 4f;
            return Mathf.Max(h + 12f, 76f + 32f);
        }

        static float Line(float x, float y, float width, string text)
        {
            float h = small.CalcHeight(new GUIContent(text), width);
            GUI.Label(new Rect(x, y, width, h), text, small);
            return y + h + 2f;
        }

        /// <summary>A tier: its numeral on a little badge in the spell's colour, and what it adds.</summary>
        static float Tier(float x, float y, float width, string numeral, string text, Color c)
        {
            float tw = width - BadgeWidth - 8f;
            float h = Mathf.Max(20f, small.CalcHeight(new GUIContent(text), tw));
            if (Event.current.type == EventType.Repaint)
            {
                Rect b = new Rect(x, y + 1f, BadgeWidth, 18f);
                MenuArt.RoundRect(b, new Color(c.r, c.g, c.b, 0.16f), 9f);
                MenuArt.RoundRect(b, new Color(c.r, c.g, c.b, 0.6f), 9f, 1f);
                badge.normal.textColor = c;
                GUI.Label(b, numeral, badge);
            }
            GUI.Label(new Rect(x + BadgeWidth + 8f, y, tw, h), text, small);
            return y + h + 4f;
        }
    }
}
