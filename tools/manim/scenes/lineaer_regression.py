"""LUP: lineær regression med fiktive boligpriser og dansk speak."""

import os
import wave
from pathlib import Path
from statistics import mean

import manimpango
from manim import (
    DOWN,
    LEFT,
    RIGHT,
    UP,
    Axes,
    Create,
    DashedLine,
    Dot,
    FadeIn,
    FadeOut,
    LaggedStart,
    Line,
    MathTex,
    RoundedRectangle,
    Scene,
    Text,
    Transform,
    VGroup,
    Write,
)

INK = "#F0F6FF"
MUTED = "#AABDD2"
NAVY = "#0D1727"
SURFACE = "#13243A"
BORDER = "#2C435D"
CYAN = "#59DDCF"
GOLD = "#FFC96A"
PINK = "#FF98AA"
BLUE = "#8FB8FF"

TRAIN = ((40, 1.2), (60, 1.3), (80, 2.0), (100, 2.1), (120, 2.9))
TEST_X, TEST_Y = 90, 2.30
FIRST = (0.70, 0.015)  # (skæring, hældning); en forsøgsmodel


def fit(points):
    """Mindste kvadrater for én forklarende variabel med frit skæringspunkt."""
    x_bar = mean(x for x, _ in points)
    y_bar = mean(y for _, y in points)
    beta = sum((x - x_bar) * (y - y_bar) for x, y in points) / sum(
        (x - x_bar) ** 2 for x, _ in points
    )
    return y_bar - beta * x_bar, beta


def estimate(model, x):
    return model[0] + model[1] * x


def mse(model, points):
    return mean((y - estimate(model, x)) ** 2 for x, y in points)


def number(value, decimals):
    return f"{value:.{decimals}f}".replace(".", ",")


def texnum(value, decimals):
    return number(value, decimals).replace(",", "{,}")


BEST = fit(TRAIN)
PREDICTION = estimate(BEST, TEST_X)
FIRST_MSE, BEST_MSE = mse(FIRST, TRAIN), mse(BEST, TRAIN)
MODEL_TEX = rf"\hat y={texnum(BEST[0], 2)}+{texnum(BEST[1], 3)}x"

# Each group matches one numbered WAV. The same timing also works as a silent video.
CAPTIONS = (
    (
        "Hvad vil en bolig på 90 kvadratmeter koste?",
        "Vi kender priserne på fem andre boliger.",
        "Med lineær regression bruger vi dem til at lave et bud.",
    ),
    (
        "Hver prik er en bolig. På den vandrette akse har vi boligens areal.",
        "På den lodrette har vi salgsprisen i millioner kroner.",
        "Det er opdigtede tal til øvelsen.",
    ),
    (
        "Vi prøver at beskrive mønsteret med en ret linje.",
        "Alfa er linjens skæring med y-aksen.",
        "Beta er hældningen: hvor meget den forventede pris ændrer sig,\nnår arealet stiger med én kvadratmeter.",
    ),
    (
        "En bolig på 60 m² blev solgt for 1,3 millioner.\nVores første linje siger 1,6.",
        "Den lodrette forskel er −0,3 millioner.",
        "Den forskel kalder vi et residual.",
    ),
    (
        "Nogle residualer er positive, andre negative.",
        "Vi kvadrerer dem, så plus og minus ikke ophæver hinanden.",
        "Gennemsnittet af kvadraterne hedder middelkvadratfejlen,\nogså kaldet MSE.",
    ),
    (
        "Mindste kvadraters metode finder den linje,\nhvor middelkvadratfejlen er mindst på træningsdataene.",
        "Når vi drejer linjen her, falder fejlen",
        "fra 0,056 til 0,0272.",
    ),
    (
        "Nu indsætter vi 90 kvadratmeter.",
        "Modellen giver et bud på 2,11 millioner kroner.",
        "Det er en forudsigelse, som vi kan sammenligne med en salgspris.",
    ),
    (
        "Lad os teste med en ny bolig, som ikke var med i træningen.",
        "Hvis den faktisk blev solgt for 2,3 millioner,",
        "var vores bud 190.000 kroner for lavt.",
    ),
    (
        "En lav fejl på træningsdata betyder ikke,\nat modellen virker lige så godt på nye data.",
        "Hold derfor testdata adskilt fra træningen,",
        "og test på flere boliger.",
    ),
    (
        "Prøv selv: Hvad forudsiger modellen for 100 kvadratmeter?",
        "Indsæt 100 i formlen.",
        "Sæt eventuelt videoen på pause, før du ser svaret.",
    ),
)


class LineaerRegression(Scene):
    def construct(self):
        self.camera.background_color = NAVY
        fonts = set(manimpango.list_fonts())
        self.font = next((font for font in ("DejaVu Sans", "Arial") if font in fonts), "")
        self.audio = os.environ.get("MANIM_REGRESSION_AUDIO_DIR")
        self.caption = None
        self.stage = None
        self.chapter_number = 0
        self.frame("Introduktion", 0)
        self.intro()
        self.observations()
        self.first_line()
        self.one_residual()
        self.loss()
        self.best_fit()
        self.predict()
        self.test()
        self.generalize()
        self.exercise()

    def text(self, content, size=30, color=INK, bold=False, width=None):
        item = Text(
            content, font=self.font, font_size=size, color=color,
            weight="BOLD" if bold else "NORMAL", line_spacing=0.88,
        )
        if width is not None and item.width > width:
            item.scale_to_fit_width(width)
        return item

    def box(self, width, height, color=SURFACE, stroke=BORDER, radius=0.24):
        return RoundedRectangle(
            width=width, height=height, corner_radius=radius,
            stroke_color=stroke, stroke_width=1.4, fill_color=color, fill_opacity=1,
        )

    def frame(self, stage, step):
        if self.stage is None:
            self.add(
                self.text("LUP  /  MATEMATIK", 20, CYAN, True).move_to([-6.45, 3.54, 0], LEFT),
                Line([-6.45, 3.27, 0], [6.45, 3.27, 0], color=BORDER, stroke_width=1.3),
            )
        title = self.text(f"{step + 1:02d} / 06    {stage.upper()}", 19, MUTED, True)
        title.move_to([6.45, 3.54, 0], RIGHT)
        if self.stage is not None:
            self.remove(self.stage)
        self.stage = title
        self.add(title)

    def title(self, heading):
        return self.text(heading, 44, INK, True, 12.8).move_to([-6.45, 2.76, 0], LEFT)

    def panel(self, label, *rows):
        box = self.box(5.55, 4.33).move_to([3.67, -0.17, 0])
        eyebrow = self.text(label.upper(), 19, CYAN, True).move_to([1.24, 1.58, 0], LEFT)
        content = VGroup(*rows).arrange(DOWN, aligned_edge=LEFT, buff=0.29)
        if content.height > 3.18:
            content.scale_to_fit_height(3.18)
        content.move_to([1.24, 1.19, 0], LEFT + UP)
        return VGroup(box, eyebrow, content)

    def note(self, heading, body, color=MUTED):
        return VGroup(
            self.text(heading, 24, color, True, 4.9),
            self.text(body, 23, MUTED, width=4.9),
        ).arrange(DOWN, aligned_edge=LEFT, buff=0.11)

    def formula(self, latex, size=41, color=INK, width=4.88):
        item = MathTex(latex, font_size=size, color=color)
        if item.width > width:
            item.scale_to_fit_width(width)
        return item

    def chapter(self, fallback):
        i = self.chapter_number
        self.chapter_number += 1
        self.lines = CAPTIONS[i]
        if self.audio:
            path = Path(self.audio).resolve() / f"{i + 1:02d}.wav"
            with wave.open(str(path), "rb") as track:
                fallback = track.getnframes() / track.getframerate()
            self.add_sound(str(path), time_offset=0.16)
        self.chapter_length = fallback + 0.62
        self.weights = [len(line.replace("\n", " ")) + 15 for line in self.lines]
        self.beat_number = 0

    def beat(self, *animations, motion=0.85):
        i = self.beat_number
        self.beat_number += 1
        # Caption boundaries are estimated within each spoken segment.
        seconds = self.chapter_length * self.weights[i] / sum(self.weights)
        self.subtitle(self.lines[i])
        if animations:
            self.play(*animations, run_time=motion)
            seconds -= motion
        if seconds > 0:
            self.wait(seconds)

    def subtitle(self, caption):
        if self.caption is not None:
            self.remove(self.caption)
        text = self.text(caption, 25, INK, width=12.6).move_to([0, -3.39, 0])
        bg = self.box(13.1, 0.85, color="#0A1220", stroke="#1A2B41", radius=0.15)
        bg.move_to(text)
        self.caption = VGroup(bg, text).set_z_index(30)
        self.add(self.caption)

    def transition(self, heading, panel):
        old_title, old_panel = self.heading, self.side
        self.heading, self.side = self.title(heading), panel
        return FadeOut(old_title), FadeOut(old_panel), FadeIn(self.heading), FadeIn(self.side)

    def intro(self):
        topic = self.text("Lineær regression", 80, INK, True, 12.4).move_to([0, 1.13, 0])
        question = self.text("Hvad koster 90 m²?", 43, CYAN, True).move_to([0, -0.25, 0])
        badge = self.text("FEM SALG  ·  ÉN LINJE  ·  ET BUD", 21, GOLD, True)
        badge.move_to([0, -1.32, 0])
        accent = Line([-1.55, -0.94, 0], [1.55, -0.94, 0], stroke_color=BORDER)
        self.hero = VGroup(topic, question, accent, badge)
        self.chapter(10)
        self.beat(FadeIn(topic, shift=UP * 0.13), FadeIn(question, shift=UP * 0.13))
        self.beat(Create(accent), FadeIn(badge))
        self.beat()

    def plot(self):
        self.axes = Axes(
            x_range=[0, 140, 20],
            y_range=[0, 3.5, 0.5],
            x_length=6.04,
            y_length=3.52,
            axis_config={"color": MUTED, "stroke_width": 2, "tick_size": 0.055},
            tips=False,
        ).move_to([-2.83, -0.34, 0])
        ax = self.axes
        grids = VGroup(*[
            Line(ax.c2p(0, y), ax.c2p(140, y), color=BORDER, stroke_width=1)
            for y in (0.5, 1, 1.5, 2, 2.5, 3, 3.5)
        ])
        ticks = VGroup(
            *[self.text(str(x), 17, MUTED).next_to(ax.c2p(x, 0), DOWN, buff=0.12)
              for x in range(0, 141, 20)],
            *[self.text(number(y, 1), 17, MUTED).next_to(ax.c2p(0, y), LEFT, buff=0.14)
              for y in (0.5, 1, 1.5, 2, 2.5, 3, 3.5)],
        )
        labels = VGroup(
            self.text("Pris (mio. kr.)", 20, MUTED, True)
            .next_to(ax.c2p(0, 3.5), UP, buff=0.19).align_to(ax.c2p(0, 3.5), LEFT),
            self.text("Areal (m²)", 20, MUTED, True)
            .next_to(ax.c2p(140, 0), DOWN, buff=0.48).align_to(ax.c2p(140, 0), RIGHT),
        )
        self.points = VGroup(*[
            Dot(ax.c2p(x, y), radius=0.077, color=CYAN, stroke_color=NAVY, stroke_width=2)
            for x, y in TRAIN
        ]).set_z_index(4)
        self.plot_group = VGroup(grids, ax, ticks, labels)

    def observations(self):
        self.frame("Data", 1)
        self.plot()
        self.heading = self.title("Fra boligsalg til datapunkter")
        self.side = self.panel(
            "Træningsdata",
            self.note("Input: areal", "x måles i kvadratmeter", BLUE),
            self.note("Mål: salgspris", "y måles i millioner kroner", CYAN),
            self.note("5 fiktive boligsalg", "Hver prik har et areal og en salgspris."),
        )
        self.chapter(13)
        self.beat(FadeOut(self.hero), FadeIn(self.heading), FadeIn(self.plot_group))
        self.beat(LaggedStart(*(FadeIn(p, scale=1.4) for p in self.points), lag_ratio=0.19), motion=1.45)
        self.beat(FadeIn(self.side))

    def fit_line(self, model, color=GOLD):
        return Line(
            self.axes.c2p(0, estimate(model, 0)),
            self.axes.c2p(132, estimate(model, 132)),
            stroke_width=5, color=color,
        ).set_z_index(2)

    def first_line(self):
        self.frame("Model", 2)
        self.line = self.fit_line(FIRST)
        self.intercept = Dot(self.axes.c2p(0, FIRST[0]), color=GOLD, radius=0.085).set_z_index(5)
        panel = self.panel(
            "Modellen",
            self.formula(r"\hat y=\alpha+\beta_1 x", 49),
            self.note("α  ·  Skæring", "Linjens værdi ved x = 0.", GOLD),
            self.note("β  ·  Hældning", "Ændring i forventet pris pr. ekstra m².", BLUE),
        )
        self.chapter(14)
        self.beat(*self.transition("En ret linje giver et bud", panel), Create(self.line), motion=1.1)
        self.beat(FadeIn(self.intercept, scale=1.3))
        self.beat()

    def residual(self, model, x, y, width=3.7):
        y_hat = estimate(model, x)
        return Line(
            self.axes.c2p(x, y_hat), self.axes.c2p(x, y),
            color=GOLD if y >= y_hat else PINK, stroke_width=width,
        ).set_z_index(3)

    def one_residual(self):
        self.frame("Residualer", 3)
        x, y = TRAIN[1]
        y_hat = estimate(FIRST, x)
        line = self.residual(FIRST, x, y, 5.7)
        self.others = VGroup(*[self.residual(FIRST, px, py) for px, py in TRAIN if px != x])
        self.residuals = VGroup(line, *self.others)
        self.residual_guides = VGroup(*[
            DashedLine(self.axes.c2p(0, value), self.axes.c2p(x, value),
                       color=color, stroke_width=1.8, dash_length=0.105)
            for value, color in ((y, CYAN), (y_hat, GOLD))
        ])
        panel = self.panel(
            "Bolig på 60 m²",
            self.note("Faktisk pris", f"{number(y, 2)} mio. kr.", CYAN),
            self.note("Første linjes bud", f"{number(y_hat, 2)} mio. kr.", GOLD),
            self.formula(r"e=y-\hat y=" + texnum(y, 2) + "-" + texnum(y_hat, 2)
                         + "=" + texnum(y - y_hat, 2), 34, PINK),
        )
        self.chapter(14)
        self.beat(*self.transition("Hvor langt rammer linjen fra?", panel),
                  FadeOut(self.intercept), Create(self.residual_guides))
        self.beat(Create(line))
        self.beat()

    def loss(self):
        panel = self.panel(
            "Middelkvadratfejl",
            self.formula(r"\mathrm{MSE}=\frac{1}{n}\sum_{i=1}^{n}(y_i-\hat y_i)^2", 42),
            self.formula(r"(-0{,}30)^2=0{,}09", 35, PINK),
            self.note("1. Kvadrér residualerne", "Store afvigelser tæller mere.", GOLD),
            self.note("2. Tag gennemsnittet", "Summen divideres med antal boliger."),
        )
        self.chapter(15)
        self.beat(*self.transition("Gør fejlene sammenlignelige", panel), FadeOut(self.residual_guides),
                  LaggedStart(*(Create(r) for r in self.others), lag_ratio=0.11))
        self.beat()
        self.beat()

    def best_fit(self):
        panel = self.panel(
            "Fejl på de fem træningsboliger",
            self.note("Første linje", number(FIRST_MSE, 3) + " (mio. kr.)²", MUTED),
            self.note("Mindste kvadraters linje", number(BEST_MSE, 4) + " (mio. kr.)²", CYAN),
            self.formula(MODEL_TEX, 43, GOLD),
        )
        self.chapter(16)
        self.beat(*self.transition("Find linjen med mindst fejl", panel))
        optimum = VGroup(self.residual(BEST, *TRAIN[1]), *[
            self.residual(BEST, px, py) for px, py in TRAIN if px != TRAIN[1][0]
        ])
        self.beat(
            Transform(self.line, self.fit_line(BEST)),
            *(Transform(source, target) for source, target in zip(self.residuals, optimum)),
            motion=1.65,
        )
        self.beat()

    def predict(self):
        self.frame("Forudsigelse", 4)
        ax = self.axes
        end = ax.c2p(TEST_X, PREDICTION)
        self.projection = VGroup(
            DashedLine(ax.c2p(TEST_X, 0), end, color=BLUE, dash_length=0.1),
            DashedLine(end, ax.c2p(0, PREDICTION), color=BLUE, dash_length=0.1),
        )
        self.forecast_dot = Dot(end, radius=0.103, color=GOLD, stroke_color=NAVY, stroke_width=2)
        self.forecast_dot.set_z_index(6)
        self.x_label = self.text("90", 20, GOLD, True).next_to(ax.c2p(TEST_X, 0), DOWN, buff=0.38)
        panel = self.panel(
            "Modellens bud",
            self.formula(MODEL_TEX, 42),
            self.formula(r"\hat y(90)=" + texnum(PREDICTION, 2), 48, GOLD),
            self.note("90 m² → 2,11 mio. kr.", "Et estimat fra modellen."),
        )
        self.chapter(13)
        self.beat(*self.transition("Forudsig prisen på 90 m²", panel),
                  *(FadeOut(r) for r in self.residuals), FadeIn(self.x_label))
        self.beat(Create(self.projection), FadeIn(self.forecast_dot, scale=1.5))
        self.beat()

    def test(self):
        self.frame("Test", 5)
        self.test_dot = Dot(self.axes.c2p(TEST_X, TEST_Y), radius=0.102, color=PINK,
                            stroke_color=NAVY, stroke_width=2).set_z_index(7)
        self.test_error = self.residual(BEST, TEST_X, TEST_Y, 5.8).set_color(PINK)
        panel = self.panel(
            "Én ny bolig · ikke træningsdata",
            self.note("Forudsagt", number(PREDICTION, 2) + " mio. kr.", GOLD),
            self.note("Faktisk salgspris", number(TEST_Y, 2) + " mio. kr.", PINK),
            self.formula(r"e=2{,}30-2{,}11=+0{,}19", 41, PINK),
        )
        self.chapter(13)
        self.beat(*self.transition("Test på et nyt boligsalg", panel))
        self.beat(FadeIn(self.test_dot, scale=1.7))
        self.beat(Create(self.test_error))

    def generalize(self):
        train = self.note("TRÆNING", "Find linjen, der passer til kendte salg.", CYAN)
        test = self.note("TEST", "Mål fejlen på salg, modellen ikke har set.", PINK)
        groups = []
        for x, item in [(-3.0, train), (3.0, test)]:
            card = self.box(5.55, 2.14).move_to([x, 0.43, 0])
            item.move_to(card)
            groups.append(VGroup(card, item))
        takeaway = self.text("En lav træningsfejl garanterer ikke en lav testfejl.", 33, INK, True)
        takeaway.move_to([0, -1.57, 0])
        old = VGroup(
            self.heading, self.side, self.plot_group, self.points, self.line,
            self.x_label, self.projection, self.forecast_dot, self.test_dot, self.test_error,
        )
        self.heading = self.title("Hvor godt virker modellen på nye data?")
        self.summary = VGroup(*groups, takeaway)
        self.chapter(13)
        self.beat(FadeOut(old), FadeIn(self.heading), FadeIn(groups[0]))
        self.beat(FadeIn(groups[1]))
        self.beat(FadeIn(takeaway))

    def exercise(self):
        answer = estimate(BEST, 100)
        question = self.text("Hvad forudsiger modellen for 100 m²?", 49, INK, True, 12.2)
        question.move_to([0, 1.52, 0])
        model = self.formula(MODEL_TEX, 62, GOLD, 9).move_to([0, 0.25, 0])
        result = self.formula(r"\hat y(100)=0{,}22+0{,}021\cdot 100=" + texnum(answer, 2),
                              48, CYAN, 11).move_to([0, -1.32, 0])
        answer_text = self.text(f"{number(answer, 2)} mio. kr.", 34, INK, True).next_to(result, DOWN, buff=0.3)
        self.chapter(10)
        self.beat(FadeOut(self.heading), FadeOut(self.summary), FadeIn(question))
        self.beat(Write(model), motion=1.05)
        self.beat()
        self.wait(4)
        self.subtitle(f"Svar: {number(answer, 2)} millioner kroner.")
        self.play(FadeIn(result, shift=UP * 0.15), FadeIn(answer_text), run_time=0.85)
        self.wait(4.5)
