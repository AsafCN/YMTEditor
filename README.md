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

### Save
`File -> Save` (**Ctrl+S**) writes back to the file you opened, without going through the
Save dialog every time. `Save As (YMT)` / `Save As (XML)` still ask for a path, and saving
somewhere else makes that file the one Ctrl+S writes to from then on.

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
