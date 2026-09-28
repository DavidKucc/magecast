# Systém kouzel — pravidla a fungování

Stav ke konci designové session. Rozhodnuté věci vs. otevřené jsou rozlišené — otevřené jsou vždy označené.

---

## 0. Základní ovládání

- Normální stav: WASD pohyb, myš míří a otáčí kamerou.
- **Držení pravého tlačítka** = režim kreslení:
  - směr míření a kamera **zamrznou** v natočení z okamžiku stisku
  - myš od té chvíle kreslí gesto
  - **WASD funguje dál** — hráč vidí dopředu a může uhýbat do stran
  - puštění = vyhodnocení gesta a seslání do zamčeného směru
- Cena castu není „nevidím", ale **„nemůžu přemířit"**. Souboj je proto o předvídání pohybu do stran.
- Pohyb během castu zpomalen na ~60 %, bez sprintu a skoku.
- Samostatná citlivost myši pro kreslení a pro míření.

**Dopad na mapu:** otevřenější prostory (musí být kam uhýbat), překážky spíš jako body k obíhání než kryty. Hodně místa do stran.

---

## 1. Arzenál — role prvků

| Prvek | Role | Povaha |
|---|---|---|
| **Oheň** | Area denial | Pomalá střela, hlavní hodnota je hořící plocha, ne zásah |
| **Led** | Kontrola | Kluzká plocha — bere soupeři schopnost uhýbat |
| **Blesk** | Burst | Rychlá střela, malý přímý damage, **jiné chování na vodiči** |
| **Vzduch** | Manipulace | **Nedělá damage vůbec.** Hýbe hráči, štítem, rozviřuje oheň |
| **Štít** | Filtr projektilů | Průhledný energetický panel ve vzduchu před hráčem, má durabilitu |
| **Portál** | Geometrie | Pár vstup/výstup. Drahý. **Ne v 1v1.** |

**Vzduch bez damage je záměr.** Kdyby poškozoval, stal by se univerzálním kouzlem a spamoval by se. Takhle nikdy nevyhraje sám, ale je ve všech nejlepších momentech hry.

---

## 2. Tři vrstvy existence kouzla

Každé kouzlo žije jako **střela → dopad → zbytek (plocha)**. Interakce se dějí ve všech třech. Z toho plyne kombinatorika, kterou není potřeba ručně vypisovat.

**Zásadní pravidlo:** kombinace vznikají **sekvenčně přes čas**, ne dvěma střelami současně.

1. Nakreslíš led → pošleš → plocha leží 5–6 s
2. Za dvě vteřiny nakreslíš blesk → pošleš do plochy → celá se nabije

Žádné dvojité seslání, žádné combo tlačítko. **To zpoždění mezi casty je ten skill** — soupeř vidí plochu, vidí tě kreslit a má čas odejít.

Kombo stojí dva casty = dvě zranitelná okna, proto musí být silnější než dva samostatné zásahy.

**Sloty na předpřipravená kouzla (2 ks) mají díky tomu jasnou roli: jsou to zrychlovače, ne komba.** Předpřipravíš blesk v klidu → položíš led → blesk vypustíš okamžitě. Soupeř nestihne uhnout, protože nevidí druhý cast. Vyvážené tím, že příprava stála čas dřív.

---

## 3. Matice interakcí

Platné jen tam, kde **jedna věc už na mapě leží**. Interakce dvou střel ve vzduchu se neřeší (nerealizovatelné).

### Střela → plocha
| Kombinace | Výsledek |
|---|---|
| Oheň → led | **Voda** — nová plocha, vodič pro blesk |
| Blesk → led | **Celá plocha se nabije**, chain po ní. Nejsilnější kombo ve hře |
| Blesk → voda | Totéž jako led |
| Vzduch → oheň | Plamen se rozvíří a **rozšíří ve směru tlaku** |
| Vzduch → led | Plocha zůstane, ale **hráči a objekty na ní odletí** (nulové tření = katapult) |

### Střela → štít
| Kombinace | Výsledek |
|---|---|
| Oheň → štít | Vysoký odběr durability |
| Blesk → štít | Nízký odběr |
| Vzduch → štít | Skoro žádný odběr, ale **posune / natočí** (viz sekce 5) |

### Plocha → plocha
| Kombinace | Výsledek |
|---|---|
| Oheň na ledu | Led taje → voda |

**Poznámka:** deset pravidel je na nováčka hodně, ale nemusí je znát. Čtyři jsou intuitivní (oheň taví led, blesk jde po vodě, vzduch odfoukne, štít blokuje), zbytek je prostor pro objevování a sdílení na Discordu.

---

## 4. Vzduch — kompletní chování

**Nedělá damage. Nikdy. Za žádných okolností.**

### Jako střela
- **Hráč** → odhodí dozadu
- **Spoluhráč** → odhodí taky, **bez jakéhokoliv buffu** (buff by se změnil v povinnost a v rankedu by vznikla meta „pořád foukej na parťáka"). Odhození spoluhráče je movement tech — boost přes propast, vystřelení nahoru, odhození pryč z hořící plochy. Zábavnější než buff.
- **Sebe** → krátký dash
- **Štít** → posun a natočení
- **Hořící plocha** → rozvíří a rozšíří
- **Ledová plocha** → hráči na ní odletí přes celou její délku

**Pravidlo, které si hráč zapamatuje:** *vzduch hýbe věcmi, ne zemí. Kromě ohně, ten rozfouká.*

### Jako plocha (past)
- Kdo do ní vleze, **vyletí nahoru**
- Vertikální odhození je horší než horizontální: **ve vzduchu nejde uhýbat**, hráč ztratí WASD kontrolu = visí jako terč
- **Střely nad ní se vychýlí nahoru a minou** → tohle je realizovatelná verze „counterování střel"; clona, ne reflex
- **Funguje i na majitele** → mobilita, přístup nahoru, únik

### Proč odhození nesmí být samo o sobě slabé
Odhození má cenu jen tam, **kde je kam odhodit**. Aréna proto potřebuje:
- kraje a propasti (nebo aspoň sesuv o patro níž)
- překážky — **náraz do zdi = malý damage + krátký stun**
- vlastní a spoluhráčovy plochy, kam se soupeř dá nastrkat

Vzduch není kouzlo na zabíjení — je to kouzlo, kterým **doručuješ soupeře do svého ohně**.

---

## 5. Štít

Průhledný energetický panel ve vzduchu před hráčem. **Není to kryt pro celé tělo** — chrání jen to, co je za ním v daném směru.

- **Má orientaci** → postavit ho do směru, odkud přijde rána, je samo o sobě skill
- **Durabilita se odečítá podle síly zásahu**, ne po hitech: oheň hodně, blesk málo, vzduch skoro nic
- **Obejít ho je základní counterplay** — hráč se posune do strany a střílí kolem. Sedí to k tomu, že se během castu smí pohybovat
- **Nejkratší gesto ze všech** (rovná čára / jednoduchý oblouk). Kdyby byl drahý na nakreslení, vznikne past: potřebuješ bezpečí, abys mohl nakreslit to, co ti dá bezpečí
- **Jeden štít na hráče** — nový ruší starý

### Odrazy: ZAHOZENO
Původní nápad byl odrážet střely od vlastního štítu. **Nefunguje:** když stojíš čelem proti soupeři, odraz jde zpátky k němu, takže je to odměna, ne risk. Horší je, že frontální odraz vytváří **nudnou situaci** — proti štítu, který vrací kouzla, se prostě nestřílí a souboj se zasekne.

**Odrazy zůstávají jen pro zdi a mapovou geometrii**, kde je hráč řídí pozicí a míření je čitelné. Některá kouzla se odrážejí 1× od stěny.

### Vzduch vs. štít (bod zásahu rozhoduje)
- **Trefíš střed** → štít se odsune o ~1 m dozadu, **sekavě**, okamžité zastavení. Malý zisk.
- **Trefíš kraj nebo roh** → štít se **natočí** kolem středu o ~20–30°. Natočený štít už nekryje to, co kryl předtím — soupeř si myslí, že je za ním, ale je odkrytý z boku.

Maximálně 2 zásahy (pak je štít „zaseknutý", aby nešel otočit o 180°). **Štít se nevrací** do původní pozice — majitel ho musí zrušit a postavit nový, což ho stojí cast.

Tohle odměňuje mířením: trefit roh je těžší než střed a dostaneš za to víc.

---

## 6. Portál

Pár vstup/výstup, každý zvlášť nakreslený. **Nejdelší gesto ve hře (spirála), dva casty = dvě zranitelná okna.**

### Pravidla
- **Patří všem.** Neexistuje „můj portál" — jakmile stojí, je to kus mapy pro obě strany
- **Obousměrný pro všechno**: střely i hráči, oběma směry
- **Oba konce hlasitě vidět** — svislý sloup světla přes celou arénu. Ne tichá past, ale oznámená hrozba
- **Jeden pár na hráče**
- **Cooldown na průchod hráčem** (1–2 s), jinak ping-pong, kdy někdo skáče tam a zpět a nedá se trefit
- **Kouzlo projde jen jednou** — po průchodu má střela příznak (bool `hasTeleported`), podruhé jím proletí bez efektu. Bez toho by šly vstup a výstup postavit proti sobě a fireball by kroužil donekonečna
- **Plochy portálem neprojdou.** Plocha není projektil, je to stav země — průchod by byl nečitelný
- **Střela si zachová směr podle orientace VÝSTUPU**, ne podle toho, kudy letěla → orientace výstupu je tvoje míření

### Proč obousměrnost drží balanc
Kdyby šlo položit výstup u soupeře a z bezpečí do vstupu posílat kouzlo za kouzlem, je to nekontrovatelné. Obousměrnost to řeší: **položit výstup u soupeře znamená otevřít mu dveře přímo k sobě.** Risk i zisk zároveň = rozhodnutí místo exploitu.

### Co z toho vzniká
- Vzduchem odfoukneš soupeře do vstupu → vypadne jinde v aréně
- Postavíš výstup a před něj svůj štít → cokoliv projde, narazí
- Soupeř vidí, že kreslíš oheň a míříš do vstupu → ví, kam to vyletí, má 2 s doběhnout k výstupu a postavit si tam vlastní štít nebo uhnout

**Portál je násobič všeho ostatního** — každé kouzlo získá nové použití bez jediného nového pravidla.

### Kde NE
**Ne v 1v1.** Na malé mapě pro duel zničí čtení pozice, což je tam celá hra. Jen deathmatch a týmové módy.

---

## 7. Counter cyklus

Není to kámen-nůžky-papír, ale cyklus s výjimkami — odolnější proti zaseknutí mety:

- **Oheň** countruje led (roztaví) a štít (vysoký odběr durability)
- **Led** countruje oheň (uhasí), posiluje kontrolu
- **Blesk** countruje toho, kdo stojí na plochách — trestá kontrolu
- **Vzduch** countruje pozici, štít a plochy — ale nedělá damage, takže sám nikdy nevyhraje
- **Štít** countruje všechno na moment, ale stojí cast a dá se obejít

---

## 8. Limity proti nečitelnosti

Největší riziko systémů s plochami v PvP je, že se aréna během třiceti vteřin změní v kaši, kde nikdo neví, na čem stojí.

- **Max 2 aktivní plochy na hráče** — třetí zruší nejstarší
- **Plochy žijí 5–6 s** a viditelně blednou
- **1 štít na hráče** — nový ruší starý
- **1 pár portálů na hráče**
- **Plocha se dá posunout jen jednou**, pak je zafixovaná (zabrání ping-pongu)
- **Každá plocha jednoznačná barva a silueta**, viditelná i periferně

---

## 9. Tiery gest a jejich napojení na kouzla

Skóre z $P rozpoznávače (viz předchozí dokument) neovlivňuje **které** kouzlo se seslá, jen **jak dobře** vyjde.

| Skóre | Tier |
|---|---|
| pod 0,45 | Fizzle |
| 0,45–0,70 | Sloppy |
| 0,70–0,88 | Clean |
| nad 0,88 | Perfect |

**Perfect nedává jen víc damage — dává schopnost:**

| Kouzlo | Perfect efekt |
|---|---|
| Oheň | Plocha o polovinu větší |
| Led | Větší / déle trvající plocha |
| Blesk | Řetězí o jeden cíl dál |
| Vzduch | Odhodí dál, natočí štít víc |
| Štít | Výrazně víc durability |
| Odrazivé kouzlo | **Druhý odraz** |

Ten poslední je nejlepší odměna v systému — není to číslo, je to nová taktická možnost a hráč okamžitě pozná, že ji dostal.

K tomu deterministická odchylka: rozdíl těžiště tahu a šablony jako 2D vektor přičtený ke směru střely, škálovaný podle tieru. **Žádný `Random.Range`.**

---

## 10. Životy

**Heal na gesto = ZAHOZENO.** Rozbil by tempo souboje (v duelu by se hráči léčili do nekonečna) a byla by to sedmá věc k naučení za nejnudnější efekt ve hře.

### Kolový formát (preferováno pro duel a ranked)
Best of 5, kolo 60–90 s, **HP se resetuje mezi koly**. Léčení není potřeba vůbec. Každé kolo má oblouk: opatrný začátek, zoufalý konec.

### Pickupy (jen pro deathmatch)
- **Pevné pozice**, ne náhodné — oba hráči musí vědět, kam běžet
- **Respawn 20–30 s** s viditelným odpočtem → timing pickupu je klasická aréna dovednost
- **Málo HP** (~25–30 %) — nesmí obracet souboje, jen prodlužovat
- **Sebrání trvá ~1 s stání na místě** → risk, soupeř ví, kde jsi a co děláš
- **Exponovaná místa**, ne rohy → z pickupu se stane bojiště

---

## 11. Friendly fire

- **Damage od spoluhráče: NE**
- **Fyzika od spoluhráče: ANO**

Po parťákově ledu kloužeš. Jeho vzduch tě odhodí. Jeho plocha vzduchu tě vystřelí nahoru. Zachová to komiku a movement tech bez frustrace ze zabití vlastním týmem.

V 3v3 systém teprve ožije, protože **kombinace jdou přes hráče**: parťák položí led, ty pustíš blesk; on postaví štít, ty od zdi za ním banknete střelu; on odfoukne soupeře na tvoji hořící plochu. Nula práce navíc, vyplyne to z pravidel samo.

---

## 12. Otevřené otázky

- Konkrétní gesto pro portál (zatím spirála) a jestli vstup a výstup mají mít odlišné gesto, nebo dvakrát to samé
- Jestli má náraz do zdi po odhození dělat damage, nebo jen stun
- Durabilita štítu v číslech a kolik z ní ubere který prvek
- Jestli voda (oheň + led) je samostatná plocha se svými pravidly, nebo jen „led bez klouzání + vodič"
- Zda plocha vzduchu vyhazuje i střely směrem nahoru, nebo jen odklání
- Kolik gest celkem — teď 6 (oheň, led, blesk, vzduch, štít, portál), strop zapamatovatelnosti je asi 8

---

## 13. Test, který rozhodne nejvíc

Vzít prototyp a zkusit **jednu jedinou kombinaci: led na zem, pak blesk do něj.**

Když ten moment udělá pocit „jooo, tohle", je celý systém potvrzený a zbytek je dopisování pravidel. Když ne, je to levná informace.
