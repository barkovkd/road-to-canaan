# Where the game's sounds come from

`Tools/sounds.sh <sources dir>` builds `Assets/Canaan/Resources/Sounds` from the recordings below (the sources dir
holds one folder per source, archives unpacked into `x/`). Everything else is synthesised in `Core/Sfx.cs`: the
engine's exhaust beat, whistle and bell, the hero's whistle, the bow, the snort. The game's menu has a page
«Авторы звуков» with the same credits; CC-BY sounds must keep their credit there.

| folder | file(s) | source | author | licence | used for |
|---|---|---|---|---|---|
| `firearms` | `Prepared SFX Library.7z` (1911, Smith & Wesson 642, Model 1894, Marlin 336, 1917, Tikka, Mosin Nagant, Model 12, Mossberg, Nova, Ruger Single Six, Ruger Mark III) | [The Free Firearm Sound Library](https://opengameart.org/content/the-free-firearm-sound-library) | Ben Jaszczak, Brian Nelson, Kevin Heras, Matthew Nanney | CC0 | `shot_*` (near and far takes; see `gunmap.py`) |
| `kenney_rpg` | RPG Audio pack | [kenney.nl/assets/rpg-audio](https://kenney.nl/assets/rpg-audio) | Kenney | CC0 | steps on dirt, doors, coins, pickup, knife, clicks, reload |
| `kenney_impact` | Impact Sounds pack | [kenney.nl/assets/impact-sounds](https://kenney.nl/assets/impact-sounds) | Kenney | CC0 | steps on boards and grass, punches, bullet impacts |
| `kenney_interface` | Interface Sounds pack | [kenney.nl/assets/interface-sounds](https://kenney.nl/assets/interface-sounds) | Kenney | CC0 | menu |
| `horse_gallop` | `archive.zip` (`ground`, `grass`, `muffled`, `hall`) | [Horse gallop on different surfaces](https://opengameart.org/node/167525) | congusbongus, from D4XX, StephenSaldanha, MrFizzywig (CC0) and Twigy233 (`hall`, CC BY 4.0) | CC BY 4.0 | `hoof_dirt` (ground, grass, muffled), `hoof_wood` (hall) |
| `zebra` | `zebra.flac` | [Zebra Whinny](https://opengameart.org/node/132780) | AntumDeluge, from a video by Peet J van Eeden | CC BY 3.0 (video CC BY) | `whinny` (pitched down to a horse) |
| `park` | `park_ambience_wind.wav`, `park_ambience_birds.wav`, `park_ambience_river.wav` | [Park ambiences](https://opengameart.org/content/park-ambiences) | Thimras | CC0 | `wind`, `birds`, `river` |
| `rain` | `Dark_Rainy_Night(ambience).ogg` | [rain and thunders](https://opengameart.org/content/rain-and-thunders) | kindland | CC0 | `rain` (the first 23 s, rumble cut), `thunder` (the four loudest rolls) |
| `crickets` | `crickets_1.mp3` | [Crickets Ambient Noise - loopable](https://opengameart.org/content/crickets-ambient-noise-loopable) | Wolfgang_ | CC0 | `crickets` |
| `fire` | `fire-1.wav` | [Fire Crackling](https://opengameart.org/content/fire-crackling) | AntumDeluge | CC0 | `fire` (camp fires, forges) |

Direct files: `https://opengameart.org/sites/default/files/` + `Prepared%20SFX%20Library.7z`, `archive_2.zip`,
`zebra.flac`, `park_ambience_wind.wav`, `park_ambience_birds.wav`, `park_ambience_river.wav`,
`Dark_Rainy_Night%28ambience%29.ogg`, `crickets_1.mp3`, `fire-1.wav`. The Kenney packs download from their pages.
