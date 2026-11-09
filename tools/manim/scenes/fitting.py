"""Danish LUP lesson on underfitting, overfitting and validation."""

import json
import os
from pathlib import Path
from textwrap import fill

import manimpango
from manim import (
    DOWN,
    LEFT,
    RIGHT,
    UP,
    Create,
    FadeIn,
    FadeOut,
    LaggedStart,
    Line,
    MathTex,
    RoundedRectangle,
    Scene,
    Succession,
    Text,
    Transform,
    VGroup,
    config,
)

from fitting_graphs import (
    BLUE,
    CYAN,
    GOLD,
    GRID,
    MODEL_COLORS,
    MUTED,
    NAVY,
    PINK,
    EarlyStoppingPlot,
    FittingPlot,
    fit_rmse,
)

INK = "#F2F7FC"
SURFACE = "#14253A"
DEGREES = (1, 2, 6)
LABELS = {1: "Underfitting", 2: "Passende fit", 6: "Overfitting"}
SPEAK = Path(__file__).with_name("fitting_speak.json")


def decimal(value):
    return f"{value:.2f}".replace(".", ",")


class Fitting(Scene):
    def construct(self):
        self.camera.background_color = NAVY
        fonts = set(manimpango.list_fonts())
        self.font = next((name for name in ("DejaVu Sans", "Arial") if name in fonts), "")
        script = json.loads(SPEAK.read_text(encoding="utf-8"))
        self.cues = [[cue if isinstance(cue, str) else cue["text"] for cue in group] for group in script]
        audio_dir = os.environ.get("MANIM_FITTING_AUDIO_DIR")
        self.audio_dir = Path(audio_dir).resolve() if audio_dir else None
        self.timings = (
            json.loads((self.audio_dir / "timings.json").read_text(encoding="utf-8"))
            if self.audio_dir
            else None
        )
        if self.timings and any(
            [cue["text"] for cue in track["cues"]] != captions
            for track, captions in zip(self.timings, self.cues, strict=True)
        ):
            raise ValueError("Speak-timings skal matche teksten i fitting_speak.json.")
        self.stage_label = None
        self.caption = None
        self.add(
            self.text("LUP  /  MACHINE LEARNING", 20, CYAN, True)
            .move_to([-6.46, 3.57, 0], LEFT),
            Line([-6.46, 3.29, 0], [6.46, 3.29, 0], color=GRID, stroke_width=1.4),
        )
        self.intro()
        self.data()
        for degree, cue_index in zip(DEGREES, (2, 3, 4)):
            self.fit_model(degree, cue_index)
        self.compare()
        self.early_stopping()
        self.folds()
        self.select_model()
        self.exercise()

    def text(self, value, size=30, color=INK, bold=False, max_width=12.75):
        item = Text(
            value, font=self.font, font_size=size, color=color,
            weight="BOLD" if bold else "NORMAL", line_spacing=0.9,
        )
        if item.width > max_width:
            item.scale_to_fit_width(max_width)
        return item

    def box(self, width, height, color=SURFACE, stroke=GRID, opacity=1):
        return RoundedRectangle(
            width=width, height=height, corner_radius=0.18,
            fill_color=color, fill_opacity=opacity, stroke_color=stroke, stroke_width=1.6,
        )

    def stage(self, index, name):
        if self.stage_label:
            self.remove(self.stage_label)
        self.stage_label = self.text(f"{index:02d} / 05    {name.upper()}", 19, MUTED, True)
        self.stage_label.move_to([6.46, 3.57, 0], RIGHT)
        self.add(self.stage_label)

    def title(self, value):
        return self.text(value, 44, bold=True).move_to([-6.46, 2.76, 0], LEFT)

    def subtitle(self, value):
        if self.caption:
            self.remove(self.caption)
        text = self.text(fill(value, 66, break_long_words=False), 25, max_width=12.5)
        text.move_to([0, -3.43, 0])
        box = self.box(13.0, 0.83, "#0A1220", "#20334A")
        box.move_to(text)
        self.caption = VGroup(box, text).set_z_index(30)
        self.add(self.caption)

    def until(self, target):
        remaining = target - self.time
        if remaining >= 1 / config.frame_rate:
            self.wait(remaining)

    def chapter(self, index, *beats, last_motion=1.15):
        captions = self.cues[index]
        if self.timings:
            track = self.timings[index]
            starts = [cue["start"] for cue in track["cues"]]
            length = track["duration"]
            self.add_sound(str(self.audio_dir / track["file"]))
        else:
            starts = [4.6 * i for i in range(len(captions))]
            length = 4.6 * len(captions)
        start = self.time
        for i, (caption, animations) in enumerate(zip(captions, beats, strict=True)):
            self.until(start + starts[i])
            self.subtitle(caption)
            if animations:
                self.play(*animations, run_time=last_motion if i == len(captions) - 1 else 1.15)
        self.until(start + length)

    def note(self, heading, body, color=INK, width=4.55):
        return VGroup(
            self.text(heading, 27, color, True, width),
            self.text(body, 24, MUTED, max_width=width),
        ).arrange(DOWN, aligned_edge=LEFT, buff=0.12)

    def panel(self, eyebrow, *rows):
        box = self.box(5.04, 4.36).move_to([3.93, -0.16, 0])
        label = self.text(eyebrow.upper(), 19, CYAN, True, 4.55)
        label.move_to([1.70, 1.63, 0], LEFT)
        content = VGroup(*rows).arrange(DOWN, aligned_edge=LEFT, buff=0.28)
        if content.height > 3.2:
            content.scale_to_fit_height(3.2)
        content.move_to([1.70, 1.22, 0], LEFT + UP)
        return VGroup(box, label, content)

    def replace_header_and_panel(self, title, panel):
        old_title, old_panel = self.heading, self.side
        self.heading, self.side = self.title(title), panel
        return FadeOut(old_title), FadeOut(old_panel), FadeIn(self.heading), FadeIn(self.side)

    def intro(self):
        self.stage(1, "Fra punkter til model")
        title = self.text("Over- og underfitting", 76, bold=True).move_to([0, 1.27, 0])
        question = self.text("Hvilken kurve virker på nye data?", 39, CYAN)
        question.move_to([0, 0.11, 0])
        labels = ("FOR SIMPEL", "PASSENDE FIT", "FOR FLEKSIBEL")
        pills = VGroup(*(
            VGroup(self.box(3.45, 0.65, SURFACE, color),
                   self.text(label, 20, color, True))
            for label, color in zip(labels, (BLUE, CYAN, PINK))
        )).arrange(RIGHT, buff=0.24).move_to([0, -1.13, 0])
        self.hero = VGroup(title, question, pills)
        self.chapter(0, (FadeIn(title, shift=UP * 0.14), FadeIn(question)), (FadeIn(pills),))

    def data(self):
        self.heading = self.title("To sæt punkter, to roller")
        self.plot = FittingPlot(width=7.4, height=4.55, font=self.font).move_to([-2.50, -0.24, 0])
        self.side = self.panel(
            "Konstrueret eksempel",
            self.note("7 træningspunkter", "Modellen får lov til at lære af dem.", CYAN),
            self.note("9 valideringspunkter", "De måler, hvor godt kurven rammer\npunkter, den ikke har lært af.", GOLD),
        )
        self.chapter(
            1,
            (FadeOut(self.hero), FadeIn(self.heading), FadeIn(self.plot.base),
             LaggedStart(*(FadeIn(dot, scale=1.35) for dot in self.plot.training_dots), lag_ratio=0.12)),
            (FadeIn(self.plot.validation_dots), FadeIn(self.side)),
        )
        self.curve = None
        self.residual_marks = None

    def fit_model(self, degree, cue_index):
        if degree == 1:
            self.stage(2, "Tre modeller")
        train, validation = fit_rmse(degree)
        details = {
            1: ("For simpel", "En ret linje kan ikke følge\nmønsterets krumning."),
            2: ("Fanger krumningen", "Kurven kommer tæt på\npunkterne i valideringen."),
            6: ("Følger støjen", "Kurven rammer træningspunkterne,\nmen svinger mellem dem."),
        }
        panel = self.panel(
            f"Polynomium af grad {degree}",
            self.note(*details[degree], MODEL_COLORS[degree]),
            self.note(f"Træning: {decimal(train)}", "RMSE på de 7 træningspunkter.", CYAN),
            self.note(f"Validering: {decimal(validation)}", "RMSE på de 9 valideringspunkter.", GOLD),
        )
        new_curve = self.plot.model_curve(degree)
        animations = [*self.replace_header_and_panel(LABELS[degree], panel)]
        if self.residual_marks is not None:
            animations.append(FadeOut(self.residual_marks))
        if self.curve is None:
            self.curve = new_curve
            animations.append(Create(self.curve))
        else:
            animations.append(Transform(self.curve, new_curve))
        self.residual_marks = self.plot.residuals(degree)
        self.chapter(cue_index, tuple(animations), (FadeIn(self.residual_marks),))

    def compare(self):
        heading = self.title("Hvilken model ville du vælge?")
        table = VGroup(self.box(12.9, 3.90).move_to([0, 0.28, 0]))
        table.add(
            self.text("MODEL", 21, MUTED, True).move_to([-5.92, 1.76, 0], LEFT),
            self.text("TRÆNING", 21, CYAN, True).move_to([1.64, 1.76, 0]),
            self.text("VALIDERING", 21, GOLD, True).move_to([4.72, 1.76, 0]),
        )
        highlighted = self.box(12.0, 0.72, "#173C47", CYAN).move_to([0, 0.15, 0])
        rows = VGroup()
        for y, degree in zip((1.04, 0.15, -0.74), DEGREES):
            train, validation = fit_rmse(degree)
            rows.add(VGroup(
                self.text(f"Grad {degree}   ·   {LABELS[degree]}", 29, MODEL_COLORS[degree], True)
                .move_to([-5.92, y, 0], LEFT),
                self.text(decimal(train), 33, CYAN, degree == 6).move_to([1.64, y, 0]),
                self.text(decimal(validation), 33, GOLD, degree == 2).move_to([4.72, y, 0]),
            ))
        rows.set_z_index(3)
        formula = MathTex(r"\mathrm{RMSE}=\sqrt{\frac{1}{n}\sum_{i=1}^{n}(y_i-\hat y_i)^2}",
                          font_size=34, color=MUTED).move_to([-3.36, -2.17, 0])
        lower = self.text("Lavere er bedre", 25, INK, True).move_to([4.55, -2.17, 0])
        old = VGroup(self.heading, self.side, self.plot.base, self.plot.training_dots,
                     self.plot.validation_dots, self.curve, self.residual_marks)
        self.comparison = VGroup(heading, table, highlighted, rows, formula, lower)
        self.chapter(
            5,
            (FadeOut(old), FadeIn(heading), FadeIn(table), FadeIn(rows), FadeIn(formula), FadeIn(lower)),
            (FadeIn(highlighted),),
        )

    def early_stopping(self):
        self.stage(3, "Tidligt stop")
        heading = self.title("Følg valideringstabet under træning")
        plot = EarlyStoppingPlot(width=7.4, height=4.55, font=self.font).move_to([-2.50, -0.24, 0])
        label = self.panel(
            "Illustreret tabskurve",
            self.note(f"Bedste epoke: {plot.checkpoint_epoch}", "Her er valideringstabet lavest.", GOLD),
            self.note("Vent på en forbedring", "Stop efter et valgt antal epoker\nuden forbedring."),
            self.note("Brug de bedste vægte", "Gå tilbage til den bedste epoke.", CYAN),
        )
        self.loss_view = VGroup(
            heading, plot.base, plot.training_curve, plot.validation_curve, plot.best_marker, label,
        )
        self.chapter(
            6,
            (FadeOut(self.comparison), FadeIn(heading), FadeIn(plot.base),
             Create(plot.training_curve), Create(plot.validation_curve)),
            (FadeIn(plot.best_marker), FadeIn(label)),
        )

    def folds(self):
        self.stage(4, "Krydsvalidering")
        title = self.title("10-fold krydsvalidering")
        detail = self.text("Del træningsdata i 10 dele", 34, INK, True).move_to([0, 1.45, 0])
        cells = VGroup(*(self.box(1.04, 0.91, "#15414A", "#2A8B88") for _ in range(10)))
        cells.arrange(RIGHT, buff=0.17).move_to([0, 0.12, 0])
        focus = self.box(1.04, 0.91, "#6F4E29", GOLD).move_to(cells[0])
        digits = VGroup(*(
            self.text(str(i), 30, INK, True).move_to(cell)
            for i, cell in enumerate(cells, start=1)
        )).set_z_index(4)
        key = VGroup(
            self.text("9 DELE TIL TRÆNING", 24, CYAN, True),
            self.text("1 DEL TIL VALIDERING", 24, GOLD, True),
        ).arrange(RIGHT, buff=1.1).move_to([0, -1.14, 0])
        note = self.text("Hver del holdes ude én gang.", 25, MUTED).move_to([0, -1.78, 0])
        changes = Succession(*(Transform(focus, focus.copy().move_to(cell)) for cell in cells[1:]))
        self.fold_view = VGroup(title, detail, cells, focus, digits, key, note)
        self.chapter(
            7,
            (FadeOut(self.loss_view), FadeIn(title), FadeIn(detail), FadeIn(cells), FadeIn(focus), FadeIn(digits)),
            (FadeIn(key), FadeIn(note), changes),
            last_motion=3.6,
        )

    def select_model(self):
        title = self.title("Vælg med validering. Test til sidst.")
        choose = self.box(6.05, 3.50).move_to([-3.19, -0.02, 0])
        confirm = self.box(6.05, 3.50).move_to([3.19, -0.02, 0])
        model_label = self.text("MODELVALG", 23, CYAN, True).move_to([-3.19, 1.15, 0])
        math = MathTex(r"\frac{e_1+e_2+\dots+e_{10}}{10}", font_size=49, color=INK)
        math.move_to([-3.19, 0.09, 0])
        left_note = self.text("Sammenlign gennemsnittet\nfor hver modelindstilling.", 25, MUTED)
        left_note.move_to([-3.19, -1.05, 0])
        test_label = self.text("SLUTTEST", 23, GOLD, True).move_to([3.19, 1.15, 0])
        test = self.text("Separat testsæt", 37, INK, True).move_to([3.19, 0.10, 0])
        right_note = self.text("Evaluér den valgte model\npå data, der var holdt helt ude.", 25, MUTED)
        right_note.move_to([3.19, -1.05, 0])
        left = VGroup(choose, model_label, math, left_note)
        right = VGroup(confirm, test_label, test, right_note)
        self.selection_view = VGroup(title, left, right)
        self.chapter(8, (FadeOut(self.fold_view), FadeIn(title), FadeIn(left)), (FadeIn(right),))

    def exercise(self):
        self.stage(5, "Fra LUP-øvelsen")
        heading = self.title("Overfitting på HIGGS-data")
        info = self.text(
            "4.Fit   ·   10.000 eksempler til træning   ·   1.000 til validering",
            25, MUTED,
        ).move_to([-6.46, 1.94, 0], LEFT)
        card = self.box(12.9, 2.82).move_to([0, 0.05, 0])
        label = self.text("TENSORFLOWS OFFENTLIGGJORTE KØRSEL   ·   STOR MODEL   ·   EPOKE 100",
                          18, MUTED, True).move_to([0, 1.02, 0])
        stats = VGroup(*(
            VGroup(self.text(value, 55, color, True), self.text(name, 22, MUTED, True))
            .arrange(DOWN, buff=0.16).move_to([x, -0.23, 0])
            for x, value, name, color in (
                (-3.14, "100,0 %", "TRÆNINGSNØJAGTIGHED", CYAN),
                (3.14, "66,7 %", "VALIDERINGSNØJAGTIGHED", GOLD),
            )
        ))
        source = self.text("Kilder: LUP 4.Fit  ·  TensorFlow  ·  Keras  ·  scikit-learn  ·  Elite Data Science",
                           21, MUTED).move_to([0, -2.53, 0])
        self.chapter(
            9,
            (FadeOut(self.selection_view), FadeIn(heading), FadeIn(info), FadeIn(card), FadeIn(label)),
            (FadeIn(stats),),
            (FadeIn(source),),
        )
        self.wait(2)
        self.subtitle("I regneeksemplet vælger vi grad 2, som havde den laveste valideringsfejl.")
        self.wait(4.5)
