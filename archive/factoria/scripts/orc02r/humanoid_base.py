#!/usr/bin/env python3
"""Factoria HumanoidBase v4: коренастый орк по утверждённому эталону (правки MODEL_REWORK).

Что изменено относительно v3 по списку владельца:
  * голова ~35 % роста, сильнее надбровье, уже глаза, нос-клин, выраженные скулы,
    тяжёлая встроенная челюсть, клыки — часть формы лица (в модуле head);
  * торс короче и шире сверху, усилены плечевой пояс, трапеции и грудная масса;
  * руки короче: массивное плечо → узкий локоть → расширяющееся предплечье → кулак;
  * ноги короче: тяжёлое бедро, короткая голень, ступенчатая стопа (носок/пятка);
  * наплечник объёмный, ступенчатый (скруглённые ярусы, а не плоская плита);
  * меньше больших плоских поверхностей: больше сечений с кривизной и фасок.

Единый Sculpt → разметка частей → «канавка» на стыке. Геометрия CubeGame не используется.
Перед = -Y, x — влево/вправо (x>0 — левая сторона персонажа), z — вверх, 0 = пол.
"""

from __future__ import annotations

from voxelkit import Grid, Sculpt

MODULE_ORDER = [
    "head", "eyes", "brows", "mouth", "hair", "torso", "pelvis",
    "upper_arm_L", "forearm_L", "hand_L", "upper_arm_R", "forearm_R", "hand_R",
    "thigh_L", "shin_L", "foot_L", "thigh_R", "shin_R", "foot_R",
    "pouch", "weapon_club",
]


class HumanoidBuilder:
    def __init__(self, profile: dict):
        self.profile = profile
        self.L = profile["landmarks"]
        self.W = profile["widths"]
        self.face = profile["face"]
        self.hair_cfg = profile["hair"]
        self.outfit = profile["outfit"]
        self.weapon_cfg = profile.get("weapon", {})
        self.detail = profile["detail"]
        self.pal = profile["palette"]
        self.s = Sculpt()
        self.cut_log: list[dict] = []
        self.front = profile["surfaces"]

    def m(self, key: str, fallback: str | None = None) -> str:
        if key in self.pal:
            return key
        if fallback and fallback in self.pal:
            return fallback
        raise KeyError(f"нет материала {key}")

    # -------------------------------------------------------------------- масса --
    def build_mass(self):
        s, W, L = self.s, self.W, self.L
        skin = self.m("skin")
        waist = L["waist"]

        def torso_part(z):
            return "pelvis" if z < waist else "torso"

        # короткий тяжёлый торс: широкий верх, узкая талия, массивные трапеции
        s.tube([
            (L["crotch"], W["pelvis_width"], W["pelvis_depth"], 0.0, -0.2),
            (L["crotch"] + 3, W["pelvis_width"] - 1, W["pelvis_depth"] + 0.5, 0.0, -0.3),
            (waist - 2, W["waist_width"], W["waist_depth"], 0.0, -0.4),
            (waist, W["waist_width"] + 1, W["waist_depth"] + 0.5, 0.0, -0.5),
            (waist + 2, W["waist_width"] + 3, W["waist_depth"] + 1.5, 0.0, -0.6),
            (waist + 4, W["chest_width"] - 2, W["chest_depth"] - 1, 0.0, -0.7),
            (L["shoulder"] - 3, W["chest_width"] - 1, W["chest_depth"], 0.0, -0.8),
            (L["shoulder"] - 1, W["chest_width"], W["chest_depth"], 0.0, -0.8),
            (L["shoulder"], W["chest_width"] - 1, W["chest_depth"] - 1, 0.0, -0.6),
            (L["shoulder"] + 1, W["chest_width"] - 4, W["chest_depth"] - 3, 0.0, -0.2),
            (L["neck_top"], W["neck_width"], W["neck_depth"] - 1, 0.0, 0.0),
        ], torso_part, skin, "LARGE", p=3.4)

        # ноги: тяжёлое бедро, короткая голень
        cx = W["leg_center_x"]

        def leg_part(z):
            return "thigh_L" if z >= L["knee"] else "shin_L"

        s.tube([
            (L["ankle"] + 1, 9, 10, cx, 0.0),
            (L["ankle"] + 3, 10, 11, cx, 0.3),
            (L["knee"] - 2, W["shin_width"], W["shin_depth"], cx, 0.2),
            (L["knee"], W["shin_width"], W["shin_depth"], cx, 0.0),
            (L["knee"] + 1, W["thigh_width"] - 2, W["thigh_depth"] - 1, cx, -0.3),
            (L["crotch"] - 2, W["thigh_width"], W["thigh_depth"], cx, -0.3),
            (L["crotch"] - 1, W["thigh_width"] - 1, W["thigh_depth"] - 1, cx, -0.3),
        ], leg_part, skin, "LARGE", p=3.6)

        # руки: массивное плечо → узкий локоть → расширение предплечья
        acx = W["arm_center_x"]

        def arm_part(z):
            return "upper_arm_L" if z >= L["elbow"] else "forearm_L"

        s.tube([
            (L["wrist"] + 1, 8, 8, acx, -1.0),
            (L["wrist"] + 3, 9, 9, acx, -1.0),
            (L["elbow"] - 2, 8.5, 8.5, acx, -1.0),
            (L["elbow"], 7, 7.5, acx, -1.0),
            (L["elbow"] + 1, 8, 8.5, acx, -1.0),
            (L["elbow"] + 3, 10, 10, acx, -1.0),
            (L["shoulder"] - 1, 10, 10, acx, -1.0),
            (L["shoulder"], 8, 9, acx, -1.0),
        ], arm_part, skin, "LARGE", p=3.2)

        # кулак: скруглённая масса с костяшками и большим пальцем
        hw = W["hand_width"]
        hd = W["hand_depth"]
        palm_top = L["wrist"] - 1
        palm_bottom = L["wrist"] - 4
        s.section(palm_bottom, palm_top, hw, hd, "hand_L", skin, "LARGE",
                  cx=acx, cy=-1.5, p=2.6)
        s.section(L["hand_tip"], palm_bottom - 1, hw - 1, hd - 1, "hand_L", skin, "LARGE",
                  cx=acx, cy=-2.0, p=2.4)
        # большой палец с внутренней стороны
        s.box(int(acx - hw / 2) - 1, int(acx - hw / 2) - 1, -5, -2,
              palm_bottom + 1, palm_top, "hand_L", skin, "MEDIUM")
        # костяшки: подсвечены цветом на поверхности кулака, без выступов
        for fx in range(int(acx - hw / 2) + 1, int(acx + hw / 2), 2):
            s.put(fx, -6, palm_bottom + 1, "hand_L", self.m("skin_light"), "SMALL")

        # ступенчатая стопа: подошва, носок, середина, пятка
        fc = W["leg_center_x"]
        fw, fl = W["foot_width"], W["foot_length"]
        fx0, fx1 = fc - fw // 2, fc + fw // 2 - 1
        fy_front = -fl + 4
        s.box(fx0, fx1, fy_front, 3, L["ground"], L["ground"], "foot_L", skin, "LARGE")
        s.box(fx0, fx1, fy_front, fy_front + 3, L["ground"] + 1, L["ankle"] - 2, "foot_L",
              skin, "LARGE")
        s.box(fx0, fx1, fy_front + 4, 0, L["ground"] + 1, L["ankle"] - 1, "foot_L", skin,
              "LARGE")
        s.box(fx0, fx1, 1, 3, L["ground"] + 1, L["ankle"] - 2, "foot_L", skin, "LARGE")
        s.box(fx0 + 1, fx1 - 1, fy_front + 2, 1, L["ankle"] - 1, L["ankle"], "foot_L",
              skin, "MEDIUM")

    # ------------------------------------------------------------------- голова --
    def build_head(self):
        s, W, F = self.s, self.W, self.face
        L = self.L
        skin = self.m("skin")
        chin, top = L["chin"], L["skull_top"]
        fy = self.front["head"]
        hw = W["head_width"]
        cy = W["head_center_y"]
        eye = L["eye"]

        s.tube([
            (chin, 12, 12, 0.0, cy - 1.5),
            (chin + 1, 14, 13, 0.0, cy - 1.2),
            (chin + 2, 15, 14, 0.0, cy - 0.8),
            (chin + 3, hw - 1, W["head_depth"] - 2, 0.0, cy),
            (eye - 2, hw - 1, W["head_depth"] - 1, 0.0, cy),
            (eye, hw, W["head_depth"], 0.0, cy),
            (L["brow"] + 2, hw - 0.5, W["head_depth"] - 0.5, 0.0, cy),
            (L["brow"] + 5, hw, W["head_depth"], 0.0, cy),
            (top - 4, hw - 0.5, W["head_depth"] - 0.5, 0.0, cy),
            (top - 2, hw - 2, W["head_depth"] - 2, 0.0, cy),
            (top, hw - 5, W["head_depth"] - 5, 0.0, cy),
        ], "head", skin, "LARGE", p=3.6)

        # встроенная тяжёлая челюсть и подбородок (выступают вперёд)
        s.box(-6, 5, fy - 1, fy, chin, chin + 1, "head", skin, "MEDIUM")
        s.box(-4, 3, fy - 2, fy - 1, chin, chin, "head", skin, "MEDIUM")
        # массивное надбровье (2 вокселя вперёд) с центральной бороздой
        s.box(-hw // 2, hw // 2 - 1, fy - int(F["brow_depth"]), fy - 1, L["brow"],
              L["brow"] + int(F["brow_height"]) - 1, "head", skin, "MEDIUM")
        s.repaint(-1, 0, fy - int(F["brow_depth"]), fy - 1, L["brow"],
                  L["brow"] + int(F["brow_height"]) - 1, self.m("skin_shadow"), "SMALL",
                  parts=("head",))
        # выраженные скулы
        for sx in (-1, 1):
            x = hw // 2 - 2
            xx = x if sx > 0 else -1 - x
            s.box(xx - (1 if sx > 0 else 0), xx + (0 if sx > 0 else 1), fy - 1, fy - 1,
                  eye - 2, eye - 1, "head", self.m("skin_light"), "MEDIUM")
        # нос-клин: узкая переносица, широкий кончик, ноздри
        s.box(-1, 0, fy - 1, fy - 1, eye - 1, eye, "head", self.m("skin_light"), "MEDIUM")
        s.box(-2, 1, fy - 2, fy - 1, eye - 3, eye - 2, "head", self.m("skin_light"), "MEDIUM")
        for xx in (-2, 1):
            s.put(xx, fy - 2, eye - 3, "head", self.m("skin_shadow"), "SMALL")
        # большие уши (широкое основание, заострённый верх)
        for sx in (-1, 1):
            for i, (dz0, dz1, dy0, dy1) in enumerate(((0, 6, -4, 2), (1, 6, -3, 1), (2, 5, -2, 0))):
                x = hw // 2 + i
                xx = x if sx > 0 else -1 - x
                s.box(xx, xx, dy0, dy1, eye - 3 + dz0, eye - 3 + dz1, "head", skin, "MEDIUM")
            s.box(hw // 2 if sx > 0 else -1 - (hw // 2),
                  hw // 2 if sx > 0 else -1 - (hw // 2), -2, 0, eye - 2, eye,
                  "head", self.m("skin_shadow"), "SMALL")

        # пасть: углубление + клыки как часть формы челюсти
        mouth_z = eye - 5
        s.carve(-3, 2, fy, fy, mouth_z, mouth_z)
        s.box(-3, 2, fy - 1, fy - 1, mouth_z + 1, mouth_z + 1, "head", self.m("skin_dark"),
              "SMALL")
        # опора челюсти вокруг пасти
        s.box(-4, 3, fy - 1, fy - 1, mouth_z, mouth_z, "head", skin, "SMALL")
        s.carve(-3, 2, fy - 1, fy - 1, mouth_z, mouth_z)
        # клыки растут из нижней челюсти по углам пасти (2 вокселя в ширину)
        tusks = int(F["tusk_length"])
        for sx in (-1, 1):
            x0, x1 = (3, 4) if sx > 0 else (-5, -4)
            for i in range(tusks):
                zz = mouth_z + i
                for xx in (x0, x1):
                    s.put(xx, fy - 1, zz, "head", self.m("ivory"), "MEDIUM")
            # верхушка клыка сдвинута наружу — форма, а не прямоугольник
            s.put(x1 if sx > 0 else x0, fy - 1, mouth_z + tusks, "head",
                  self.m("ivory"), "SMALL")
        # шрамы и боевая раскраска
        if F.get("scars", True):
            for i in range(2):
                s.put(hw // 2 - 2, fy - 1, eye + i, "head", self.m("scar"), "SMALL")
            for i in range(2):
                s.put(4, fy - 2, eye - 3 - i, "head", self.m("skin_dark"), "SMALL")
        # светлая «морда» вокруг пасти
        s.repaint(-4, 3, fy - 1, fy, mouth_z - 1, mouth_z + 1, self.m("skin_light"), "MEDIUM",
                  parts=("head",))

    def build_face(self):
        s, W, L = self.s, self.W, self.L
        fy = self.front["head"]
        eye = L["eye"]
        # очень узкие глаза: щель 2x1 + янтарный зрачок
        for sx in (-1, 1):
            ex0 = -5 if sx < 0 else 3
            s.carve(ex0, ex0 + 1, fy, fy, eye, eye)
            s.box(ex0, ex0 + 1, fy, fy, eye, eye, "eyes", self.m("eye_sclera"), "SMALL")
            s.put(ex0 + (1 if sx < 0 else 0), fy, eye, "eyes", self.m("eye_iris"), "SMALL")
            # нависающее веко
            s.repaint(ex0 - 1, ex0 + 2, fy - 1, fy - 1, eye + 1, eye + 1,
                      self.m("skin_shadow"), "SMALL", parts=("head",))
        # толстые брови со сломом к переносице
        for sx in (-1, 1):
            for i, dz in enumerate((0, 0, 1)):
                x = 1 + i
                xx = x if sx > 0 else -1 - x
                s.box(xx, xx, fy - 2, fy - 1, eye + 2 + dz, eye + 3 + dz,
                      "brows", self.m("hair"), "SMALL")
        # губы: тёмная щель пасти и тень под губой
        mw = int(self.face["mouth_width"])
        s.box(-mw // 2 + 1, mw // 2 - 2, fy, fy, eye - 5, eye - 5, "mouth",
              self.m("mouth"), "SMALL")
        s.box(-2, 1, fy - 1, fy - 1, eye - 6, eye - 6, "mouth", self.m("skin_shadow"), "SMALL")

    def build_hair(self):
        cfg = self.hair_cfg
        if not cfg.get("enabled", True):
            return
        s, W, L = self.s, self.W, self.L
        hair = self.m("hair")
        hw = W["head_width"]
        top = L["skull_top"]
        band_z0, band_z1 = L["brow"] + 4, L["brow"] + 5
        # тёмные волосы на макушке и затылке (не шлем, а шапка по форме черепа)
        s.repaint(-hw, hw, -11, 9, top - 4, top, hair, "MEDIUM", parts=("head",))
        s.repaint(-hw, hw, 3, 9, band_z1 + 1, top - 5, hair, "MEDIUM", parts=("head",))
        # небольшой верхний хохолок
        s.box(-2, 1, -2, 1, top + 1, top + 1, "hair", hair, "MEDIUM")
        s.box(-1, 0, -1, 1, top + 2, top + 2, "hair", hair, "MEDIUM")
        s.box(-1, 0, 0, 1, top + 3, top + 3, "hair", hair, "SMALL")
        # красная повязка на лбу + узел и хвосты
        red = self.m("red")
        s.repaint(-hw // 2, hw // 2 - 1, self.front["head"], self.front["head"] + 1,
                  band_z0, band_z1, red, "MEDIUM", parts=("head",))
        for sx in (-1, 1):
            x = hw // 2 - 1
            xx = x if sx > 0 else -1 - x
            s.repaint(xx, xx, -9, 5, band_z0, band_z1, red, "MEDIUM", parts=("head",))
        s.repaint(-hw // 2, hw // 2 - 1, 5, 6, band_z0, band_z1, red, "MEDIUM",
                  parts=("head",))
        s.box(-1, 0, 6, 7, band_z0 - 1, band_z1, "head", self.m("red_dark"), "SMALL")
        for sx in (-1, 1):
            x = 2
            xx = x if sx > 0 else -1 - x
            s.box(xx, xx, 7, 7, band_z0 - 4, band_z0 - 1, "head", self.m("red_dark"), "SMALL")

    # ---------------------------------------------------------- мышцы и детали ---
    def build_muscles(self):
        s, W, L = self.s, self.W, self.L
        light, shadow = self.m("skin_light"), self.m("skin_shadow")
        bulge = float(self.detail.get("muscle_bulge", 1.0))
        acx, lcx = W["arm_center_x"], W["leg_center_x"]
        tf = self.front["torso"]
        # грудная масса и трапеции
        for sx in (-1, 1):
            s.ellipsoid(sx * 4.6, tf + 1.2, L["shoulder"] - 4.0, 4.2 * bulge, 2.2, 3.0,
                        "torso", light, "MEDIUM", p=2.6)
        s.repaint(-1, 0, tf, tf + 1, L["shoulder"] - 7, L["shoulder"] - 2, shadow, "SMALL",
                  parts=("torso",))
        for sx in (-1, 1):
            s.ellipsoid(sx * (W["chest_width"] // 2 - 2), -1.0, L["shoulder"] - 1,
                        2.6, 4.2, 2.2, "torso", light, "MEDIUM", p=2.6)
        # пресс
        for zz in (L["waist"] + 1, L["waist"] + 3):
            for sx in (-1, 1):
                x0 = 1 if sx > 0 else -5
                s.box(x0, x0 + 3, tf, tf + 1, zz, zz, "torso", light, "MEDIUM")
            s.repaint(-1, 0, tf, tf + 1, zz, zz, shadow, "SMALL", parts=("torso",))
        # дельтовидные (массивный плечевой пояс)
        for sx in (-1, 1):
            s.ellipsoid(sx * acx, -1.0, L["shoulder"] - 0.5, 5.0 * bulge, 4.6, 2.6,
                        "upper_arm_L", light, "MEDIUM", p=2.6)
        # бицепс/трицепс, расширение предплечья
        s.ellipsoid(acx, -5.2, L["elbow"] + 3.0, 3.4, 2.0, 2.6, "upper_arm_L", light,
                    "MEDIUM", p=2.6)
        s.ellipsoid(acx, 4.0, L["elbow"] + 2.5, 3.0, 1.8, 2.4, "upper_arm_L", shadow,
                    "MEDIUM", p=2.6)
        s.ellipsoid(acx, -5.0, L["wrist"] + 3.0, 3.4, 2.0, 2.6, "forearm_L", light,
                    "MEDIUM", p=2.6)
        # тяжёлое бедро, икра
        s.ellipsoid(lcx, -6.0, L["knee"] + 3.0, 4.8, 2.2, 2.8, "thigh_L", light,
                    "MEDIUM", p=2.8)
        s.ellipsoid(lcx, 4.8, L["knee"] + 2.0, 4.0, 2.0, 2.6, "thigh_L", shadow,
                    "MEDIUM", p=2.8)
        s.ellipsoid(lcx, 4.6, L["knee"] - 3.0, 3.6, 2.0, 2.4, "shin_L", light,
                    "MEDIUM", p=2.8)

    # ---------------------------------------------------------------- одежда -----
    def build_outfit(self):
        s, W, L, O = self.s, self.W, self.L, self.outfit
        tunic, tunic_d = self.m("tunic"), self.m("tunic_dark")
        pants, boot, sole = self.m("pants"), self.m("boot"), self.m("boot_sole")
        leather, leather_d = self.m("leather"), self.m("leather_dark")
        red, red_d = self.m("red"), self.m("red_dark")
        metal, bronze = self.m("metal"), self.m("bronze")
        metal_d = self.m("metal_dark")
        tf, pf, lf, sf, ff = (self.front["torso"], self.front["pelvis"], self.front["leg"],
                              self.front["shin"], self.front["foot"])
        acx, lcx = int(round(W["arm_center_x"])), W["leg_center_x"]

        # === туника на торсе (следует форме тела, а не плоская плита)
        if O.get("tunic", True):
            s.repaint(-W["chest_width"], W["chest_width"], tf - 1, 7, L["waist"] + 1,
                      L["shoulder"] - 1, tunic, "MEDIUM", parts=("torso",))
            s.repaint(-W["chest_width"], W["chest_width"], tf - 1, 7, L["waist"] + 1,
                      L["waist"] + 1, tunic_d, "SMALL", parts=("torso",))
            s.repaint(-W["chest_width"], W["chest_width"], tf - 1, 7, L["shoulder"],
                      L["neck_top"] - 1, tunic_d, "SMALL", parts=("torso",))
            s.repaint(-1, 0, tf - 1, tf, L["waist"] + 2, L["shoulder"] - 2, tunic_d, "SMALL",
                      parts=("torso",))
            for zz in (L["waist"] + 4, L["waist"] + 7):
                s.put(-1, tf - 1, zz, "torso", bronze, "SMALL")
            # короткие рукава
            s.repaint(acx - 6, acx + 5, -6, 4, L["shoulder"] - 1, L["shoulder"],
                      tunic, "MEDIUM", parts=("upper_arm_L",))

        # === штаны
        if O.get("pants", True):
            for part, z0, z1 in (("thigh_L", L["knee"] + 1, L["crotch"] - 1),
                                 ("shin_L", L["ankle"] + 1, L["knee"] - 1)):
                s.repaint(lcx - 8, lcx + 7, -8, 6, z0, z1, pants, "MEDIUM", parts=(part,))
            s.repaint(lcx - 5, lcx + 4, lf - 1, lf, L["knee"] + 1, L["knee"] + 1, leather_d,
                      "SMALL", parts=("thigh_L",))

        # === сапоги: носок, пятка, ремни, светлая подошва
        if O.get("boots", True):
            fw, fl = W["foot_width"], W["foot_length"]
            fx0, fx1 = lcx - fw // 2, lcx + fw // 2 - 1
            s.repaint(fx0, fx1, ff, 3, 1, L["ankle"], boot, "LARGE", parts=("foot_L",))
            s.repaint(fx0, fx1, ff, 3, L["ground"], L["ground"], sole, "SMALL",
                      parts=("foot_L",))
            s.box(fx0, fx1, ff + 2, ff + 2, 1, L["ankle"] - 1, "foot_L", leather, "MEDIUM")
            s.box(fx0, fx1, 1, 1, 1, L["ankle"] - 1, "foot_L", leather, "MEDIUM")
            s.repaint(fx0 + 1, fx1 - 1, ff, ff + 1, 1, 2, metal, "SMALL", parts=("foot_L",))

        # === широкий красный пояс + пряжка
        if O.get("sash", True):
            belt_z0, belt_z1 = L["waist"] - 2, L["waist"] - 1
            s.tube([(belt_z0, W["waist_width"] + 2, W["waist_depth"] + 2, 0.0, -0.3),
                    (belt_z1, W["waist_width"] + 2, W["waist_depth"] + 2, 0.0, -0.3)],
                   "pelvis", red, "MEDIUM", p=3.0)
            s.box(-2, 1, pf - 2, pf - 1, belt_z0, belt_z1, "pelvis", metal, "MEDIUM")
            s.put(-1, pf - 2, belt_z0 + 1, "pelvis", bronze, "SMALL")
            s.tube([(belt_z0 - 1, W["waist_width"] + 1, W["waist_depth"] + 1, 0.0, -0.3),
                    (belt_z0 - 1, W["waist_width"] + 1, W["waist_depth"] + 1, 0.0, -0.3)],
                   "pelvis", leather_d, "SMALL", p=3.0)

        # === красная ткань спереди и сзади
        if O.get("loincloth", True):
            hem = L["crotch"] - 6
            for zz in range(hem, L["waist"] - 2):
                half = 2 if zz > hem + 2 else 1
                s.box(-half, half + 1, pf - 1, pf, zz, zz, "pelvis", red, "MEDIUM")
            s.carve(-1, -1, pf - 1, pf, hem, hem)
            s.carve(2, 2, pf - 1, pf, hem, hem)
            s.repaint(-2, 3, pf - 1, pf, hem, hem, red_d, "SMALL", parts=("pelvis",))
            for zz in range(L["crotch"] - 1, L["waist"] - 2):
                s.box(-4, 3, 5, 6, zz, zz, "pelvis", red_d, "MEDIUM")

        # === диагональный ремень через грудь
        if O.get("diagonal_strap", True):
            steps = 9
            for i in range(steps):
                t = i / (steps - 1)
                x = int(round(6 * (1 - t) + (-8) * t))
                z = int(round((L["shoulder"] - 1) * (1 - t) + (L["waist"] + 1) * t))
                s.box(x, x + 1, tf - 1, tf, z, z, "torso", leather, "MEDIUM")
            s.box(-1, 0, tf - 1, tf - 1, L["waist"] + 5, L["waist"] + 6, "torso", bronze, "SMALL")

        # === объёмный ступенчатый наплечник (скруглённые ярусы вокруг плеча)
        side = str(O.get("pauldron_side", "RIGHT")).upper()
        sgn = 1 if side == "LEFT" else -1
        pcx = acx if sgn > 0 else -1 - acx

        def srang(x0, x1):
            return (x0, x1) if sgn > 0 else (-1 - x1, -1 - x0)

        for i, (z0, w, d, cy_, mat) in enumerate((
            (L["shoulder"] + 1, 12, 11, -1.0, leather),
            (L["shoulder"] - 1, 13, 12, -1.0, leather),
            (L["shoulder"] - 3, 12, 11, -0.6, leather_d),
        )):
            s.section(z0, z0 + 1, w, d, "torso", mat, "MEDIUM", cx=pcx, cy=cy_, p=2.6)
            # тёмная кромка яруса — ступень читается как объём
            s.section(z0, z0, w + 0.5, d + 0.5, "torso", leather_d, "SMALL", cx=pcx, cy=cy_, p=2.6)
        # бронзовые заклёпки по переднему краю ярусов (без плоской железной полосы)
        for xx in (acx - 5, acx + 3):
            s.put(xx if sgn > 0 else -1 - xx, tf - 2, L["shoulder"], "torso", bronze, "SMALL")
        s.put(acx if sgn > 0 else -1 - acx, tf - 2, L["shoulder"] - 2, "torso", bronze, "SMALL")

        # === наручи
        for _ in range(int(O.get("bracers", 2))):
            s.tube([(L["wrist"] + 2, W["forearm_width"] + 1, W["forearm_depth"] + 1,
                     W["arm_center_x"], -1.0),
                    (L["wrist"] + 4, W["forearm_width"] + 1, W["forearm_depth"] + 1,
                     W["arm_center_x"], -1.0)],
                   "forearm_L", leather, "MEDIUM", p=3.0)
            s.box(acx - 3, acx + 3, -7, -6, L["wrist"] + 4, L["wrist"] + 4, "forearm_L",
                  bronze, "SMALL")

        # === сумка на правом бедре
        if O.get("pouch", True):
            s.box(-11, -8, 2, 6, L["crotch"] + 1, L["crotch"] + 5, "pouch", leather_d, "MEDIUM")
            s.box(-11, -8, 2, 6, L["crotch"] + 5, L["crotch"] + 5, "pouch", leather, "SMALL")
            s.put(-10, 2, L["crotch"] + 3, "pouch", bronze, "SMALL")
            s.box(-9, -8, 1, 2, L["waist"] - 3, L["waist"] - 2, "pouch", leather_d, "SMALL")

    # ---------------------------------------------------------------- оружие -----
    def build_weapon(self):
        cfg = self.weapon_cfg
        if not cfg.get("enabled", True):
            return
        s, W, L = self.s, self.W, self.L
        wood, stone = self.m("wood"), self.m("stone")
        leather_d = self.m("leather_dark")
        acx = int(round(W["arm_center_x"]))
        hx = -acx
        s.box(hx - 3, hx - 1, -4, -2, L["hand_tip"], L["shoulder"] + 1, "weapon_club", wood,
              "MEDIUM")
        s.box(hx - 3, hx - 1, -5, -1, L["wrist"] - 2, L["wrist"] + 1, "weapon_club", leather_d,
              "SMALL")
        s.box(hx - 3, hx - 1, -5, -1, L["shoulder"], L["shoulder"] + 1, "weapon_club",
              leather_d, "SMALL")
        # каменная голова с фасетками
        s.box(hx - 7, hx - 1, -7, -1, L["shoulder"] + 2, L["shoulder"] + 5, "weapon_club",
              stone, "MEDIUM")
        s.box(hx - 6, hx - 2, -6, -2, L["shoulder"] + 6, L["shoulder"] + 6, "weapon_club",
              stone, "MEDIUM")
        s.box(hx - 6, hx - 2, -6, -2, L["shoulder"] + 1, L["shoulder"] + 1, "weapon_club",
              stone, "MEDIUM")
        s.box(hx - 7, hx - 6, -4, -2, L["shoulder"] + 2, L["shoulder"] + 5, "weapon_club",
              stone, "SMALL")
        for cx_, cy_ in ((hx - 7, -7), (hx - 1, -7), (hx - 7, -1), (hx - 1, -1)):
            s.carve(cx_, cx_, cy_, cy_, L["shoulder"] + 2, L["shoulder"] + 5)
        s.put(hx - 4, -7, L["shoulder"] + 4, "weapon_club", self.m("metal_light"), "SMALL")
        s.put(hx - 5, -7, L["shoulder"] + 3, "weapon_club", self.m("metal_dark"), "SMALL")
        s.repaint(hx - 7, hx - 1, -7, -1, L["shoulder"] + 2, L["shoulder"] + 2,
                  self.m("metal_dark"), "SMALL", parts=("weapon_club",))

    # --------------------------------------------------------------- детали ------
    def build_details(self):
        s, W, L = self.s, self.W, self.L
        bronze = self.m("bronze")
        tf = self.front["torso"]
        # заклёпки наплечника
        for zz in (L["shoulder"] - 1, L["shoulder"] - 3):
            s.put(13, tf - 2, zz, "torso", bronze, "SMALL")
        # швы кулака
        hw = W["hand_width"]
        x0 = int(round(W["arm_center_x"] - hw / 2.0))
        for fx in (x0 + 2, x0 + 4, x0 + 6):
            s.repaint(fx, fx, -6, -2, L["hand_tip"], L["wrist"] - 5,
                      self.m("skin_shadow"), "SMALL", parts=("hand_L",))

    # ---------------------------------------------------------------- стыки ------
    def cut_joints(self):
        L = self.L
        bridging = ("red", "red_dark", "metal", "metal_dark", "metal_light", "bronze")
        ranges = {
            "head": (L["chin"], L["skull_top"] + 1),
            "torso": (L["waist"], L["neck_top"]),
            "pelvis": (L["crotch"], L["waist"] - 1),
            "upper_arm_L": (L["elbow"], L["shoulder"]), "forearm_L": (L["wrist"] + 1, L["elbow"] - 1),
            "hand_L": (L["hand_tip"], L["wrist"] - 1),
            "upper_arm_R": (L["elbow"], L["shoulder"]), "forearm_R": (L["wrist"] + 1, L["elbow"] - 1),
            "hand_R": (L["hand_tip"], L["wrist"] - 1),
            "thigh_L": (L["knee"], L["crotch"] - 1), "shin_L": (L["ankle"] + 1, L["knee"] - 1),
            "foot_L": (L["ground"], L["ankle"]),
            "thigh_R": (L["knee"], L["crotch"] - 1), "shin_R": (L["ankle"] + 1, L["knee"] - 1),
            "foot_R": (L["ground"], L["ankle"]),
        }
        clamped = 0
        body_mats = {self.m(k) for k in ("skin", "skin_light", "skin_shadow", "skin_dark",
                                        "pants", "boot", "tunic", "tunic_dark")
                     if k in self.pal}
        for part, (zmin, zmax) in ranges.items():
            for pos, val in list(self.s.cells.items()):
                if val[2] == part and val[0] in body_mats and not (zmin <= pos[2] <= zmax):
                    del self.s.cells[pos]
                    clamped += 1
        cuts = [("head", L["chin"]), ("torso", L["waist"]), ("pelvis", L["crotch"]),
                ("thigh_L", L["knee"]), ("shin_L", L["ankle"] + 1),
                ("upper_arm_L", L["elbow"]), ("forearm_L", L["wrist"] + 1),
                ("thigh_R", L["knee"]), ("shin_R", L["ankle"] + 1),
                ("upper_arm_R", L["elbow"]), ("forearm_R", L["wrist"] + 1)]
        for part, z in cuts:
            self.cut_log.append({"part": part, "z": z,
                                 "dropped_voxels": self.s.cut_shell(part, z, keep_mats=bridging)})
        self.cut_log.append({"clamped_skin_voxels": clamped})

    # ------------------------------------------------------------------ сборка ---
    def build(self) -> dict[str, Grid]:
        self.build_mass()
        self.build_head()
        self.build_muscles()
        self.build_outfit()
        self.build_weapon()
        self.build_hair()
        self.build_face()
        self.build_details()
        self.s.merge(self.s.mirror_side("L"))
        self.cut_joints()
        return self.s.to_grids(MODULE_ORDER)
