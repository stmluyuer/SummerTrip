# Project-level UI theme values. Kept outside screens.rpy so Ren'Py's
# generated template remains easy to compare with upstream.

init offset = -1

init python:
    gui.accent_color = "#c96b27"
    gui.idle_color = "#7d6c5a"
    gui.idle_small_color = "#9a8a76"
    gui.hover_color = "#d98a2b"
    gui.selected_color = "#4c382a"
    gui.insensitive_color = "#7d6c5a80"
    gui.muted_color = "#6f4b2f"
    gui.hover_muted_color = "#9a6036"
    gui.text_color = "#5a554e"
    gui.interface_text_color = "#5a554e"

    gui.textbox_width = 1060
    gui.textbox_height = 338
    gui.name_xpos = 142
    gui.name_ypos = 78
    gui.dialogue_xpos = 142
    gui.dialogue_ypos = 128
    gui.dialogue_width = 782

    gui.choice_button_width = 1185
    gui.choice_button_borders = Borders(120, 14, 120, 14)
    gui.choice_button_text_idle_color = "#6b5542"
    gui.choice_button_text_hover_color = "#b85222"
    gui.choice_button_text_insensitive_color = "#7d6c5a80"
    gui.choice_spacing = 24

    gui.navigation_xpos = 68
    gui.navigation_spacing = 8
    gui.quick_button_borders = Borders(14, 6, 14, 4)
    gui.quick_button_text_size = 21
    gui.quick_button_text_idle_color = "#786b5f"
    gui.quick_button_text_hover_color = "#a44920"
    gui.quick_button_text_selected_color = "#c96b27"

style st_panel_frame is empty:
    background Frame(Solid("#fff7ead9"), Borders(18, 18, 18, 18))
    padding (28, 24, 28, 24)

style st_dialogue_icon_button is default:
    xysize (46, 46)

style st_dialogue_tip_text is gui_text:
    size 19
    color "#6b5542"
    outlines [ (1, "#fff8ec", 0, 0) ]

style st_choice_hint_text is gui_text:
    size 22
    color "#8f7a65"
    textalign 0.5

style st_main_menu_subtitle is main_menu_text:
    size 30
    color "#fff4df"
    outlines [ (2, "#5f2d18a8", 0, 2) ]

style st_about_text is about_text:
    line_spacing 8
