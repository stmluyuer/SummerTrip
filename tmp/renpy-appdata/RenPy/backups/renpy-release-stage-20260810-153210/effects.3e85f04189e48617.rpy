init:
    define slow_dissolve = Dissolve(0.85)
    define scene_blend = Fade(0.35, 0.2, 0.55, color="#120b08")
    define memory_flash = Fade(0.08, 0.04, 0.35, color="#f1d3a1")

transform bg_drift:
    alpha 0.0
    zoom 1.04
    xalign 0.5
    yalign 0.5
    linear 0.8 alpha 1.0
    ease 12.0 zoom 1.08 xalign 0.47 yalign 0.52

transform soft_scene_in:
    alpha 0.0
    zoom 1.02
    linear 0.55 alpha 1.0
    ease 2.2 zoom 1.0

transform say_window_in:
    on show:
        alpha 0.0
        yoffset 34
        easeout 0.28 alpha 1.0 yoffset 0
    on hide:
        easein 0.18 alpha 0.0 yoffset 18

transform namebox_in:
    on show:
        alpha 0.0
        xoffset -24
        easeout 0.25 alpha 1.0 xoffset 0

transform choice_vbox_in:
    on show:
        alpha 0.0
        yoffset 28
        easeout 0.35 alpha 1.0 yoffset 0
    on hide:
        easein 0.18 alpha 0.0 yoffset 18

transform choice_item_in:
    on show:
        alpha 0.0
        xoffset 32
        pause 0.06
        easeout 0.25 alpha 1.0 xoffset 0
    on hover:
        easeout 0.12 xoffset 10
    on idle:
        easeout 0.18 xoffset 0

transform quick_menu_in:
    on show:
        alpha 0.0
        yoffset 18
        easeout 0.25 alpha 1.0 yoffset 0

transform menu_panel_in:
    on show:
        alpha 0.0
        xoffset -34
        easeout 0.35 alpha 1.0 xoffset 0

transform title_float:
    on show:
        alpha 0.0
        yoffset 20
        easeout 0.55 alpha 1.0 yoffset 0
    block:
        ease 3.2 yoffset -6
        ease 3.2 yoffset 0
        repeat

transform page_in:
    on show:
        alpha 0.0
        yoffset 24
        easeout 0.32 alpha 1.0 yoffset 0

transform leaf_shiver:
    alpha 0.0
    easeout 0.25 alpha 0.22
    block:
        ease 1.6 alpha 0.12
        ease 1.6 alpha 0.24
        repeat

image autumn_glow = Solid("#d98a2b22")
image mystery_glow = Solid("#6f8fb833")
