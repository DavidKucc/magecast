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

Všechno je na **levém tlačítku**:

```
1.  držíš levé               → kamera zamrzne, myš se změní na pero
2.  kreslíš runu             → tah se vykresluje, soupeř ho vidí
3.  pustíš levé              → runa se vyhodnotí a oznámkuje, kouzlo máš v ruce
4.  míříš, jak dlouho chceš  → crosshair má barvu kouzla, žádný časový limit
5a. klikneš levým            → kouzlo letí tam, kam míří crosshair
5b. podržíš levé a pustíš    → kouzlo zahodíš (DROPPED), ruka je prázdná
```

Míření je pořád jedno a to samé: z kamery jde paprsek dopředu a kde trefí, tam kouzlo letí.

Rozdíl mezi kliknutím a podržením je **0,45 s**; při držení se vlevo nahoře plní proužek, a jakmile je
plný, puštění kouzlo zahodí. Dokud kouzlo držíš, **jen jdeš** (70 % rychlosti, animace chůze) a **nejde
sprint** — buď máš připravené kouzlo, nebo jsi rychlý, ne obojí.

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
| **Uruz** (síla, vytrvalost) | brána `∩` se šikmou střechou | bariéra | — | — | zavře směr na 6 s, dokud ji zásahy neprorazí |

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
| **vzduch** | odhoz, rychlejší let | + updraft plocha; do ohně **ohnivá vlna** (oheň zmizí a 8 m po větru se převalí stěna plamenů, zapálí a odhodí každého v cestě, zastaví ji zeď); na ledu rozklouže lidi | + v jakékoli ploše **vzduchová bomba**: všechny v ploše a do 1,5 m od ní jednou hodí do jejího středu (i autora) — **po 0,4 s**, aby se dalo uhnout |
| **bariéra** | stěna | větší stěna (1,35× šířka), víc výdrže | **kopule** kolem tebe na 4,5 s, chodí s tebou, tvoje kouzla propouští, cizí zastaví |

- **Tier I je rychlá střela**: letí rychleji (oheň 34, led 24, blesk 52, vzduch 38 m/s), ale nezanechá
  žádnou plochu, odraz ani kombinaci (voda, ohnivá vlna...). Oheň a led tieru I mají **splash**: když
  dopadnou na zem, zeď nebo bariéru (ne do člověka), každý do 2 m dostane až 30 % zásahu (uprostřed
  nejvíc, na kraji nic), **i autor**. Zeď nebo bariéra mezi tím ho zastaví. Splash nesnižuje tier
  drženého kouzla a nic nezapaluje ani nezpomaluje.
- **Cooldown 2 s na prvek**: stejný prvek jde poslat znovu až 2 s po minulém. Kreslit a držet ho můžeš
  i mezitím (čára je při kreslení červená), ostatní prvky jsou volné. Tabulka run (F6) ukazuje zbývající
  čas. Kenaz–Kenaz–Kenaz nejde, Kenaz–Sowulo–Kenaz ano. Blesk do
  ledu se vybije na každém tieru — je to jeho hlavní použití.
- **Vzduch přeruší kreslení** při každém zásahu, na každém tieru.
- **Tier III v ruce vydrží 4 s**, pak spadne na tier II (proužek pod názvem ukazuje, kolik zbývá). Jinak
  by se vyplatilo kreslit v klidu za krytem, dokud nepadne III, a s ním chodit. Tier II vydrží napořád.
- Tier je **vidět nad hlavou** — „FIRE III“; když spadne, objeví se „FIRE II“.
- **Zásah sráží tier kouzla v ruce.** Když tě cokoli trefí přímým zásahem, kouzlo, které držíš, klesne o
  tier; kouzlo s tierem I zmizí (LOST). Stání v ploše ani hoření se nepočítá.

V menu je **„How to play“** — ovládání, pravidla a všechna kouzla s runami a tiery. V nastavení je
hlasitost celé hry a zvlášť hlasitost kouzel (výchozí 70 %).

V pravém horním rohu je **legenda** se všemi gesty (`F6` ji schová). Kreslí skutečné šablony
rozpoznávače, ne ručně dělané ikonky, aby nikdy nemohla ukazovat něco jiného, než co hra čeká.

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
- **Výbuch u stěny slábne se vzdáleností.** Kdo se o zeď opírá, dostane plnou sílu; kdo stojí dva metry
  opodál, jen štulec.

**Značka při míření — jen v tréninku.** Dokud držíš nakreslené kouzlo, ukazuje, co se stane tam, kam
míříš: kruh na zemi velký jako budoucí plocha, na stěně čáru, kudy se oheň odrazí, kruh výbuchu vzduchu
(leží na zdi, proto je svislý) a šedý kroužek tam, kde kouzlo nic neudělá. Slouží k naučení. **Ve hře
proti člověku se neukazuje** — odhadnout odraz a dosah z geometrie je součást dovednosti.

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

Výdrž je **60** krát 0,6–1,4 podle přesnosti kreslení (36–84). Běžná bariéra tedy vydrží jeden oheň a
druhý ji prorazí, blesků snese kolem sedmi, vzduch ji skoro nepoškodí. Jak ubývá, bledne — soupeř vidí,
že ještě jeden oheň a je pryč. **Jedna na hráče**: nová zruší starou.

*Ověřeno: čistá bariéra po jednom ohni 27 %, druhý ji rozbil.*

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


## Hra ve dvou

**Menu:** jméno, *Training*, *Host a game*, *Join* s kódem. Trénink je ta samá hra, jen bez soupeře —
všechno jde stejnou cestou jako online, takže co funguje v tréninku, funguje i ve dvou.

**Připojení kódem.** Hostitel klikne *Host a game*, nahoře v aréně uvidí kód (Esc → zkopírovat) a
pošle ho druhému. Ten ho zadá v menu a je ve hře. Jde to přes Unity Relay, takže nikdo nemusí otevírat
porty ani znát cizí IP. Když hostitel odejde, hra končí i pro ostatní.

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

**Zápas:** první na 5 zabití vyhrává. Všem se ukáže vítěz a skóre, 6 s se nikdo nehýbe, pak se skóre
vynuluje a všichni začnou znovu z protilehlých spawnů. V tréninku zápasy nejsou.

**Mapy:** hostitel (a trénink) vybírá v menu — *Blockout* (původní šedá aréna), *Temple* a *Crossing*. Kdo se
připojí kódem, jde tam, kde je hostitel.

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

**Pohyb:** chůze 5,0 m/s, sprint (Shift) 7,2 m/s. Sprint stojí staminu (žlutý pruh nad životy):
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
