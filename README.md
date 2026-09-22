# YMTEditor
 - Program allows to edit GTA5 *.ymt (Peds clothes) files
 - Using [Codewalker.Core](https://github.com/dexyfex/CodeWalker) by @dexyfex

---

## What this fork adds

A fork of [grzybeek/YMTEditor](https://github.com/grzybeek/YMTEditor) with three things on top:

### Build from ped folder
`File -> Build from ped folder...` reads the **names** of the .ydd/.ytd files in a ped folder
and fills in every component, drawable and texture at once, instead of clicking them in one by
one. Everything a ymt holds is already in the names:

| File | Becomes |
|---|---|
| `ig_mike^jbib_003_u.ydd` | component `jbib`, drawable 3, `_u` -> propMask 1 |
| `ig_mike^jbib_diff_003_b_uni.ytd` | its texture **b**, `uni` -> texId 0 |
| `ig_mike^head_000_r.ydd` + `..._diff_000_a_whi.ytd` | `_r` -> propMask 17, `whi` -> texId 1 |
| `ig_mike_p^p_head_000.ydd` | a prop on `ANCHOR_HEAD`, in the same ymt |
| `ig_mike^jbib_003_u.yld` / `_u_1.ydd` | ownsCloth / numAlternatives |

Numbers are kept as they are, because the number in the file name *is* the drawable index the
game loads. A number with no files (usually 000) stays as an empty drawable, which is what gives
a ped its "no mask / no vest" default. Afterwards it reports what looks wrong: textures without a
model, missing texture letters, a texture numbered one off from its model, and files that aren't
named the way GTA expects.

A folder can also be dropped straight onto YMTEditor.exe, or passed on the command line.

### Batch rename
`File -> Batch rename files...` renames files in bulk, with a live preview and clashes
flagged before anything happens. Drag files or folders onto the window, pick them with the
buttons, or point it at a folder - files from several folders at once are fine, and it works
on any file, so it doubles as a plain renamer. Beside the plain text rules there are ped aware
ones, which is where the time goes:

| Rule | Does |
|---|---|
| Rename the ped | `ig_old^jbib_000_u.ydd`, `ig_old_p^p_head_000.ydd`, `ig_old.ymt` and `ig_old.yft` all become `ig_new...` in one go |
| Move component | `jbib_004_u.ydd` -> `task_004_u.ydd`, textures included, slot position only |
| Shift numbers by | moves drawable numbers (+5 turns 004 into 009), optionally in one slot - handy before merging two peds |
| Replace / Remove / Add | plain text on the file name, e.g. `_uni` -> `_whi` |
| lower case | for folders that came back from Windows in mixed case |

Two files landing on one name, or landing on a file that isn't moving, are marked in the
preview and skipped. Renames go through a temporary name, so shifting a whole component
down by one can't overwrite anything, and `rename-log.txt` records what moved.

### Sort ped folder
`File -> Sort ped folder (close numbering gaps)...` renames the files so the numbers run
without gaps: a component with 001 and 005 ends up with 001 and 002, and a drawable whose
textures are b, d ends up with a, b.

Numbers **below** the first one in use are left alone, so a component that deliberately
starts at 001 (an empty 000 being the ped's "none" option) keeps that empty slot instead of
everything sliding down onto it. Only the folder holding most of the ped is touched, since a
resource often keeps older copies of the same names in sibling folders.

Nothing is renamed until the preview is confirmed; what happened is written to
`renumber-log.txt` next to the files, and the ymt is rebuilt from the sorted folder
afterwards.

### Save
`File -> Save` (**Ctrl+S**) writes back to the file you opened, without going through the
Save dialog every time. `Save As (YMT)` / `Save As (XML)` still ask for a path, and saving
somewhere else makes that file the one Ctrl+S writes to from then on.

### Dark theme
The whole editor is restyled: dark slate background, flat rounded cards, purple accents,
slim scroll bars and a dark title bar. It is one implicit-style dictionary
(`Themes/Modern.xaml`) merged in `App.xaml`, so no window had to be rebuilt for it, and
each drawable's **Remove** button now sits in the card header instead of being placed with
a negative margin.

### Double-clicking a .ymt opens it
The editor now opens whatever file it was started with, so a .ymt opens with its contents
loaded instead of an empty editor. `File -> Open .ymt files with YMTEditor` registers the file
association (for your user only, no admin needed), and the item under it removes it again and
puts back whatever opened .ymt files before.
### Download YMTEditor [here](https://github.com/grzybeek/YMTEditor/releases) ###
### Join Official Discord [here](https://discord.gg/xUXbrupFhN) ###
## Tutorials about YMTEditor (FiveM)
 - [How to stream clothes and props as addons for mp freemode](https://forum.cfx.re/t/how-to-stream-clothes-and-props-as-addons-for-mp-freemode-models/3345474)
 - [How to create addon heels or hide hair with addon hat](https://forum.cfx.re/t/how-to-create-addon-heels-or-hide-hair-with-addon-hat/4209989)


_My coding skills are not greatest but it works 😛_
