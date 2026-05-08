# Project story definitions for the prologue.

define n = Character(None)
define m = Character("秋穰子", color="#d98a2b")
define s = Character("秋静叶", color="#b85a4a")
define p = Character("[st_player_name]", color="#6f8fb8")

default st_player_name = "旅人"
default st_minoriko_affection = 0
default st_shizuha_trust = 0
default st_autumn_mystery = 0

image bg autumn_garden = Transform("images/backgrounds/autumn_garden.png", xysize=(1920, 1080))
image bg autumn_room = Transform("images/backgrounds/autumn_room.png", xysize=(1920, 1080))
image minoriko neutral = "images/characters/minoriko.png"
image sizuha neutral = "images/characters/sizuha.png"

transform minoriko_right:
    xpos 0.72
    ypos 1.02
    xanchor 0.5
    yanchor 1.0
    zoom 0.66
    on show:
        alpha 0.0
        xoffset 64
        easeout 0.28 alpha 1.0 xoffset 0

transform sizuha_left:
    xpos 0.28
    ypos 1.02
    xanchor 0.5
    yanchor 1.0
    zoom 0.66
    on show:
        alpha 0.0
        xoffset -64
        easeout 0.28 alpha 1.0 xoffset 0
