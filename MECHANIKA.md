# Mage Cast — o čem ta hra je

Stav k 4. 10. 2026.

Dokument je úvod do hry, ne specifikace. Je rozdělený na tři části, protože se snadno slijí do jedné:
**co je postavené**, **co je rozhodnuté ale nepostavené** a **co je otevřené**. Čísla nejsou odhady —
pocházejí z nástrojů v `Assets/Editor/`, které jdou pustit znovu.

---

## Jedna věta

Multiplayerová PvP aréna ze třetí osoby, kde se kouzla neseslávají klávesou, ale **nakreslí myší**.

---

## Pravidlo, ze kterého plyne všechno ostatní

**Kreslení tě dělá zranitelným.**

Když kreslíš, kamera stojí, běháš na 60 % rychlosti a nemůžeš sprintovat ani skákat. Půl sekundy až
sekundu jsi napůl slepý a napůl bezbranný. To je cena za každé kouzlo.

Z toho vyplývá dvojí:

**Balanc se dělá délkou gesta, ne čísly.** Složitější tvar = delší okno = větší riziko. Nemusí se to
dolaďovat, vyplývá to samo.

**Soupeř tvůj tah vidí.** Kreslená čára se vykresluje ve světě, takže protivník pozná, co chystáš, ještě
než to vypustíš. Vzniká tím čtení záměru a blafování — a je to ta nejcennější věc v celém návrhu.

---

## Jak se sesílá

Kreslí a posílá se **levým tlačítkem**, ruší **pravým**:

```
1.  držíš levé               → kamera zamrzne, myš se změní na pero
2.  kreslíš runu             → tah se vykresluje, soupeř ho vidí   (pravé = zrušit runu)
3.  pustíš levé              → runa se vyhodnotí a oznámkuje, kouzlo máš v ruce
4.  míříš, jak dlouho chceš  → crosshair má barvu kouzla, žádný časový limit
5a. klikneš levým            → kouzlo letí tam, kam míří crosshair (hned při stisku)
5b. klikneš pravým           → kouzlo zahodíš (DROPPED), ruka je prázdná
```

Míření je pořád jedno a to samé: z kamery jde paprsek dopředu a kde trefí, tam kouzlo letí.

(Do 14. 10. se kouzlo zahazovalo podržením levého 0,45 s, a proto i každé odeslání čekalo na puštění
tlačítka.) Dokud kouzlo držíš, **jen jdeš** (70 % rychlosti, animace chůze) a **nejde sprint** — buď
máš připravené kouzlo, nebo jsi rychlý, ne obojí.

Bariéra se staví gestem bloku (zvednout ruce, spustit), skok z běhu vpřed má vlastní animaci skoku z
běhu.

Když by čára z ruky narazila (třeba do hrany sloupu, kolem kterého kamera vidí), kouzlo vyletí z čáry
kamery vedle ruky — letí vždycky tam, kam ukazuje crosshair.

Kouzlo letí **z ruky na straně, kam kamera kouká přes rameno** — výchozí je pravá. **Kliknutí kolečkem
myši** přehodí kameru přes druhé rameno a kouzla pak letí z levé ruky (animace je zrcadlená).

**Zrušit kreslení** jde kdykoli **pravým tlačítkem** — nic se nesešle a nic se nespotřebuje. Stejně skončí
kreslení, když tě trefí vzduch.

---

## Gesta a kouzla

Symboly jsou **vikingské runy**, kreslené jedním tahem. Význam runy je ta mnemotechnika — hráč se učí
malou abecedu, ne pět náhodných čmáranic.

| runa | tvar | kouzlo | poškození | rychlost | vlastnost |
|---|---|---|---|---|---|
| **Kenaz** (pochodeň) | `<` | oheň | 16 | 26 m/s (tier I 34) | běžný útok |
| **Laguz** (voda) | stonek s větví | led | 22 | 17 m/s (tier I 24) | pomalé a tlusté, snadno se uhne; vždy zpomalí |
| **Sowulo** (slunce) | klikatice | blesk | 12 | 44 m/s | skoro se nedá uhnout, málo ubere |
| **Ehwaz** (pohyb) | `M` | vzduch | **0** | 30 m/s | odhodí — vytáhne z krytu, shodí rozkreslené kouzlo |
| **Uruz** (síla, vytrvalost) | brána `∩` se šikmou střechou | bariéra | — | — | zavře směr na 8 s, dokud ji zásahy neprorazí |

**Vzduch nedává poškození, nikdy.** Kdyby dával, stal by se kouzlem na všechno. Takhle sám nikdy
nevyhraje, ale je u všech dobrých momentů — je to kouzlo, kterým soupeře **doručíš do svého ohně**.

Špatně nakreslený tvar prostě **nevyjde (fizzle)** a další runu jde začít až po 0,5 s. Dřív z něj létal náhodný „misfire“; ten je pryč —
odměnou za kreslení jsou jen tiery těch tvarů, které vyjdou.

Všechno jsou runy — kolečko pro bariéru nahradila Uruz, aby byla celá abeceda runová.

### Tiery

Jak dobře je runa nakreslená, dává kouzlu **tier I, II nebo III** (odfláknutá / průměrná / pečlivá).
Tier mění, **co kouzlo dělá**, ne jen kolik ubere — poškození samo se s přesností hýbe jen 0,8–1,2×.

| | I | II | III |
|---|---|---|---|
| **oheň** | zásah, rychlejší let, splash | + hoření na zasaženém (3/s na 2 s), plocha, odraz od zdi | + výbuch: všichni do 2,5 m kromě zasaženého dostanou polovinu zásahu (i autor), bariéru opotřebí 1,5× |
| **led** | zásah se zpomalením, rychlejší let, splash | + kluzká plocha | + zamrznutí na 0,6 s: nehýbe se ani neskočí, kreslit může |
| **blesk** | zásah, rychlejší let | + přeskočí na nejbližší jiný cíl do 6 m za polovinu; do země nechá **elektrickou plochu** (2,2 m, 4 s, 3/s, kdo v ní stojí, nemůže kreslit) | + elektrická plocha se objeví přímo pod zasaženým |
| **vzduch** | odhoz, rychlejší let; **náraz do zdi** po odhozu (do ~2 m od ní) = 8 dmg a 0,4 s na místě | + updraft plocha (vlastní tě vyhodí až na 4 m: střecha, most); do ohně **ohnivá vlna** (oheň zmizí a 8 m po větru se převalí stěna plamenů, zapálí a odhodí každého v cestě, zastaví ji zeď); na ledu rozklouže lidi | + v jakékoli ploše **vzduchová bomba**: všechny v ploše a do 1,5 m od ní jednou hodí do jejího středu (i autora) — **po 0,4 s**, aby se dalo uhnout |
| **bariéra** | stěna (8 s) | větší stěna (1,35× šířka), víc výdrže | **kopule** kolem tebe na 6 s, chodí s tebou, tvoje kouzla propouští, cizí zastaví |

- **Tier I je rychlá střela**: letí rychleji (oheň 34, led 24, blesk 52, vzduch 38 m/s), ale nezanechá
  žádnou plochu, odraz ani kombinaci (voda, ohnivá vlna...). Oheň a led tieru I mají **splash**: když
  dopadnou na zem, zeď nebo bariéru (ne do člověka), každý do 2 m dostane až 30 % zásahu (uprostřed
  nejvíc, na kraji nic), **i autor**. Zeď nebo bariéra mezi tím ho zastaví. Splash nesnižuje tier
  drženého kouzla a nic nezapaluje ani nezpomaluje.
- **Cooldown 2,5 s na prvek**: stejný prvek jde vytvořit znovu až 2,5 s po minulém odeslání. **Runa
  prvku, který má ještě cooldown nebo je zamčený, nevznikne** — po dokreslení se ukáže „FIRE NOT READY
  1,2 s“ (resp. LOCKED), soupeř nad tebou vidí NOT READY a čas kreslení je pryč: kdo nesleduje panel,
  platí. Čára při kreslení zčervená a dlaždice v panelu má červený rámeček, když by runa takhle nevyšla.
  Runa dokreslená do 0,4 s před koncem cooldownu ještě vznikne (a odešle se, jakmile to jde). Ostatní
  prvky jsou volné: Kenaz–Kenaz–Kenaz nejde, Kenaz–Sowulo–Kenaz ano. (Do 13. 10. 2 s a runa vznikla
  vždy, čekalo se jen s odesláním.) Blesk do ledu se vybije na každém tieru — je to jeho hlavní použití.
- **Globální cooldown (GCD), jako ve WoW, zvlášť pro každý druh kouzla:** po odeslání **útočného**
  kouzla (oheň, led, blesk, vzduch) nejde 1,5 s poslat žádné jiné útočné (do 13. 10. 1,3 s). GCD
  **nebrání vytvořit** runu, jen ji poslat. **Obranná** (bariéra) mají
  vlastní GCD, takže obrana nikdy nečeká na rytmus útoku; **speciální** (portál, zatím není) taky.
  Kreslit a držet jde i během GCD — další runa se kreslí v pauze a posílá na jejím konci. Klik v
  posledních 0,4 s se zapamatuje a kouzlo odletí samo, jakmile to jde (jako spell queue ve WoW).
  Proč: s pěti prvky na vlastních cooldownech bylo vždycky něco připravené a v šesti lidech vyhrávalo
  střílet cokoli. Panel kouzel ukazuje i GCD (sweep bez čísla).
- **Vzduch přeruší kreslení** při každém zásahu, na každém tieru.
- **Zámek školy (jako ve WoW):** když tě přeruší cizí kouzlo (zásah vzduchem, šokové pole) a z toho, co už
  máš rozkreslené, jde poznat, která runa to je, ten prvek se ti **zamkne na 3,5 s** — nad hlavou se ukáže
  „INTERRUPTED – FIRE LOCKED“ a v panelu kouzel červené „LOCKED“. Runa se čte stejně, jako ji čte soupeř: hotová
  runa podle sebe, rozkreslená podle prvních čar, a jen když jedna runa sedí jasně líp než ostatní
  („<“ je oheň, i když blesk začíná stejně). Přerušení moc brzy nezamkne nic — přerušit pozdě, když je
  runa vidět, se vyplatí víc. Menu, smrt ani šplhání nic nezamykají.
- **Tier III v ruce vydrží 4 s**, pak spadne na tier II (proužek pod názvem ukazuje, kolik zbývá). Jinak
  by se vyplatilo kreslit v klidu za krytem, dokud nepadne III, a s ním chodit. Tier II vydrží napořád.
- Tier je **vidět nad hlavou** — „FIRE III“; když spadne, objeví se „FIRE II“.
- **Zásah sráží tier kouzla v ruce.** Když tě cokoli trefí přímým zásahem, kouzlo, které držíš, klesne o
  tier; kouzlo s tierem I zmizí (LOST). Stání v ploše ani hoření se nepočítá.

V menu je **„How to play“** — ovládání, pravidla a všechna kouzla s runami a tiery. V nastavení je
hlasitost celé hry a zvlášť hlasitost kouzel (výchozí 70 %).

Dole uprostřed je **panel kouzel** jako ve WoW (`F6` ho schová): dlaždice pro každé kouzlo, útočná
pohromadě, obranná zvlášť (speciální, až budou, jako třetí skupina). Runy na dlaždicích kreslí skutečné
rohy šablon rozpoznávače, takže panel je zároveň tahák. Co ukazuje:
- **tmavá ručička** (čtvercový sweep po směru hodin od dvanácté), dokud kouzlo nejde poslat; **číslo**
  uprostřed = vlastní cooldown prvku nebo zámek. Jen **GCD** je sweep **bez čísla** (jako ve WoW),
- **červeně a LOCKED** — prvek zamčený přerušením,
- **zlatý rámeček a tier** (I/II/III) — kouzlo v ruce, nad ním **QUEUED**, když čeká odeslání,
- **bílý rámeček** — runa, kterou právě kreslíš, se teď čte jako tohle kouzlo,
- **záblesk** — cooldown právě skončil.

---

## Když kouzlo netrefí člověka

Jedno gesto dává několik efektů — rozhoduje, **do čeho kouzlo dopadne**. Nic nového se neučí: oheň pálí,
led klouže, blesk se uzemní, vzduch zvedá.

| | do soupeře | do stěny | na zem |
|---|---|---|---|
| **oheň** | 16 | odrazí se jednou, zbyde 80 % | hořící plocha — 2,5 m, 5 s, 3/s |
| **led** | 22 + zpomalení na 50 % na 1,2 s (každý tier) | nic | **kluzká plocha** — 3 m, 6 s |
| **blesk** | 12 | nic | nic, uzemní se — **kromě ledu** |
| **vzduch** | odhoz + **shodí rozkreslené kouzlo** | výbuch 3 m, odfoukne od stěny | vzdušný proud — 2 m, 5 s, vyhodí ~2 m |

**Led je kluzký, ne pomalý.** Rychlost zůstává, mizí přilnavost: rozběhnout se, zabrzdit i zatočit jde na
12 % obvyklého. Kdo na led vběhne, klouže dál tím směrem, kterým šel — z plného běhu zastaví za 0,9 s
místo 0,1 s. Ve hře o uhýbání je to kontrola: na ledu se neuhýbá.

## Kombinace

Kombinace vznikají **postupně**: jedno kouzlo leží na zemi, druhé do něj přiletí. Žádné dvojité
seslání — ta prodleva mezi dvěma casty je ta dovednost, soupeř vidí plochu, vidí tě kreslit a má čas odejít.

**Blesk do ledu** — celá ledová plocha se nabije a každý, **kdo se jí dotýká**, dostane **40 × síla
tahu** (čistě nakreslený blesk 52). Stačí trefit led, nebo někoho, kdo na ledu stojí. Kdo zrovna
vyskočil, tomu se nic nestane. Platí i pro toho, kdo led položil. Je to nejsilnější kombinace ve hře
schválně: stojí dva casty, dvě zranitelná okna, a cíl měl vteřiny na to z ledu slézt.

*Ověřeno měřením: panák na ledu dostal přesně 53,4 (40 × 1,335).*

Ostatní kombinace — vždy jedno kouzlo leží, druhé do něj přiletí:

| co leží | co přiletí | výsledek |
|---|---|---|
| led | oheň | **voda** — nekluže, nepálí, ale **vede blesk** úplně stejně jako led |
| oheň | led | **voda** (led oheň uhasí) |
| voda | oheň | oheň zhasne, voda zůstane |
| voda | led | voda **zase zmrzne** na led |
| voda | blesk | nabije se jako led |
| oheň | vzduch | **ohnivá vlna**: plocha zmizí a 8 m po větru se převalí stěna plamenů — zapálí a odhodí každého v cestě, zastaví ji zeď |
| led | vzduch | kdo stojí na ledu, **odletí po něm** ve směru větru, až za jeho kraj |

Voda vzniká jen takhle, žádná runa ji nedělá. Kombinace spočítá hostitel a ostatním pošle, jak teď
vypadá zem.

**Vzdušná plocha je clona.** Střely, které letí nad ní (do výšky 4 m), stočí nahoru a minou — všechny,
i vlastní. Rychlý blesk jen trochu, pomalý led úplně. *Ověřeno: oheň přes vzdušnou plochu panáka minul,
stejná rána bez ní ubrala 29,4.*

Záměrně nemá každé kouzlo všechno. Dvanáct efektů by byla polévka.

**Proč zrovna tak.** Oheň se odráží, aby šlo trefit někoho za krytem — kolem rohu. Blesk nedělá nic,
protože se mu skoro nedá uhnout, a jeho cenou je, že musí trefit člověka — nebo led. Vzduch vyhazuje
z krytu i do vzduchu, a kdo letí, letí po předvídatelné dráze — tam ho najde blesk. A led bere
schopnost uhýbat, takže kdo na něm kreslí, je terč; a je to plocha, na kterou blesk čeká.

**Pravidla, aby to drželo:**

- **Plocha nikdy nedá víc než přímý zásah.** Kdo stojí v ohni celých 5 s, dostane 15 — míň než přímých 16.
  Platí to při každé kvalitě, protože hoření roste se silou tahu stejně jako zásah.
- **Plochy žijí 5–6 s a každý hráč smí mít dole nejvýš dvě.** Třetí smaže tu nejstarší — aby se
  aréna za půl minuty nezměnila v kaši, kde nikdo neví, na čem stojí.
- **Stěna, nebo zem, rozhoduje sklon povrchu.** Co míří nahoru, je zem; zbytek je stěna.
- **Na zem kouzlo dopadne tam, kam míří jeho střed** — tedy tam, kam míří crosshair. Dřív velké kouzlo
  (hlavně vzduch) škrtlo okrajem o zem o 2–4 m dřív a do cíle nedoletělo.
- **Lidi a bariéry zasahuje celou šířkou, mapu jen jádrem (0,12 m).** Kouzlo těsně kolem sloupu nebo
  přes hranu krytu proletí; do člověka stačí trefit okrajem.
- **Kombinace stačí, když se kouzlo plochy dotkne** okrajem, nemusí trefit středem.
- **Plochy zasáhnou i toho, kdo je seslal.** Oheň pod vlastníma nohama není zadarmo.
- **Vzdušný proud vyhodí každého jen jednou**, jinak by z plochy byla trampolína.
- **Velikost ploch roste s velikostí glyfu** (0,7–1,4×), stejně jako velikost střely.
- **Plocha končí na hraně toho, na čem leží.** Dopadne-li kousek od kraje platformy, je u kraje
  rovně uříznutá a nevisí do vzduchu — a kdo stojí dole pod hranou, toho nezasáhne.
- **Plocha leží na kopci jako na kopci.** Na nerovné zemi (tráva ve Valley, rampa) kopíruje terén a
  zabírá ho celý; utne ji jen schod nebo hrana (víc než ~40° mezi dvěma body čtvrt metru od sebe).
  Kdo v ní stojí, se počítá podle výšky plochy tam, kde stojí. (Do 13. 10. byla plocha placatý disk,
  na svahu ji kopec napůl zakryl a ořízla se na pruh; navíc střela, která trávu jen škrtla, ji brala
  jako zeď — oheň se od trávy odrazil a žádná plocha nevznikla.)
- **Výbuch u stěny slábne se vzdáleností.** Kdo se o zeď opírá, dostane plnou sílu; kdo stojí dva metry
  opodál, jen štulec.

**Značka při míření — jen v tréninku, a jen co kouzlo v ruce na svém tieru opravdu udělá.** Dokud držíš
nakreslené kouzlo, ukazuje, co se stane tam, kam míříš (od 15. 10. podle tieru — tier I plochu nedělá,
tak ji ani neukazuje):

- **zem:** oheň a led od II kruh budoucí plochy, oheň III k tomu kruh výbuchu (2,5 m); blesk od II kruh
  elektrické plochy; vzduch od II kruh updraftu, na III mířený do plochy kruh dosahu vzduchové bomby;
  tier I nic (ani splash — kruh na zemi se četl jako plocha),
- **stěna:** oheň od II čára odrazu; vzduch kruh výbuchu u stěny (každý tier, leží na zdi, proto je
  svislý); jinak nic.

Slouží k naučení. **Ve hře s jinými hráči se neukazuje** — odhadnout odraz a dosah z geometrie je
součást dovednosti. Ve všech hrách zůstává jen 2D obrys ledové stěny a ohnivé čáry u nohou — to je o tom,
kam TY dáváš své kouzlo.

## Bariéra

Deska krytu postavená ve směru míření. **Blokuje z obou stran**, i vlastní kouzla. Střely se o ni
**zastaví, neodráží se** — bariéra, která by vracela oheň střelci, by byla odměna za to, že na tebe
někdo střílí.

**Opotřebovává se.** Každý zásah ubere tolik, kolik by dal hráči, krát podle prvku:

| prvek | krát | čistý zásah ubere |
|---|---|---|
| oheň | 1,5 | 44 |
| led | 1,0 | 39 |
| blesk | 0,5 | 8 |
| vzduch | vždy 3 | 3 |

Výdrž je **90** (do 14. 10. 60) krát tier: stěna tieru I 0,8× (72), tieru II 1,4× (126), kopule 1,6×
(144). Stěna stojí **8 s** (dřív 6), kopule **6 s** (dřív 4,5). Bariéra je od teď ten silný, vytrvalý
kryt — rychlý a křehký je ledová stěna (níž). Jak ubývá, bledne — soupeř vidí, že ještě jeden oheň a je
pryč. **Jedna na hráče**: nová zruší starou.

## Ledová stěna

**Led zamířený na zem blízko před sebe nevyletí, ale vyrazí ze země jako stěna ledových hrotů.**
Spouštěč: zaměřovač na zemi (ne na člověku, zdi ani římse) do **4,5 m** od tvých nohou a zhruba v jejich
výšce; blíž než 1,6 m se stěna posune na 1,6 m, aby nevyrostla skrz tebe. Když to platí, u drženého ledu
svítí **„WALL“** a na zemi leží **2D obrys plochy**, kterou stěna zabere (jen půdorys po terénu, žádný
rám do výšky — ten nešel poznat a stěna tak vysoko pokaždé nevyroste) — ve všech hrách, ne jen v tréninku,
aby stěna nikdy nebyla nechtěná.

- **rychlá a křehká:** stojí hned, **první zásah jakýmkoli kouzlem ji roztříští** (i tvým — je pevná z obou
  stran), po **6 s** sama roztaje do země,
- **pevná:** zastaví kouzla, lidi i pohled (runa ani jméno přes ni nejsou vidět),
- **stojí tvůj led:** spotřebuje cooldown ledu i útočné GCD — volíš mezi útokem a krytem,
- **velikost podle tieru:** I 2,0 × 1,7 m (kryt v podřepu), II 2,8 × 2,3 m, III 3,4 × 2,6 m,
- **oheň do ní:** roztaví ji a zůstane po ní **voda** (vede blesk) — oheň do ledu jako všude jinde; voda
  leží **oválem podél stěny** (na každém konci o 0,3 m delším než stěna, 0,9 m na každou stranu od ní),
- **boti** ji stavějí, když je někdo ostřeluje a jsou zranění (2,6 m před sebou, směrem k útočníkovi).

**Co je pevné:** neviditelný kvádr šířky a výšky stěny (mezi hroty se nedá prostřelit) a navíc **každý
hrot ve svém vlastním tvaru** — hroty vykloněné dopředu, do stran nebo jehly tieru III nad kvádr. Střela
se zastaví o led, který vidíš, a zásah kteréhokoli kusu je zásah stěny (roztříští ji celou). Při tání
klesá i neviditelný kvádr.

Vzhled: hroty a ledový materiál z balíku Stylized AoE (pointy rock), vyrážejí ze země od středu ke
krajům za ledovým výbuchem z téhož balíku; při rozbití se rozletí na střepy s ledovou explozí. Bez
balíku (klon z repozitáře) vlastní jednoduché krystaly.

## Ohnivá čára

**Oheň zamířený na zem blízko před sebe nevyletí, ale zapálí na zemi čáru napříč** — ohnivá obdoba ledové
stěny, jenže **nic nezastaví**. Spouštěč je stejný jako u stěny (do 4,5 m od nohou, ne blíž než 1,6 m);
u drženého ohně svítí **„LINE“** a na zemi leží 2D obrys čáry.

- **kdo jí projde, hoří** jako po zásahu tierem II (3/s na 2 s) — a znovu každou **1 s**, co v ní stojí;
  přeskočit se dá, spojenci ani autor nehoří,
- **délka podle tieru:** I 3 m, II 4,5 m, III 6 m; šířka 0,8 m, plameny do 1,8 m, hoří **4 s**,
- **stojí tvůj oheň:** cooldown ohně i útočné GCD jako výstřel,
- **čára přes průchod = dveře, za které se platí** — nebo zeď, za kterou soupeř nepůjde,
- **boti** ji kladou mezi sebe a soupeře, když se k nim blíží nebo když jsou zranění.

Vzhled: **stěna ohně složená z efektů balíků** — plameny ohnivé koule (Stylized VFX) stoupají z pásu
podél čáry, nad nimi žhavý kouř z jejího zásahu, jiskry a měkká záře na zemi z AoE ohně (žádný ostrý
obdélník). Čára se zapálí od místa dopadu ke krajům za ¼ s s ohnivým „vuuf“, svítí oranžovým světlem a
plameny sahají zhruba do výšky ramen. Kousek, který by stál ve zdi, se vynechá. Složené to je v
Tools/Arena/Assign Fire Line Effects (prefab v Assets/Vefects/_MageCast/FireLine, mimo repozitář); bez
balíků jednoduché plameny nad pásem uhlíků. Hořící člověk má plameny po celou dobu hoření (efekt z
anime balíku je jen krátký záblesk, tady hoří ve smyčce).

## Voda a mokrý (WET)

**Led, který proletí ohnivou čárou, z ní vyletí jako voda** — kohokoli led, kteroukoli čárou, takže to
může schytat i autor. Voda:

- dá **zásah ledu bez zpomalení** a zasažený je **mokrý 4 s** (modrý nádech a kape z něj, nad ním „WET“),
- z tieru II nechá na zemi **vodní plochu** (vede blesk),
- **blesk do mokrého = 2× poškození** („SHOCK x2“) a vysuší ho,
- **oheň do mokrého jen zasyčí** („STEAM“): nehoří, a je suchý — jediná výhoda mokra,
- **mokrý na ledové ploše přimrzne** („FROZEN“) na **1,5 s** — ať na ni vstoupí, nebo mu ji někdo dá pod
  nohy. Nehne se ani neskočí, kreslit může; voda zmrzla, takže je pak suchý. Kombo: led přes ohnivou čáru,
  pak led na zem.

Ověřeno (15. 10.): led tieru II přes čáru tieru III → voda, 12 poškození, mokrý; blesk 24 (místo 12), suchý;
další blesk 12; oheň na mokrého → bez hoření, suchý; bot stojící v čáře hoří. Suchý bot na ledové ploše
nepřimrzne; mokrému ji položenou pod nohy přimrzne (rychlost 0), mokrý přestane být a po 1,5 s se zase
hýbe. Voda po stěně tieru II rozbité ohněm: 1,7 m podél stěny, 0,9 m napříč.

Zvuk čáry (15. 10.): smyčka ohně je nahlas celou dobu (RMS 0,27), tak praská jen na 0,2 (dřív 0,6) a „vuuf“
zapálení je na 0,35 (dřív 0,7); hořící člověk 0,4 (dřív 0,6).

Ověřeno měřením (dřívější čísla): odraz vyšel zrcadlově na desetinu s poškozením 17,6, vzdušný proud
vyhodil rychlostí 9,4 m/s a výbuch u stěny
odhodil hráče směrem od ní.

---

## Kvalita tahu

Jsou to **dvě nezávislé branky** a snadno se pletou.

**Vzdálenost tvaru** rozhoduje, jestli to vůbec vyjde. Měří se v jednotkách, kde 1,0 znamená „tah s
roztřesením, na které je to nakalibrované" — takže je to číslo nezávislé na tom, který tvar kreslíš.

**Preciznost** rozhoduje o tieru (viz Tiery) a trochu i o síle: poškození jde plynule od **0,8×** za
odfláknutý tah po **1,2×** za dokonalý. Hlavní odměna za dobré kreslení je to, co kouzlo na vyšším tieru
navíc udělá, ne číslo.

**Rozpoznávač čte runu po čarách.** Tah se zjednoduší na rohy a čáry mezi nimi se porovnají **úhlem**
s čarami každé runy. Nezáleží, odkud začneš (každá runa se zkouší z obou konců), zrcadlená runa je ale
pořád jiná runa. Roh je skutečná změna směru (aspoň 36°): mírný ohyb je pořád jedna čára, zakulacený
roh se počítá jako jeden roh, cuknutí na začátku nebo na konci se zahodí. Čára víc než **35°** od své
čáry v runě = jiná čára, kouzlo nevyjde.

**Tier** počítá z toho, jak přesně úhly seděly (nejvíc), jak rovné čáry byly a jak odpovídá poměr jejich
délek. Hotové čáry se hned kreslí **narovnané** — ve stopě i v runě nad hlavou, kterou vidí soupeř.

Změřeno na stovkách strojově „ručně“ kreslených run (`Tools/Arena/Rune Recognition Bench`): pečlivě
100 %, průměrně 98–100 %, odfláknutě 78–89 % (zbytek většinou fizzle). Kolečka nevyjdou nikdy, náhodné
klikyháky v 6 %. Perfect dá jen pečlivá kresba.

Původní $P (mračno bodů) zůstává v tréninku na `F7` pro porovnání.

---


## Hra ve více lidech

**Menu:** jméno, *Training*, *Host a game*, *Join* s kódem. Trénink je ta samá hra, jen bez soupeře —
všechno jde stejnou cestou jako online, takže co funguje v tréninku, funguje i ve dvou.

**Připojení kódem.** Hostitel klikne *Host a game* a otevře se **lobby** s kódem místnosti (Copy code,
na webu i Copy invite link). Kdo kód zadá v menu, objeví se v lobby. Jde to přes Unity Relay, takže nikdo
nemusí otevírat porty ani znát cizí IP. Když hostitel odejde (*Close room*), hra končí i pro ostatní.
V místnosti je nejvýš 6 hráčů včetně botů (mapy mají 6 spawnů, 3 na stranu); přijde-li člověk do plné
místnosti, jeden bot mu uvolní místo.

**Lobby** (scéna `Lobby`, kreslí ji `LobbyScreen`, stav drží síťový objekt `Lobby`):
- **hostitel = leader.** Jen on vybírá **mapu** (karty s náhledem), **režim** (*Free for all* / *Teams*) a
  **počet killů k výhře** (5 / 10 / 15 / 20; režim nastaví výchozí 5, resp. 10), přidává a odebírá boty
  (v týmech přímo do týmu, bota přesune tlačítkem „to Red/Blue“) a nastavuje jejich dovednost.
- **ostatní** si v týmovém režimu vyberou tým (*Join Blue / Join Red*, max 3 na tým) a dají **I'M READY**.
  Změna týmu ready zruší. Když hostitel změní mapu, režim nebo počet killů, ready se zruší všem — nikdo
  nespustí zápas na mapě, se kterou ostatní nesouhlasili (ukáže se jim upozornění).
- **START MATCH** jde, až jsou ready všichni lidé (boti jsou ready vždy), jsou aspoň 2 hráči a v týmech má
  každý tým aspoň jednoho. Na tlačítku je vidět, na co se čeká.
- Lobby objekt není vázaný na scénu, takže jde se všemi do arény a zpátky. Kdo se připojí během zápasu,
  dostane tým (ten menší) a naskočí rovnou do arény.

**Co vidí soupeř:**
- **runu, jak ji kreslíš** — bíle nad tvou hlavou, tak jak ji kreslíš ty (ne zrcadlově). Jakmile ji
  pustíš a kouzlo držíš, zbarví se barvou kouzla. Tohle je druhá polovina hlavního pravidla: kreslení tě
  vystavuje, a soupeř to musí *vidět*, aby na to mohl reagovat,
- **název kouzla ve chvíli, kdy vznikne** — když dokreslíš a kouzlo držíš, ne až když ho vypustíš. Od té
  chvíle soupeř ví, co na něj čeká. Kritický zásah má vykřičník. Taky FIZZLE, INTERRUPTED a DROPPED,
- **poškození** jako číslo nad zasaženým. Hoření se sčítá do jednoho rostoucího čísla, ne čtyři za
  sekundu,
- tvoje jméno, životy a to, kam se díváš.

**Kdo o čem rozhoduje:**

| co | rozhoduje | proč |
|---|---|---|
| pohyb, míření, kreslení | každý sám za sebe | jinak by každý krok čekal na odezvu |
| jestli kouzlo trefilo, poškození | hostitel, jednou | zásah se počítá právě jednou |
| odhození, zpomalení, vyhození | tvůj počítač, na pokyn hostitele | tělo je tvoje |

Každý vidí svou kopii každého kouzla (letí, odráží se, bliká stejně), ale ubírá jen ta hostitelova.
Plocha na zemi je jedna — pošle ji hostitel všem.

**Smrt:** na nule padneš (animace pádu podle směru, odkud nejspíš přišla rána) a tělo leží bez kolize
až do respawnu. Za 3 s jsi zpátky na spawnu, který žádný živý soupeř nevidí, a z takových co nejdál od
ostatních. Nahoře se počítá skóre zabití / smrtí. Vlastní oheň nebo vlastní výboj v ledu tě zabít může,
ale soupeři to nepřipíše. Jméno soupeře nad hlavou vidíš, jen když ho vidíš (ne přes zeď).

**Zápas:** první na nastavený počet zabití (výchozí 5) vyhrává. Všem se ukáže vítěz a skóre, 6 s se
nikdo nehýbe, pak se všichni vrátí do lobby (bez lobby — jen když se aréna pustí přímo — se skóre vynuluje
a začne nový zápas). Hostitel může zápas ukončit dřív v Esc menu: *Back to the lobby*. V tréninku zápasy nejsou.

**Týmy** (režim *Teams*, Modří proti Červeným, max 3 na tým): počítají se killy celého týmu (výchozí 10),
nahoře je skóre obou týmů v jejich barvách. Modří začínají na spawnech A, Červení na B; respawn je na
spawnu vlastní strany, který žádný soupeř nevidí (jen když hlídá všechny tři, kdekoli). Pravidlo mezi
spoluhráči je jedno: **kouzlo spoluhráče ti nikdy neublíží ani tě neodstrčí**. Střely spoluhráčem proletí
(nezastaví se o něj, nezpomalí ho, neshodí mu runu), jeho oheň, výbuch, splash ani výboj ho nezraní, jeho
šokové pole mu nebere runu, vzduch ho neodhodí, bomba ho nevtáhne, blesk na něj nepřeskočí. Co je jen
zem, zůstává zemí: led spoluhráče klouže i tobě a **updraft spoluhráče tě vyhodí vysoko jako jeho samotného**
(výtah pro tým). Vlastní kouzla ubližují i tobě jako dřív. Jména spoluhráčů (v barvě týmu) jsou vidět i přes
zdi, soupeřů jen na přímý pohled. Boti útočí jen na soupeře.

**Boti:** v online hře je přidává hostitel v lobby (do týmu), v tréninku v Esc menu (*Add bot*,
*Remove bots*, dovednost 0–100 %, volba „boti mě nechají být“ pro sledování jejich souboje). Bot hraje stejným tělem a
podle stejných pravidel jako člověk: kreslí runu (trvá podle počtu čar, zpomalí ho, runa se mu kreslí nad
hlavou, vzduch mu ji shodí), runa mu může nevyjít nebo vyjít na nižším tieru, platí pro něj cooldowny, GCD,
zámek školy i NOT READY, chodí po mapě po spočítané síti (po schodech a rampách, na hrany nešplhá).

**Ví jen to, co by věděl člověk** (od 13. 10.; předtím viděl přes zdi, za sebe a reagoval v tom samém
snímku):
- **oči:** vidí asi 110° před sebe (jako kamera za zády) a kohokoli do 3,5 m; přes zeď nevidí. Když se
  prochází, rozhlíží se do stran,
- **paměť:** kde někoho naposledy viděl, si pamatuje 7 s — dojde tam, rozhlédne se, a když nikdo není,
  zapomene,
- **zásah:** ví, kdo ho trefil a odkud (jako ty podle směru střely), a otočí se tam,
- **reakce:** všechno vidí se zpožděním 0,38–0,17 s (podle dovednosti), takže předsazení míří tam, kam
  jsi *šel* — změna směru ho nechá minout. Chyba mušky roste se vzdáleností a s tím, jak rychle mu
  přebíháš před očima,
- **otáčení:** jen rychlostí ruky na myši (380–760 °/s) a kouzlo pošle, až má cíl před sebou.

**Dovednost (posuvník 0–100 %):** ovlivňuje mušku, předsazení, reakci (0,6–0,17 s), otáčení, rychlost a
jistotu kreslení (na 0 % fizzlne čtvrtina run a kreslí skoro o půl sekundy déle), pauzy mezi kouzly,
úhyby, jestli v boji stojí na místě a jestli vidí chytré kouzlo (blesk do ledu, vzduch na kreslícího).
Nejvíc se to projeví dole — kolem 60 % se bot skoro nezměnil, 0 % je začátečník. Naměřeno proti hráči,
který náhodně uhýbá do stran a nestřílí: 0 % ~15–30 poškození za minutu (dřív 99, tedy smrt každou
minutu), 50 % ~77, 100 % ~150.

**Chová se jako člověk:** nejdřív odpovídá tomu, kdo na něj střílí; po spatření soupeře chvíli počká, než
začne kreslit; střele, kterou vidí letět na sebe, uhne úkrokem, když to stihne (ne vždy, podle dovednosti);
těžce zraněný bez bariéry se na pár sekund schová z dohledu a pak se vrátí. Vybírá kouzlo podle situace:
blesk na soupeře na ledu nebo ve vodě, vzduch na blízko nebo na toho, kdo kreslí, bariéru, když je zraněný
nebo pod palbou, jinak oheň a led. Zabití se jim připisují (každý má vlastní číslo útočníka). Jejich zásahy
se počítají v přesnosti zvlášť, jako „(bots)“.

**Mapy:** v online hře vybírá hostitel v lobby, pro trénink se vybírá v menu — *Blockout* (původní šedá
aréna), *Temple*, *Crossing* a *Valley*. Náhledy map v lobby generuje `Tools/Arena/Build Map Previews`
(po změně mapy pustit znovu).

**Temple** (generuje `Tools/Arena/Build Temple Map`, měří `Tools/Arena/Audit Temple`): zřícený chrám
44 × 44 m, čtvrtina orazítkovaná čtyřikrát otočením. Výšky jsou jen tyhle: 1,2 skok, 1,6 kryt po prsa
(dá se vyšplhat), 2,4 hrana na vyšplhání, 4 patro (po rampě).
- **střed:** oltář 2,4 m s obeliskem, na každé straně schody,
- **rohy:** věž o dvou podlažích — místnost se dvěma dveřmi, uvnitř rampa na střechu s cimbuřím,
- **mosty:** ze střechy každé věže dolů na roh oltáře. Výhled, ale žádný kryt — a vzduch tě z nich shodí,
- **boční uličky:** půlka je propadlina 1,2 m (kouzla létají nad tebou, plochy se v ní drží), půlka ruina
  (zeď 2,4 m na vyšplhání, padlý sloup, kryt po prsa).

**Crossing** (`Tools/Arena/Build Crossing Map`, `Audit Crossing`): nádvoří 48 × 40 m s vyschlým
korytem řeky (1,2 m) napříč středem. Polovina otočená o 180° — tým B na +z, tým A na −z.
- **brod:** prostředních 6 m koryta je pevná zem se sochou,
- **mosty:** na x = ±15 ve výšce 2,4 m, pod nimi se dá projít korytem; jeden konec ústí na terasu s
  cimbuřím, druhý sestupuje po schodech,
- **břehy:** kolonáda, zřícená kaple bez střechy, ruiny brány vzadu (za nimi spawny), v korytě kameny.
- **vzhled je záměrně tlumený** (šedomodrý kámen, šedozelený mech, jen pár slabých teplých lamp), aby
  nejbarevnější věc na obrazovce byla kouzla; nic nesvítí studeně, protože tyrkysová čára vypadá jako led.
  Slunce svítí z boku, aby oba týmy měly stejné světlo.

**Valley** (`Tools/Arena/Build Valley Map`, `Audit Valley`): travnaté údolí 88 × 88 m (čtyřikrát
víc plochy než Temple) obklopené horami. Terén je generovaný: mírné kopce (±2,4 m), za okrajem hory,
na úpatí neviditelná hranice. Polovina otočená o 180°, tým B na +z, tým A na −z.
- **pevnost uprostřed** a **čtyři zřícené kruhové věže**: zbytky zdí rostou po kruhu vždy o skok nebo
  vyšplhání výš, takže se po nich dá vylézt až na podlahu nahoře (4,8 m),
- **skupiny balvanů** asi 13 m od sebe (velký kámen na schování, menší na vylezení) a mezi nimi otevřená
  tráva, **zbořené zdi** a jedna u každého spawnu,
- tlumené barvy (tráva ztlumená, šedé kameny), slunce z boku, bez mlhy.

**Pohyb:** chůze 5,0 m/s, sprint (Shift) 7,2 m/s. Rozjezd do klusu ~0,3 s, do sprintu ~0,5 s, zastavení ~0,2 s, otočka úhybu ~0,5 s (setrvačnost ~0,4 m), zatáčka o 90° ~0,3 s; změna směru je oblouk, ne zlom, aby se dal soupeř předvídat. Ve vzduchu se dá skok jen trochu ovlivnit. Měří to `Tools/Arena/Audit Movement`. Sprint stojí staminu (žlutý pruh nad životy):
plná vydrží 4 s sprintu, doplní se za 3,5 s od chvíle, kdy přestaneš (s pauzou 0,8 s). Když ji
vyčerpáš, sprintovat jde znovu až od 30 %. Skok z běhu vpřed (i bez Shiftu) má animaci skoku z běhu. Tvrdý dopad (pád z ~3 m a víc) v pohybu je
kotoul.

**Šplhání:** skoč proti hraně a drž směr k ní. Na cokoli, co má vršek do 1,3 m nad tvýma nohama ve
skoku (tj. hrany do ~2,6 m), se vytáhneš za 0,7 s (ruce na hraně, podřep, vstaneš). Během šplhání nekreslíš ani neposíláš (kouzlo v
ruce zůstává) a přímý zásah tě pustí dolů. Na 2,8 m zeď ani na 4 m patro se vyšplhat nedá.

---

## Rozhodnuté, ale nepostavené

**Běžný zásah trhne rukou.** Do rozkresleného tahu se přimíchá odchylka a zbytek dořeší hodnocení
kvality. Žádné nové pravidlo, žádné nové UI. Důsledek: pod palbou sešleš slabé kouzlo, v krytu silné.
*(Postavená je jen polovina: vzduch kresbu shodí celou. Tohle trhnutí rukou u ostatních kouzel zatím ne.
Ve dvou už to vyzkoušet jde.)* **Už nakreslené a držené kouzlo je
nedotknutelné** — riziko je v kreslení, ne v držení.

**Minimální vzdálenost odjištění** (~3 m), aby se nedalo spamovat z bezprostřední blízkosti.

**Obrana napříč cenami:** kolečko jako rychlý panický štít, Othala jako pořádná postavená hradba,
osobní štít, co pohltí poškození a chodí s tebou.

---

## Otevřené

**V aréně není o co hrát.** Žádný bod, co se drží, nic, co se sbírá. Plochy z kouzel teď aspoň dělají
**dočasné území** — místa, kam nechceš vstoupit, a místa, kam soupeře zaháníš — ale jen na pár vteřin.
Trvalý důvod někde stát pořád chybí, a za mě je to **největší chybějící věc**. S ním naráz dostanou smysl
kryty, průhledy, plochy i zábrany.

**Blízkost přebíjí všechno ostatní.** Na 10 m se střele uhne, na 6 m ne — doba letu klesá rychleji než
schopnost uhnout. A hlavně: couvání je nejvýš 4,3 m/s (se Shiftem) proti sprintu vpřed 7,2 m/s, takže **jakmile se k tobě někdo
dostane, nemůžeš odejít**. To není o obtížnosti, to je chybějící možnost.

**Slovník se hroutí na blízko.** Pod šesti metry není čas na nic delšího než nejrychlejší gesto, takže
ve vrcholu souboje ti mechanika vypne. Nabízené řešení: nechat hráče volit, jak rychle kreslí —
rychleji a špinavěji, nebo pomaleji a přesněji.

**Prahy nejsou kalibrované na skutečnou ruku.** Všechny pocházejí ze syntetického roztřesení. Nástroj
`Calibrate From Cast Log` je hotový a čeká na asi 40 skutečných tahů na symbol.

**Léčení schválně chybí.** V souboji dvou lidí prodlužuje remízy a odměňuje pasivnějšího, což je opak
toho, na čem hra stojí. Kdyby bylo potřeba, spíš pomalá regenerace bez gesta než kouzlo.

---

## Riziko, které rozhodne o osudu projektu

Malé PvP hry neumírají na špatný koncept, ale na prázdná lobby. Obrana patří do designu, ne do
marketingu: boti do zápasů od začátku, malé formáty (1v1 a 3v3, ne 5v5), a tréninkový nebo PvE mód, aby
hra dávala smysl i při nule hráčů online.

**Referenční bod:** Mage Arena (7/2025, hlasové castění, sólo vývojář, Unity) — 119 tis. kopií za první
týden, ~818 tis. celkem. Vydáno zjevně rozbité a lidem to nevadilo. Neprodalo se to proto, že bylo
vyladěné, ale proto, že to mělo jednu věc, kterou nikdo jiný neměl — a ta šla natočit na
třicetisekundový klip.

**Varovný protipříklad:** Arx Fatalis (2002) měl kreslení run myší, kritika ho milovala, komerčně
propadl. Rozdíl je v tom, že jeho selhání nebyla vtipná.
