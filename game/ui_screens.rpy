# Focused project screen overrides. The base template stays in screens.rpy.

screen say(who, what):

    default active_tip = ""

    window:
        id "window"

        if who is not None:

            window:
                id "namebox"
                style "namebox"
                vbox:
                    spacing 4
                    text who id "who"
                    add Solid("#b9b1a780") xsize 280 ysize 2

        text what id "what"

        hbox:
            xpos 812
            ypos 50
            spacing 7

            imagebutton:
                style "st_dialogue_icon_button"
                idle Transform("gui/dialogue/button_skip.png", xysize=(44, 44), alpha=0.84)
                hover Transform("gui/dialogue/button_skip.png", xysize=(44, 44), alpha=1.0)
                action Skip()
                hovered SetScreenVariable("active_tip", _("快进"))
                unhovered SetScreenVariable("active_tip", "")
                focus_mask True

            imagebutton:
                style "st_dialogue_icon_button"
                idle Transform("gui/dialogue/button_auto.png", xysize=(44, 44), alpha=0.84)
                hover Transform("gui/dialogue/button_auto.png", xysize=(44, 44), alpha=1.0)
                action Preference("auto-forward", "toggle")
                hovered SetScreenVariable("active_tip", _("自动"))
                unhovered SetScreenVariable("active_tip", "")
                focus_mask True

            imagebutton:
                style "st_dialogue_icon_button"
                idle Transform("gui/dialogue/button_history.png", xysize=(44, 44), alpha=0.84)
                hover Transform("gui/dialogue/button_history.png", xysize=(44, 44), alpha=1.0)
                action ShowMenu("history")
                hovered SetScreenVariable("active_tip", _("历史"))
                unhovered SetScreenVariable("active_tip", "")
                focus_mask True

        if active_tip:
            text active_tip:
                style "st_dialogue_tip_text"
                xpos 860
                ypos 98

    if not renpy.variant("small"):
        add SideImage() xalign 0.0 yalign 1.0


screen choice(items):
    style_prefix "choice"

    vbox at choice_vbox_in:
        text _("选择下一步行动") style "st_choice_hint_text" xalign 0.5

        for i in items:
            textbutton i.caption action i.action at choice_item_in


screen quick_menu():

    zorder 100

    if quick_menu:

        hbox at quick_menu_in:
            style_prefix "quick"
            style "quick_menu"

            textbutton _("回退") action Rollback()
            textbutton _("历史") action ShowMenu("history")
            textbutton _("快进") action Skip() alternate Skip(fast=True, confirm=True)
            textbutton _("自动") action Preference("auto-forward", "toggle")
            textbutton _("保存") action ShowMenu("save")
            textbutton _("快存") action QuickSave()
            textbutton _("快读") action QuickLoad()
            textbutton _("设置") action ShowMenu("preferences")


style window:
    background Transform("gui/dialogue/box_compact.png", xysize=(gui.textbox_width, gui.textbox_height))

style say_label:
    font "fonts/SourceHanSansCN/SourceHanSansCN-Bold.otf"
    size 29
    color "#8b6c56"
    outlines [ (1, "#fffaf0cc", 0, 0) ]

style say_dialogue:
    font "fonts/SourceHanSansCN/SourceHanSansCN-Bold.otf"
    size 31
    color "#4e4a44"
    outlines [ (1, "#fffaf0aa", 0, 0) ]
    line_spacing 5
    slow_cps 28
    slow_abortable True
    textshader "dissolve:6"

style input_prompt:
    font "fonts/SourceHanSansCN/SourceHanSansCN-Bold.otf"
    size 38
    color "#8b5524"
    outlines [ (2, "#fff8eccf", 0, 0), (1, "#d08a3448", 0, 2) ]

style input:
    font "fonts/SourceHanSansCN/SourceHanSansCN-Heavy.otf"
    size 38
    color "#d88324"
    outlines [ (2, "#fffaf0e6", 0, 0), (1, "#7c3f1c55", 0, 2) ]

style choice_vbox:
    xalign 0.5
    ypos 416
    yanchor 0.5
    spacing gui.choice_spacing

style choice_button is default:
    properties gui.button_properties("choice_button")
    background Frame("gui/button/choice_idle_background.png", gui.choice_button_borders, tile=gui.choice_button_tile)
    hover_background Frame("gui/button/choice_hover_background.png", gui.choice_button_borders, tile=gui.choice_button_tile)
    padding (42, 18, 42, 18)

style choice_button_text is default:
    properties gui.text_properties("choice_button")
    xalign 0.5
    textalign 0.5
    hover_outlines [ (1, "#fff4dfcc", 0, 0) ]

style quick_menu:
    xalign 0.5
    yalign 1.0
    yoffset -8

style quick_button:
    properties gui.button_properties("quick_button")
    background Frame("gui/button/quick_idle_background.png", gui.quick_button_borders, tile=gui.button_tile)
    hover_background Frame("gui/button/quick_hover_background.png", gui.quick_button_borders, tile=gui.button_tile)

style quick_button_text:
    properties gui.text_properties("quick_button")

style navigation_button_text:
    color "#f7ead8"
    hover_color "#ffd28f"
    selected_color "#ffd28f"
    insensitive_color "#f7ead880"
    outlines [ (2, "#4d2317aa", 0, 2) ]

style main_menu_title:
    color "#fff4df"
    outlines [ (3, "#5f2d18b0", 0, 3) ]

style main_menu_version:
    size 26
    color "#f7d2a8"
    outlines [ (2, "#5f2d18a8", 0, 2) ]

style game_menu_label_text:
    color "#c96b27"

style confirm_frame:
    background Frame("gui/frame.png", gui.confirm_frame_borders, tile=gui.frame_tile)

style confirm_prompt_text:
    color "#4e3828"
    line_spacing 5

style confirm_button_text:
    color "#6b5542"
    hover_color "#b85222"
