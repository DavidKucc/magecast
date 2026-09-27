# Mage Cast

PvP aréna ze třetí osoby, kde se kouzla **kreslí myší** — runa, zamíření, vypuštění.
Co hra je a jak funguje: [MECHANIKA.md](MECHANIKA.md).

## Hrát v prohlížeči

**https://davidkucc.github.io/magecast/**

- Chrome, Edge nebo Firefox na počítači, s myší.
- Do hry se klikne, aby si vzala myš. **Esc** myš vrátí a otevře menu.
- Hra ve dvou: jeden dá *Host a game* a pošle kód, druhý ho zadá u *Join*.
- Trénink na webu potřebuje internet (jde přes Unity Relay, stejně jako hra ve dvou).
- Při zkoušení ve dvou na jednom počítači dej každou hru do **samostatného okna**, ne do záložky —
  prohlížeč zpomalí záložku, která není vidět, a spojení pak vypadá jako lag.

## Projekt

Unity 2022.3.30f1, Built-in Render Pipeline, Netcode for GameObjects + Unity Relay (WebSockets,
takže se web i Windows verze potkají ve stejné místnosti).

**Postava a animace nejsou v repozitáři.** Jsou z Mixama, jehož podmínky dovolují je mít ve hře, ale ne
šířit samostatně. Kdo si projekt otevře odsud, musí si vlastní postavu dát do `Assets/Characters` a
animace do `Assets/Animation` (podrobně v komentářích `Assets/Editor/CharacterAnimatorBuilder.cs`)
a spustit *Tools → Arena → Rebuild Character Animator* a *Build Player Prefab*.

Buildy:
- *Tools → Arena → Build Web* → `Builds/Web`, zveřejňuje se na větev `gh-pages`
- *Tools → Arena → Build Windows Test* → `Builds/MageCast/MageCast.exe`
