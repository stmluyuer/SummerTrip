# 秋姐妹 AVG 序章：《秋日归途》
# 放入 Ren'Py 项目的 game/ 目录后可直接运行。
# 之后有素材时，可把 scene black / scene expression 替换为正式背景，
# 把注释里的 show/play music 改成你的立绘和音乐文件名。

define n = Character(None)
define m = Character("秋穰子", color="#d98a2b")
define s = Character("秋静叶", color="#b85a4a")
define p = Character("[player_name]", color="#6f8fb8")

image bg autumn_garden = Transform("images/bg_autumn_garden.png", xysize=(1920, 1080))
image bg autumn_room = Transform("images/bg_autumn_room.png", xysize=(1920, 1080))
image minoriko = "images/minoriko.png"
image sizuha = "images/sizuha.png"

transform minoriko_right:
    xalign 0.8
    yalign 1.0
    zoom 0.72
    on show:
        alpha 0.0
        xoffset 40
        easeout 0.28 alpha 1.0 xoffset 0


transform sizuha_left:
    xalign 0.2
    yalign 1.0
    zoom 0.72
    on show:
        alpha 0.0
        xoffset 40
        easeout 0.28 alpha 1.0 xoffset 0


default player_name = "旅人"
default minoriko_affection = 0
default shizuha_trust = 0
default autumn_mystery = 0

label start:

    $ player_name = renpy.input("你的名字是？", default="旅人").strip()
    if player_name == "":
        $ player_name = "旅人"

    window hide None
    jump prologue_tree

label prologue_tree:

    window hide None
    scene bg autumn_garden
    show autumn_glow at leaf_shiver
    with scene_blend
    window auto
    # play music "audio/autumn_morning.ogg" fadein 1.0

    n "风里有泥土、草叶和一点烤番薯的甜味。"
    n "你睁开眼，最先看见的是一片红得过分鲜明的叶子。"          # ← 去"时"
    n "它挂在枝头，却像被谁从边缘擦掉了颜色，一半红，一半青。"

    show minoriko at minoriko_right
    m "啊，你醒啦！"
    m "先别急着起来。你刚才倒在树下，脸色比霜打过的叶子还难看。"

    n "一个戴着红色荷叶边帽子的少女蹲在你身旁。帽檐上垂着葡萄藤，衣角还沾着田里的细土。"

    p "这里是……哪里？"

    m "妖怪之山的山脚。嗯——反正不是那种会把人丢树下不管的地方。"  # ← 原文语义略绕，改清晰
    m "我是秋穰子，掌管丰收的神明。你呢？外界人？迷路的人？还是被谁捉弄了？"

    n "你试着回想，却只记得一阵突如其来的雾，以及雾中像钟声一样的低响。"

    m "想不起来也没关系。先跟我回去吧。"
    m "虽然我本来要把收好的作物带回家……"

    n "她回头看了一眼树下的竹篮，像是在心里和那篮番薯认真道歉。"

    m "算了，救人比较重要。作物先放在这里，等下再回来拿！"

    n "她向你伸出手。掌心温暖，带着晒过稻穗的气味。"

    jump aki_house_wakeup

label aki_house_wakeup:

    window hide None
    scene bg autumn_room
    # scene black
    show autumn_glow at leaf_shiver
    with slow_dissolve
    window auto
    # scene bg aki_house_day
    # show minoriko smile at left
    # show shizuha neutral at right

    n "再次清醒时，你躺在一间暖色木屋的客房里。窗外有风拂过，红叶轻叩窗棂。"  # ← "风吹过/敲在窗棂上"改为更轻的节奏

    
    m "醒得正好。我煮了热汤，放的是新收的萝卜。"                      # ← "里面放了"→"放的是"，更口语
    
    show sizuha at sizuha_left
    s "穰子，他刚醒，不要把碗直接塞到人家手里。"

    n "说话的是另一位少女。她站在窗边，金色眼睛落在你袖口，又落在桌上的一片叶子上。"

    s "秋静叶。她的姐姐。"                                           # ← 删"我是"，更寡言
    s "你身上有外界的尘，也有不属于这个季节的气息。"

    m "姐姐，你说得太吓人了。"

    s "我只是说事实。"
    s "这片叶子是在你旁边捡到的。红色在往回褪——正常的秋天不会这样。"  # ← "倒着褪回绿色"微调，节奏更利落

    n "桌上的叶片一半红，一半青，像被看不见的手拨乱了时间。"

    menu:
        "先感谢穰子的照顾":
            $ minoriko_affection += 1
            jump thank_minoriko

        "询问静叶那片叶子的异常":
            $ shizuha_trust += 1
            $ autumn_mystery += 1
            jump ask_shizuha_leaf

        "说明自己只记得雾和钟声":
            $ autumn_mystery += 1
            jump mention_bell

label thank_minoriko:

    p "谢谢你救了我……还有热汤。"                                      # ← 加省略号，语气更自然

    m "嘿嘿，不用那么正式啦。"
    m "丰收之神看到有人饿着、冷着、倒在树下，总不能当作没看见吧。"

    s "所以你把收好的作物忘在树下了。"

    m "呜。"

    n "穰子的笑容僵了一瞬。窗外的风恰好吹过，像是在替谁小声提醒。"

    m "我、我等下就去拿！"

    menu:
        "提出陪穰子一起去取作物":
            $ minoriko_affection += 1
            jump farm_basket

        "建议先听静叶说完异常":
            $ shizuha_trust += 1
            jump ask_shizuha_leaf

label ask_shizuha_leaf:

    p "这片叶子为什么会倒着变回绿色？"

    s "如果只是颜色褪去，那是秋天结束。"
    s "但它不是褪色。它在回到夏天。"

    n "静叶把叶子举到窗前。光穿过叶脉，你看见红与青交界处有细小的裂纹。"

    s "有人——或者某种力量——把季节的顺序弄乱了。"                    # ← 加破折号，静叶说话更有停顿感
    s "你倒下的地方，是第一处异常。"

    m "也就是说，只要回到那棵树下面，也许就能找到线索？"

    s "也许。也可能找到麻烦。"

    menu:
        "立刻回到红叶树下调查":
            $ autumn_mystery += 1
            jump maple_tree_investigation

        "先帮穰子取回作物，再一起调查":
            $ minoriko_affection += 1
            jump farm_basket

label mention_bell:

    p "我只记得雾，还有像钟声一样的声音。"

    m "钟声？山脚这边没有钟吧。"

    s "秋祭会场有旧铃。不是钟，但在雾里听起来可能相似。"

    n "静叶的视线微微一沉。"

    s "如果声音来自那里，事情就不只是迷路了。"

    menu:
        "询问秋祭是什么":
            jump ask_festival

        "请求她们带你去听到钟声的方向":
            $ autumn_mystery += 1
            jump maple_tree_investigation

label ask_festival:

    p "秋祭是什么？"

    m "是感谢收成、迎接红叶最盛时节的祭典！会有热汤、烤番薯、栗子饭，还有很多人来参拜。"

    s "也是秋天秩序最容易被看见的时刻。"

    s "如果异常和秋祭有关，我们最好在祭典结束前弄清楚。"

    n "她说得很平静，但你看见她指尖压住了那片倒色的叶子。"

    jump chapter_one_choice

label farm_basket:

    window hide None
    scene bg autumn_room
    show autumn_glow at leaf_shiver
    with scene_blend
    window auto
    # scene bg mountain_path_day
    # show minoriko worried

    show minoriko at minoriko_right
    n "你和穰子沿着山脚小路往回走。树影落在路上，像一层被风揉皱的红色薄毯。"

    m "希望篮子还在。里面有今天最好的番薯。"
    m "我本来想晚上烤给姐姐吃的。她最近总说红叶颜色不够漂亮，心情有点差。"

    p "你很在意她。"

    m "那当然。她可是我姐姐。"
    m "虽然她有时候说话像冷掉的茶，但其实很温柔。"

    n "前方的红叶树下，竹篮还在。"
    n "只是篮子旁边，多了一圈浅浅的青色草痕。"

    $ autumn_mystery += 1

    jump maple_tree_investigation

label maple_tree_investigation:

    window hide None
    scene bg autumn_room
    show mystery_glow at leaf_shiver
    with scene_blend
    window auto
    # scene bg maple_tree_day
    # show minoriko neutral at left
    # show shizuha quiet at right

    show minoriko at minoriko_right
    n "你们回到那棵红叶树下。"
    n "树根旁的草叶呈现出奇怪的青色，像有一小块夏天被硬塞进了秋天。"

    s "就是这里。"

    m "我的篮子也在这里。太好了……不对，现在不是高兴的时候。"

    n "静叶蹲下身，捡起一片叶子。穰子则把手放在竹篮旁，像在确认作物有没有受伤。"

    s "旅人，你站在这里时，有没有感觉到什么？"

    menu:
        "触碰那片青色草痕":
            $ autumn_mystery += 1
            jump touch_green_trace

        "请穰子用丰收之力确认作物变化":
            $ minoriko_affection += 1
            jump minoriko_checks_crops

        "请静叶继续观察红叶颜色":
            $ shizuha_trust += 1
            jump shizuha_reads_leaves

label touch_green_trace:

    n "你伸手触碰草痕。"
    n "一瞬间，雾声、铃声、夏夜的虫鸣同时涌进耳中。"

    p "这里……不是只有秋天。"

    s "看来你能感到边界。"

    m "那就更不能放着不管了。"

    jump chapter_one_choice

label minoriko_checks_crops:

    m "我试试看。"

    n "穰子把掌心覆在竹篮上。几枚番薯微微发热，泥土的香气变得清晰。"

    m "奇怪。它们成熟过头了。"
    m "明明刚收起来，却像在地里多待了半个月。"

    s "季节被加速了。至少在作物上是这样。"

    jump chapter_one_choice

label shizuha_reads_leaves:

    s "红叶不是单纯变色。它们在记住风经过的顺序。"

    n "静叶沿着叶脉轻轻一划。"

    s "这里的顺序断了。有人从秋天里拿走了一小段时间。"

    m "拿走时间？听起来像很麻烦的妖怪会做的事。"

    s "也可能是迷路的人无意中带来的。"

    jump chapter_one_choice

label chapter_one_choice:

    window hide None
    scene bg autumn_room
    show autumn_glow at leaf_shiver
    with scene_blend
    window auto
    # scene bg aki_house_evening

    show minoriko at minoriko_right
    n "傍晚，小屋的灯亮了。锅里煮着热汤，桌上放着那片倒色的叶子。"       # ← 去"时"
    n "秋祭还有三天。若在那之前找不到原因，你也许会被困在这个错位的秋天里。"

    m "明天我想去小农场看看。作物如果也受影响，大家的收成都会出问题。"

    s "我去红叶林。颜色先乱起来的地方，通常知道最多。"

    n "两人同时看向你。"

    menu:
        "明天帮穰子调查小农场":
            $ minoriko_affection += 1
            jump minoriko_route_open

        "明天陪静叶前往红叶林":
            $ shizuha_trust += 1
            jump shizuha_route_open

        "提议三人一起行动，先整理所有线索":
            $ autumn_mystery += 1
            jump sisters_route_open

label minoriko_route_open:

    m "那就说定了！明天要早起哦。"
    m "我会准备热乎乎的早饭。调查也要先吃饱才行。"

    s "别把人家喂到走不动。"

    n "穰子鼓起脸，静叶却轻轻笑了一下。"
    n "你的第一个秋日夜晚，就在热汤的香气与红叶敲窗声里落下帷幕。"

    return

label shizuha_route_open:

    s "红叶林清晨很冷。穿厚一点。"

    m "姐姐这是在关心你哦。"

    s "我只是避免他冻倒之后，又要把人搬回来。"

    n "静叶移开视线，耳尖却比窗外的叶子更红。"
    n "你的第一个秋日夜晚，就在未解的叶色与安静的灯火中落下帷幕。"

    return

label sisters_route_open:

    p "如果异常同时影响作物和红叶，也许分开会漏掉什么。"

    m "三个人一起？听起来也不错。"

    s "效率未必高，但至少穰子不会把篮子又忘在路边。"

    m "姐姐！"

    n "争辩声让小屋热闹起来。桌上的叶子却在灯下轻轻颤动，红与青的边界又扩大了一点。"
    n "你的第一个秋日夜晚，就在姐妹的拌嘴与逼近的谜团中落下帷幕。"

    return
